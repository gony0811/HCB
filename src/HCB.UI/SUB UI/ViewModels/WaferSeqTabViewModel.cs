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

        // 비전/모션/파라미터 서비스 (CalibrationTabViewModel과 동일한 주입 패턴)
        private readonly SequenceService _sequenceService;
        private readonly EqpCommunicationService _communication;
        private readonly ECParamService _ecParamService;

        [ObservableProperty] private bool isBonding;

        public WaferSeqTabViewModel(
            RecipeService recipeService,
            SettingsViewModel settingsViewModel,
            StepSeqTabViewModel stepSeqTabViewModel,
            SequenceService sequenceService,
            EqpCommunicationService communication,
            ECParamService ecParamService,
            ILogger logger)
        {
            RecipeService = recipeService;
            Settings = settingsViewModel;
            StepSeqTab = stepSeqTabViewModel;
            _sequenceService = sequenceService;
            _communication = communication;
            _ecParamService = ecParamService;
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
        //  Wafer Center Align (엣지 3점 원 피팅)
        //
        //  절차:
        //   1) 사용자가 W-Table 척 중심에 웨이퍼를 중심/기울기 ~0으로 로딩.
        //   2) 레시피에서 웨이퍼 직경을 읽어 엣지 3점 위치를 계산하고
        //      저배율(HC_LOW) 카메라로 각 엣지를 측정.
        //   3) 측정 Motion+Vision으로 엣지 절대좌표 3점 → 원 피팅으로 중심 계산.
        //   4) (저배율 카메라 ↔ 샹크) 간격(ShankLowOffsetX/Y)만큼 시프트해
        //      샹크 중심이 웨이퍼 중심에 오도록 이동.  (참고: TopLowMeasure / DTablePickup)
        //   5) HC1/HC2 고배율로 AlignMark 촬상.
        //   6) 둘 다 실패 → "재로딩 필요" 알림(수동).
        //   6.1) 한쪽만 성공 → 둘 다 보이는 위치로 X,Y 조정 후 재촬상.
        //   6.2) 반복 조정에도 실패 → "재로딩 필요" 알림(수동).
        // ═══════════════════════════════════════════════════════════════

        #region 설정 (필요 시 UI 바인딩/레시피 연동 가능)

        // 저배율 엣지 검출
        private const CameraType LowCam = CameraType.HC_LOW;
        private const MarkType EdgeMark = MarkType.WAFER_EDGE;
        private const string LowVisionZPos = MotionExtensions.WAFER_ALIGN_LOW; // HC_LOW 촬상용 H_Z 위치

        // 고배율 AlignMark 검출
        private const CameraType Hc1Cam = CameraType.HC1_HIGH;
        private const CameraType Hc2Cam = CameraType.HC2_HIGH;
        private const MarkType AlignMark = MarkType.ALIGN_MARK;

        // 사용 축
        private const string XAxis = MotionExtensions.H_X;   // 스테이지 X
        private const string YAxis = MotionExtensions.W_Y;   // 웨이퍼 테이블 Y
        private const string ThetaAxis = MotionExtensions.W_T; // 웨이퍼 테타

        private const string CenterPosName = MotionExtensions.WAFER_CENTER_POSITION; // "PLACE_CENTER"

        // 엣지 3점 각도(도). 원 피팅은 120° 간격이 조건수가 좋다.
        // (Notch/Flat 위치와 겹치지 않도록 필요 시 조정)
        private static readonly double[] EdgeAnglesDeg = { 90.0, 210.0, 330.0 };

        // 저배율 재시도, AlignMark 조정 반복 한계
        [ObservableProperty] private int lowVisionRetryMax = 3;
        [ObservableProperty] private int alignAdjustMaxIter = 3;

        // 이동/촬상 사이 진동 안정화 대기(ms)
        [ObservableProperty] private int settleDelayMs = 300;

        // 센터 후 Theta 보정 적용 여부 (HC1/HC2 AlignMark 기울기로 W_T 회전)
        [ObservableProperty] private bool applyThetaAfterCenter = true;
        // W_T 회전 부호 (하드웨어 방향과 반대면 +1로 뒤집기), 스킵 임계각(deg)
        private const double ThetaSign = -1.0;
        [ObservableProperty] private double thetaMinDeg = 0.0005;

        #endregion

        #region 상태(UI 바인딩용)

        [ObservableProperty] private bool isAligning;
        [ObservableProperty] private string alignStatus = "-";
        [ObservableProperty] private bool reloadRequired;      // 재로딩 필요 플래그(수동 처리)
        [ObservableProperty] private double waferCenterX;      // 계산된 웨이퍼 중심 (스테이지 좌표계)
        [ObservableProperty] private double waferCenterY;
        [ObservableProperty] private double loadingErrorXUm;   // 척 중심 대비 로딩 편차
        [ObservableProperty] private double loadingErrorYUm;
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

        // ── 메인 커맨드 ───────────────────────────────────────────────
        [RelayCommand]
        public async Task WaferCenterAlign()
        {
            if (IsAligning) return;
            IsAligning = true;
            ReloadRequired = false;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                _logger.Information("Wafer Center Align 시작");

                // ── 2) 웨이퍼 직경 → 반경 (레시피 WaferSize = mm 직경) ──
                double diameter = RecipeService.FindByParamDouble("WaferSize");
                double radius = diameter / 2.0;
                if (radius <= 0)
                {
                    AlignStatus = "레시피 WaferSize(직경 mm)가 유효하지 않습니다.";
                    return;
                }

                // ── 저배율 촬상 높이로 H_Z 이동 후 척 중심(PLACE_CENTER)으로 XY 이동 ──
                AlignStatus = "저배율 촬상 위치 이동 중...";
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, LowVisionZPos, ct);
                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, CenterPosName, ct),
                    _sequenceService.MotionsMove(YAxis, CenterPosName, ct));

                double cHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
                double cWY = await _sequenceService.GetCurrentPosition(YAxis, ct);

                // ── 2~3) 엣지 3점 측정 → 원 피팅 ──
                var edgePoints = new List<Point2D>();
                for (int i = 0; i < EdgeAnglesDeg.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    AlignStatus = $"엣지 측정 {i + 1}/{EdgeAnglesDeg.Length} ({EdgeAnglesDeg[i]:F0}°)...";
                    var pt = await MeasureWaferEdgeAsync(cHX, cWY, radius, EdgeAnglesDeg[i], ct);
                    if (pt == null)
                    {
                        ReloadRequired = true;
                        AlignStatus = $"엣지 {i + 1} 검출 실패 — 재로딩이 필요합니다.";
                        _logger.Warning("Wafer 엣지 {Idx} 검출 실패 — 재로딩 필요", i + 1);
                        return;
                    }
                    edgePoints.Add(pt);
                }

                var center = CalibrationMath.FitCircleCenter(edgePoints);
                WaferCenterX = center.X;
                WaferCenterY = center.Y;
                LoadingErrorXUm = (center.X - cHX) * 1000.0;
                LoadingErrorYUm = (center.Y - cWY) * 1000.0;
                _logger.Information(
                    "Wafer 중심(원 피팅)=({CX:F4},{CY:F4}), 로딩편차=({EX:F1},{EY:F1})μm",
                    center.X, center.Y, LoadingErrorXUm, LoadingErrorYUm);

                // ── 4) 샹크 중심을 웨이퍼 중심으로: (저배율↔샹크) 오프셋만큼 시프트 ──
                //     DTablePickup 참고: 샹크가 측정 마크로 오도록 (ShankLowOffset - 측정오차) 이동.
                //     여기선 절대 목표 = 웨이퍼중심 + ShankLowOffset.
                double shankLowX = _ecParamService.GetDouble("ShankLowOffsetX");
                double shankLowY = _ecParamService.GetDouble("ShankLowOffsetY");
                double targetHX = center.X + shankLowX;
                double targetWY = center.Y + shankLowY;

                AlignStatus = "샹크 중심을 웨이퍼 중심으로 이동 중...";
                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, targetHX, ct),
                    _sequenceService.MotionsMove(YAxis, targetWY, ct));

                // ── 고배율(HC1/HC2) 촬상 높이로 H_Z 이동 (FiducialAngleTracking과 동일 Z) ──
                double shankToWafer = _ecParamService.GetDouble("ShankToWaferOffset");
                double topDieThickness = RecipeService.FindByParamDouble("TopDieThickness");
                double btmDieThickness = RecipeService.FindByParamDouble("BtmDieThickness");
                double zHigh = shankToWafer - topDieThickness - btmDieThickness - 0.1;
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, zHigh, ct);

                // ── 5~6) HC1/HC2 AlignMark 촬상 + 실패 처리 ──
                bool ok = await VerifyAlignMarksAsync(ct);
                if (!ok)
                {
                    // VerifyAlignMarksAsync가 상태/ReloadRequired 설정 완료
                    return;
                }

                AlignStatus = ReloadRequired
                    ? AlignStatus
                    : $"Wafer Center Align 완료 — 중심=({center.X:F4},{center.Y:F4}), " +
                      $"로딩편차=({LoadingErrorXUm:F1},{LoadingErrorYUm:F1})μm" +
                      (ApplyThetaAfterCenter ? $", Theta={LastThetaDeg:F4}°" : "");

                WriteAlignLog(center, LoadingErrorXUm, LoadingErrorYUm, LastThetaDeg);
                _logger.Information("Wafer Center Align 완료");
            }
            catch (OperationCanceledException) { AlignStatus = "Wafer Center Align 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Wafer Center Align 오류");
                AlignStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        /// <summary>
        /// 척 중심(cHX,cWY) 기준 반경 radius, 각도 angleDeg 위치로 저배율 카메라를 이동해
        /// 웨이퍼 엣지를 측정하고, 엣지의 절대 좌표(스테이지 좌표계)를 반환한다.
        /// 실패(재시도 소진) 시 null.
        /// </summary>
        private async Task<Point2D?> MeasureWaferEdgeAsync(
            double cHX, double cWY, double radius, double angleDeg, CancellationToken ct)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double tHX = cHX + radius * Math.Cos(rad);
            double tWY = cWY + radius * Math.Sin(rad);

            await Task.WhenAll(
                _sequenceService.MotionsMove(XAxis, tHX, ct),
                _sequenceService.MotionsMove(YAxis, tWY, ct));
            await Task.Delay(SettleDelayMs, ct);

            for (int attempt = 0; attempt <= Math.Max(0, LowVisionRetryMax); attempt++)
            {
                ct.ThrowIfCancellationRequested();
                // 저배율은 방향 인자 없이 호출 (BtmLowMeasure와 동일 규약)
                var r = await _communication.RequestVisionMarkPosition(EdgeMark, LowCam, "");
                if (r != null && r.Result != Result.NG)
                {
                    // 엣지 절대좌표 = 현재 스테이지 위치 − 카메라→마크 오프셋
                    double curHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
                    double curWY = await _sequenceService.GetCurrentPosition(YAxis, ct);
                    return Point2D.of(curHX - r.X, curWY - r.Y);
                }
                if (attempt < LowVisionRetryMax)
                    _logger.Warning("엣지 저배율 측정 실패 — 재시도 {A}/{M}", attempt + 1, LowVisionRetryMax);
            }
            return null;
        }

        /// <summary>
        /// HC1/HC2 고배율로 AlignMark를 촬상한다.
        /// 둘 다 실패 → 재로딩 필요. 한쪽만 성공 → 성공 카메라 오프셋으로 X,Y를 조정해
        /// 둘 다 보이는 위치로 이동 후 재촬상(최대 AlignAdjustMaxIter회). 계속 실패 → 재로딩 필요.
        /// 성공 시 (옵션) 두 마크 기울기로 W_T Theta 보정.
        /// </summary>
        private async Task<bool> VerifyAlignMarksAsync(CancellationToken ct)
        {
            for (int iter = 0; iter <= Math.Max(0, AlignAdjustMaxIter); iter++)
            {
                ct.ThrowIfCancellationRequested();
                AlignStatus = iter == 0
                    ? "HC1/HC2 AlignMark 촬상 중..."
                    : $"AlignMark 위치 조정 후 재촬상 {iter}/{AlignAdjustMaxIter}...";

                var h1 = await MeasureAlignAsync(Hc1Cam, DirectType.LEFT, ct);
                var h2 = await MeasureAlignAsync(Hc2Cam, DirectType.RIGHT, ct);
                bool ok1 = h1 != null && h1.Result != Result.NG;
                bool ok2 = h2 != null && h2.Result != Result.NG;

                if (ok1 && ok2)
                {
                    if (ApplyThetaAfterCenter)
                        await ApplyThetaFromAlignMarksAsync(h1!, h2!, ct);
                    AlignStatus = "AlignMark 양쪽 검출 완료";
                    return true;
                }

                // 6) 둘 다 실패 → 재로딩
                if (!ok1 && !ok2)
                {
                    ReloadRequired = true;
                    AlignStatus = "AlignMark 양쪽 검출 실패 — 재로딩이 필요합니다.";
                    _logger.Warning("AlignMark 양쪽 검출 실패 — 재로딩 필요");
                    return false;
                }

                // 6.1) 한쪽만 성공 → 성공한 카메라의 마크를 중심으로 가져와 둘 다 보이게 조정
                if (iter >= AlignAdjustMaxIter) break; // 다음 루프 없으면 조정 생략

                var okMark = ok1 ? h1! : h2!;
                _logger.Information("AlignMark 한쪽만 검출({Cam}) — 조정 이동 ΔX={X:F4},ΔY={Y:F4}",
                    ok1 ? "HC1" : "HC2", -okMark.X, -okMark.Y);
                await Task.WhenAll(
                    _sequenceService.RelativeMotionsMove(XAxis, -okMark.X, ct),
                    _sequenceService.RelativeMotionsMove(YAxis, -okMark.Y, ct));
                await Task.Delay(SettleDelayMs, ct);
            }

            // 6.2) 반복 조정에도 실패
            ReloadRequired = true;
            AlignStatus = "AlignMark 반복 조정 실패 — 재로딩이 필요합니다.";
            _logger.Warning("AlignMark 반복 조정 실패 — 재로딩 필요");
            return false;
        }

        private async Task<VisionMarkPositionResponse?> MeasureAlignAsync(
            CameraType cam, DirectType direct, CancellationToken ct)
        {
            try
            {
                await _communication.RequestAFStart(cam, AlignMark, ct);
                return await _communication.RequestVisionMarkPosition(AlignMark, cam, direct.ToString());
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Warning(e, "{Cam} AlignMark 촬상 예외", cam);
                return null;
            }
        }

        /// <summary>
        /// HC1(Left)/HC2(Right) AlignMark 두 점의 기울기(atan2)로 W_T를 회전해 웨이퍼 Theta를 보정.
        /// (MainSequence.FiducialAngleTracking의 Hc 각도 산출 규약을 따름: Hc2Offset=HC2_X/HC2_Y 적용)
        /// </summary>
        private async Task ApplyThetaFromAlignMarksAsync(
            VisionMarkPositionResponse h1, VisionMarkPositionResponse h2, CancellationToken ct)
        {
            double hc2OffX = _ecParamService.GetDouble(MotionExtensions.HC2_X);
            double hc2OffY = _ecParamService.GetDouble(MotionExtensions.HC2_Y);

            var left = Point2D.of(-h1.X, -h1.Y);
            var right = Point2D.of(hc2OffX - h2.X, hc2OffY - h2.Y);

            double angleDeg = Math.Atan2(right.Y - left.Y, right.X - left.X) * (180.0 / Math.PI);
            // 두 마크를 잇는 선의 수평 대비 기울기로 정규화(±90° 접기)
            if (angleDeg > 90) angleDeg -= 180;
            else if (angleDeg < -90) angleDeg += 180;

            LastThetaDeg = angleDeg;
            double corr = ThetaSign * angleDeg;
            if (Math.Abs(corr) < ThetaMinDeg)
            {
                _logger.Information("Theta 보정 불필요 (기울기 {A:F4}°)", angleDeg);
                return;
            }

            AlignStatus = $"Theta 보정: W_T {corr:F4}° 회전 중...";
            await _sequenceService.RelativeMotionsMove(ThetaAxis, corr, ct);
            _logger.Information("Theta 보정 적용 — 기울기={A:F4}°, W_T 회전={C:F4}°", angleDeg, corr);
        }

        private void WriteAlignLog(Point2D center, double errXUm, double errYUm, double thetaDeg)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "정밀도 데이터");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "WaferCenterAlign.log");

                var sb = new StringBuilder();
                sb.AppendLine($"[Wafer Center Align] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  중심(스테이지) : X={center.X:F4}, Y={center.Y:F4}");
                sb.AppendLine($"  로딩 편차      : X={errXUm:F2}μm, Y={errYUm:F2}μm");
                if (ApplyThetaAfterCenter)
                    sb.AppendLine($"  Theta          : {thetaDeg:F4}°");
                sb.AppendLine("─────────────────────────────────────");

                File.AppendAllText(path, sb.ToString());
            }
            catch (Exception e)
            {
                _logger.Warning(e, "WaferCenterAlign 로그 저장 실패");
            }
        }
    }
}
