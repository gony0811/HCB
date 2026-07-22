using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class WaferSeqTabViewModel : ObservableObject
    {
        internal static readonly SolidColorBrush DefaultDieBrush;
        internal static readonly SolidColorBrush SelectedDieBrush;
        internal static readonly SolidColorBrush BondedDieBrush;

        static WaferSeqTabViewModel()
        {
            DefaultDieBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50));
            DefaultDieBrush.Freeze();
            SelectedDieBrush = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB));
            SelectedDieBrush.Freeze();
            BondedDieBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
            BondedDieBrush.Freeze();
        }

        public RecipeService RecipeService { get; }

        [ObservableProperty] private int waferSize = 6;
        [ObservableProperty] private double dieSizeX = 10.0;
        [ObservableProperty] private double dieSizeY = 10.0;
        [ObservableProperty] private double gapX = 0.5;
        [ObservableProperty] private double gapY = 0.5;
        [ObservableProperty] private double centerX;
        [ObservableProperty] private double centerY;

        [ObservableProperty] private List<DieData> dieList;
        [ObservableProperty] private DieData selectedDie;
        [ObservableProperty] private bool hasDieSelected;

        public SettingsViewModel Settings { get; }
        public StepSeqTabViewModel StepSeqTab { get; }

        private readonly ILogger _logger;

        // 비전/모션 서비스 (VisionTabViewModel과 동일한 주입 패턴)
        private readonly SequenceService _sequenceService;
        private readonly EqpCommunicationService _communication;

        [ObservableProperty] private bool isBonding;

        public WaferSeqTabViewModel(
            RecipeService recipeService,
            SettingsViewModel settingsViewModel,
            StepSeqTabViewModel stepSeqTabViewModel,
            SequenceService sequenceService,
            EqpCommunicationService communication,
            ILogger logger)
        {
            RecipeService = recipeService;
            Settings = settingsViewModel;
            StepSeqTab = stepSeqTabViewModel;
            _sequenceService = sequenceService;
            _communication = communication;
            _logger = logger.ForContext<WaferSeqTabViewModel>();

            // Interlock 발생 시 진행 중인 정렬을 즉시 취소
            _sequenceService.InterlockActivated += OnInterlockActivated;

            LoadRecipeParams();
            GenerateWaferMap();
        }

        [RelayCommand]
        private void LoadRecipeParams()
        {
            if (RecipeService.UseRecipe == null) return;
            try { WaferSize = (int)RecipeService.FindByParamDouble("WaferSize"); } catch { }
            try { DieSizeX = RecipeService.FindByParamDouble("DieSizeX"); } catch { }
            try { DieSizeY = RecipeService.FindByParamDouble("DieSizeY"); } catch { }
            try { GapX = RecipeService.FindByParamDouble("GapX"); } catch { }
            try { GapY = RecipeService.FindByParamDouble("GapY"); } catch { }
        }

        [RelayCommand]
        private async Task ApplyAndSave()
        {
            if (RecipeService.UseRecipe == null) return;

            await SaveParam("WaferSize", WaferSize.ToString(), ValueType.Integer);
            await SaveParam("DieSizeX", DieSizeX.ToString(), ValueType.Double, UnitType.mm);
            await SaveParam("DieSizeY", DieSizeY.ToString(), ValueType.Double, UnitType.mm);
            await SaveParam("GapX", GapX.ToString(), ValueType.Double, UnitType.mm);
            await SaveParam("GapY", GapY.ToString(), ValueType.Double, UnitType.mm);

            GenerateWaferMap();
        }

        private async Task SaveParam(string name, string value, ValueType valueType, UnitType unitType = UnitType.None)
        {
            var recipe = RecipeService.UseRecipe;
            var existing = recipe.ParamList.FirstOrDefault(p => p.Name == name);

            if (existing != null)
            {
                existing.Value = value;
                await RecipeService.UpdateRecipeParam(existing);
            }
            else
            {
                var param = new RecipeParamDto
                {
                    RecipeId = recipe.Id,
                    Name = name,
                    Value = value,
                    ValueType = valueType,
                    UnitType = unitType
                };
                await RecipeService.AddRecipeParam(param);
            }
        }

        [RelayCommand]
        private void GenerateWaferMap()
        {
            var dies = new List<DieData>();
            double halfCol = (WaferSize - 1) / 2.0;
            double halfRow = (WaferSize - 1) / 2.0;
            double radius = (WaferSize + 1) / 2.0;

            for (int row = 0; row < WaferSize; row++)
            {
                for (int col = 0; col < WaferSize; col++)
                {
                    double dx = col - halfCol;
                    double dy = row - halfRow;
                    if (dx * dx + dy * dy > radius * radius) continue;

                    dies.Add(new DieData
                    {
                        Row = row,
                        Col = col,
                        PositionX = CenterX + (col - halfCol) * (DieSizeX + GapX),
                        PositionY = CenterY - (row - halfRow) * (DieSizeY + GapY),
                        DieBrush = DefaultDieBrush,
                        Information = "Ready"
                    });
                }
            }

            DieList = dies;
            SelectedDie = null;
            HasDieSelected = false;
        }

        public void SelectDie(DieData die)
        {
            if (die == null) return;
            SelectedDie = die;
            HasDieSelected = true;
        }

        [RelayCommand]
        private async Task Bonding()
        {
            if (SelectedDie == null) return;

            IsBonding = true;
            SelectedDie.Information = "Bonding...";
            OnPropertyChanged(nameof(SelectedDie));

            _logger.Information("Wafer Bonding 시작 — Die({Row},{Col})",
                SelectedDie.Row, SelectedDie.Col);

            await StepSeqTab.TopRunFullSequence();

            if (StepSeqTab.TopBondingState == StepState.Completed)
            {
                SelectedDie.DieBrush = BondedDieBrush;
                SelectedDie.Information = "Bonded";
            }
            else
            {
                SelectedDie.Information = "Failed";
            }

            OnPropertyChanged(nameof(SelectedDie));
            DieList = new List<DieData>(DieList);
            IsBonding = false;
        }

        [RelayCommand]
        private async Task CancelBonding()
        {
            if (!IsBonding) return;
            await StepSeqTab.Stop();
            _logger.Information("Wafer Bonding 취소");
        }

        // ═══════════════════════════════════════════════════════════════
        //  Wafer Center / Theta 보정
        //  (MDS AccuracyPageViewModel.WaferAlign 알고리즘 이식)
        //  - Center 보정 : 웨이퍼 중심 마크를 카메라 중심에 정렬
        //  - Theta 보정  : 좌/우 마크의 위치 차이(atan2)로 웨이퍼 기울기를
        //                  구해 W_T(웨이퍼 테타축)를 회전. 2회 반복(coarse→fine).
        // ═══════════════════════════════════════════════════════════════

        #region Wafer Align 설정 (필요 시 UI 바인딩/레시피 연동 가능)

        // 웨이퍼 마크를 촬상하는 카메라 / 마크 / 방향
        private const CameraType WaferCamera = CameraType.HC1_HIGH;
        private const MarkType WaferMarkType = MarkType.ALIGN_MARK;
        private const DirectType WaferDirect = DirectType.LEFT;

        // 사용 축
        private const string XAxis = MotionExtensions.H_X;   // 스테이지 X
        private const string YAxis = MotionExtensions.W_Y;   // 웨이퍼 테이블 Y
        private const string ThetaAxis = MotionExtensions.W_T; // 웨이퍼 테타

        // 티칭 위치명 (MotionConstants 참조)
        private const string CenterPosName = MotionExtensions.WAFER_CENTER_POSITION; // "PLACE_CENTER"
        private const string LeftPosName = MotionExtensions.WAFER_LEFT_POSITION;    // "WAFER_LEFT"
        private const string RightPosName = MotionExtensions.WAFER_RIGHT_POSITION;  // "WAFER_RIGHT"

        // W_T 회전 부호. 하드웨어 회전 방향과 부호가 반대면 -1 ↔ +1 로 뒤집는다.
        private const double ThetaSign = -1.0;

        // Center 보정: 잔차가 이 값(µm) 이하가 될 때까지 반복(최대 CenterMaxIter회)
        [ObservableProperty] private double centerToleranceUm = 3.0;
        [ObservableProperty] private int centerMaxIter = 3;

        // Theta 보정: 반복 횟수(1차 coarse, 2차 fine)와 이동 스킵 임계각(deg)
        [ObservableProperty] private int thetaIterations = 2;
        [ObservableProperty] private double thetaMinDeg = 0.0005;

        // 이동/촬상 사이 진동 안정화 대기(ms)
        [ObservableProperty] private int settleDelayMs = 300;

        #endregion

        #region Wafer Align 상태(UI 바인딩용)

        [ObservableProperty] private bool isAligning;
        [ObservableProperty] private string alignStatus = "-";
        [ObservableProperty] private double lastResidualXUm;
        [ObservableProperty] private double lastResidualYUm;
        [ObservableProperty] private double lastThetaDeg;

        private CancellationTokenSource? _alignCts;

        private void OnInterlockActivated()
        {
            try { _alignCts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        [RelayCommand]
        private void StopAlign()
        {
            _alignCts?.Cancel();
            AlignStatus = "중지 요청됨...";
        }

        #endregion

        /// <summary>
        /// 현재 FOV의 마크를 촬상하여 카메라 중심으로 정렬(센터링)하고,
        /// 정렬 후 스테이지 피드백 위치와 정렬 전 잔차(mm)를 반환한다.
        /// (MDS VisionCorrectAndCapturePosAsync 이식)
        /// </summary>
        /// <returns>ok=측정 성공 여부, stageX/stageY=정렬 후 (H_X,W_Y) 위치,
        /// resX/resY=정렬 전 마크 offset(mm, =잔차)</returns>
        private async Task<(bool ok, double stageX, double stageY, double resX, double resY)>
            MeasureAndCenterAsync(CancellationToken ct)
        {
            // 진동 안정화 대기
            await Task.Delay(SettleDelayMs, ct);

            // AF + 마크 위치 측정
            await _communication.RequestAFStart(WaferCamera, WaferMarkType, ct);
            var mark = await _communication.RequestVisionMarkPosition(
                WaferMarkType, WaferCamera, WaferDirect.ToString());

            if (mark == null || mark.Result == Result.NG)
            {
                double sx0 = await _sequenceService.GetCurrentPosition(XAxis, ct);
                double sy0 = await _sequenceService.GetCurrentPosition(YAxis, ct);
                return (false, sx0, sy0, 0, 0);
            }

            double resX = mark.X;
            double resY = mark.Y;

            // 센터링 이동 — HC 카메라 규약(VisionTabViewModel): H_X -= X, W_Y -= Y
            await Task.WhenAll(
                _sequenceService.RelativeMotionsMove(XAxis, -resX, ct),
                _sequenceService.RelativeMotionsMove(YAxis, -resY, ct));

            await Task.Delay(100, ct);

            double sx = await _sequenceService.GetCurrentPosition(XAxis, ct);
            double sy = await _sequenceService.GetCurrentPosition(YAxis, ct);
            return (true, sx, sy, resX, resY);
        }

        /// <summary>
        /// 한 위치에서 잔차가 허용치 이하가 될 때까지 센터링을 반복한다.
        /// </summary>
        private async Task<(bool ok, double stageX, double stageY, double resX, double resY)>
            CenterAtPositionAsync(string positionName, CancellationToken ct)
        {
            // 티칭 위치로 이동 (X,Y 동시)
            await Task.WhenAll(
                _sequenceService.MotionsMove(XAxis, positionName, ct),
                _sequenceService.MotionsMove(YAxis, positionName, ct));

            double tolMm = CenterToleranceUm / 1000.0;
            (bool ok, double sx, double sy, double rx, double ry) last = (false, 0, 0, 0, 0);

            for (int i = 0; i < Math.Max(1, CenterMaxIter); i++)
            {
                ct.ThrowIfCancellationRequested();
                last = await MeasureAndCenterAsync(ct);
                if (!last.ok) break;

                double dist = Math.Sqrt(last.rx * last.rx + last.ry * last.ry);
                LastResidualXUm = last.rx * 1000.0;
                LastResidualYUm = last.ry * 1000.0;

                if (dist <= tolMm) break;
            }

            return last;
        }

        // ── Center 보정 단독 ──────────────────────────────────────────
        [RelayCommand]
        public async Task CenterCorrection()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;
            try
            {
                AlignStatus = "Center 보정 중...";
                var r = await CenterAtPositionAsync(CenterPosName, ct);
                if (!r.ok)
                {
                    AlignStatus = "비전 검출 실패 — 마크가 FOV 내에 없습니다.";
                    _logger.Warning("Center 보정 실패: 비전 검출 NG");
                    return;
                }

                AlignStatus = $"Center 보정 완료 — 잔차 X:{r.resX * 1000:F1}μm, Y:{r.resY * 1000:F1}μm";
                _logger.Information("Center 보정 완료 | Stage=({X:F4},{Y:F4}), 잔차=({RX:F4},{RY:F4})mm",
                    r.stageX, r.stageY, r.resX, r.resY);
            }
            catch (OperationCanceledException) { AlignStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Center 보정 오류");
                AlignStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        // ── Theta 보정 단독 ───────────────────────────────────────────
        [RelayCommand]
        public async Task ThetaCorrection()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;
            try
            {
                double total = await RunThetaAsync(ct);
                AlignStatus = $"Theta 보정 완료 — 총 {total:F4}°";
            }
            catch (OperationCanceledException) { AlignStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Theta 보정 오류");
                AlignStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        /// <summary>
        /// 좌/우 웨이퍼 마크를 센터링한 뒤 두 점의 기울기(atan2)로 W_T를 회전.
        /// ThetaIterations회 반복(1차 coarse, 2차 fine)한다.
        /// (MDS WaferAlignAsync의 2단계 atan2 보정 이식)
        /// </summary>
        /// <returns>적용한 총 보정각(deg)</returns>
        private async Task<double> RunThetaAsync(CancellationToken ct)
        {
            double totalCorr = 0;

            for (int iter = 1; iter <= Math.Max(1, ThetaIterations); iter++)
            {
                ct.ThrowIfCancellationRequested();

                // 좌측 마크 센터링
                AlignStatus = $"[{iter}차] 좌측 마크 측정 중...";
                var l = await CenterAtPositionAsync(LeftPosName, ct);
                if (!l.ok) throw new Exception($"[{iter}차] 좌측 마크 비전 검출 실패");

                // 우측 마크 센터링
                AlignStatus = $"[{iter}차] 우측 마크 측정 중...";
                var r = await CenterAtPositionAsync(RightPosName, ct);
                if (!r.ok) throw new Exception($"[{iter}차] 우측 마크 비전 검출 실패");

                // 기울기 = atan2(ΔY, ΔX). 우측이 +X 이므로 ΔX>0.
                double dx = r.stageX - l.stageX;
                double dy = r.stageY - l.stageY;
                double angleDeg = Math.Atan2(dy, dx) * (180.0 / Math.PI);

                // 기준 벡터(+X) 대비 기울기로 정규화(±90° 범위로 접기)
                if (angleDeg > 90) angleDeg -= 180;
                else if (angleDeg < -90) angleDeg += 180;

                double corr = ThetaSign * angleDeg; // 기울기를 0으로 만드는 반대방향 회전
                LastThetaDeg = angleDeg;

                _logger.Information(
                    "[{Iter}차] Theta | L=({LX:F4},{LY:F4}) R=({RX:F4},{RY:F4}) 기울기={A:F4}° 보정={C:F4}°",
                    iter, l.stageX, l.stageY, r.stageX, r.stageY, angleDeg, corr);

                if (Math.Abs(corr) < ThetaMinDeg)
                {
                    AlignStatus = $"[{iter}차] 보정 불필요 (기울기 {angleDeg:F4}°)";
                    break; // 이미 충분히 수평 → 종료
                }

                AlignStatus = $"[{iter}차] W_T {corr:F4}° 회전 중...";
                await _sequenceService.RelativeMotionsMove(ThetaAxis, corr, ct);
                totalCorr += corr;
            }

            // 정렬 후 웨이퍼 센터로 복귀
            AlignStatus = "Theta 보정 완료 — Center 복귀 중";
            await Task.WhenAll(
                _sequenceService.MotionsMove(XAxis, CenterPosName, ct),
                _sequenceService.MotionsMove(YAxis, CenterPosName, ct));

            return totalCorr;
        }

        // ── Center + Theta 통합 (Wafer Align) ─────────────────────────
        [RelayCommand]
        public async Task WaferAlign()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;
            try
            {
                _logger.Information("Wafer Align 시작");

                // 1) Theta 보정 (좌/우 마크 → W_T 회전, 2회 반복)
                double totalTheta = await RunThetaAsync(ct);

                // 2) 회전 후 Center 재보정 (회전으로 인한 중심 이동 보정)
                AlignStatus = "Center 재보정 중...";
                var c = await CenterAtPositionAsync(CenterPosName, ct);
                if (!c.ok) throw new Exception("Center 마크 비전 검출 실패");

                AlignStatus = $"Wafer Align 완료 — Theta {totalTheta:F4}°, " +
                              $"Center 잔차 ({c.resX * 1000:F1}, {c.resY * 1000:F1})μm";

                WriteAlignLog(totalTheta, c.resX, c.resY);
                _logger.Information("Wafer Align 완료 | Theta 총 {T:F4}°, Center 잔차=({RX:F4},{RY:F4})mm",
                    totalTheta, c.resX, c.resY);
            }
            catch (OperationCanceledException) { AlignStatus = "Wafer Align 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Wafer Align 오류");
                AlignStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        private void WriteAlignLog(double totalTheta, double resX, double resY)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "정밀도 데이터");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "WaferAlign.log");

                var sb = new StringBuilder();
                sb.AppendLine($"[Wafer Align] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  Theta 총 보정 : {totalTheta:F4}°");
                sb.AppendLine($"  Center 잔차   : X={resX * 1000:F2}μm, Y={resY * 1000:F2}μm");
                sb.AppendLine("─────────────────────────────────────");

                File.AppendAllText(path, sb.ToString());
            }
            catch (Exception e)
            {
                _logger.Warning(e, "WaferAlign 로그 저장 실패");
            }
        }
    }
}
