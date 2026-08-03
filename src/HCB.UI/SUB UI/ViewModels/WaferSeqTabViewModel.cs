using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Telerik.Windows.Controls;
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

        public SettingsViewModel Settings { get; }      // SettingsSidebar가 {Binding Settings.*}로 사용
        public StepSeqTabViewModel StepSeqTab { get; }

        private readonly ILogger _logger;

        // 비전/모션/파라미터 서비스 (CalibrationTabViewModel과 동일한 주입 패턴)
        private readonly SequenceService _sequenceService;
        private readonly EqpCommunicationService _communication;
        private readonly ECParamService _ecParamService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
        private bool isBonding;

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

            // Interlock 발생 시 진행 중인 측정/시프트를 즉시 취소
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
            try { ScribeShiftX = RecipeService.FindByParamDouble("ScribeShiftX"); } catch { }
            try { ScribeShiftY = RecipeService.FindByParamDouble("ScribeShiftY"); } catch { }
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
            await SaveParam("ScribeShiftX", ScribeShiftX.ToString(), ValueType.Double, UnitType.mm);
            await SaveParam("ScribeShiftY", ScribeShiftY.ToString(), ValueType.Double, UnitType.mm);

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
        //  Wafer 중심 찾기 (Scribeline 기반) — 총 3단계
        //   1차: 저배율 3점 측정으로 대략 중심            (측정 방법 미정 → 스킵)
        //   2차: 저배율(HC_LOW) 카메라로 Scribeline(교차점) 측정
        //   3차: (2차 측정 오프셋 + 저배율카메라↔Shank 상대거리)만큼 Shank를 중심으로 시프트
        //
        //  ※ 테스트 전제: 저배율 카메라가 이미 Scribeline 위에 위치(Z=저배율 촬상 높이)해 있다고 가정.
        //     1차는 무시하고 2차 측정 → 3차 시프트만으로 동작을 확인한다.
        // ═══════════════════════════════════════════════════════════════

        #region Wafer 중심 찾기 (Scribeline 기반)

        private const CameraType LowCam = CameraType.HC_LOW; // 저배율 카메라
        private const string XAxis = MotionExtensions.H_X;   // 스테이지 X
        private const string YAxis = MotionExtensions.W_Y;   // 웨이퍼 테이블 Y
        private const string TAxis = MotionExtensions.W_T;   // 웨이퍼 테이블 Y

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
        private bool isAligning;          // 측정/시프트 진행 중 busy 플래그
        [ObservableProperty] private string scribeCenterStatus = "-";
        [ObservableProperty] private double scribeOffsetXUm;   // 카메라 중심 → 스크라이브 교차점 오프셋(μm)
        [ObservableProperty] private double scribeOffsetYUm;
        [ObservableProperty] private double scribeAbsX;        // 스크라이브 교차점 절대(스테이지) 좌표
        [ObservableProperty] private double scribeAbsY;
        [ObservableProperty] private double scribeAbsT;
        [ObservableProperty] private bool hasScribeMeasure;    // 2차 측정 완료 여부(3차 활성화용)

        // ── 1차: 저배율 3점(12/4/7시) 웨이퍼 엣지 절대좌표 ──
        //  operator가 저배율 카메라를 각 시계 위치로 조그한 뒤 해당 버튼으로 1점씩 측정.
        //  3점이 모두 채워지면 FindCenterStep1이 원 피팅으로 대략 중심을 산출·이동한다.
        private readonly Point2D?[] _edgePoints = new Point2D?[3]; // [0]=12시, [1]=4시, [2]=7시
        [ObservableProperty] private string edgePoint12Text = "-";
        [ObservableProperty] private string edgePoint04Text = "-";
        [ObservableProperty] private string edgePoint07Text = "-";
        [ObservableProperty] private double coarseCenterX;     // 1차 원 피팅 대략 중심(스테이지 절대)
        [ObservableProperty] private double coarseCenterY;

        private int EdgeMeasuredCount => _edgePoints.Count(p => p != null);

        // ── 2차: Scribeline 기반 중심/Theta 정렬 설정 ──
        //  ScribeShiftX/Y : (Recipe) 대략 중심에서 기준 Scribe 측정 위치까지의 초기 Shift(mm)
        //  ScribeSweepDies: (사용자) 기준 위치에서 양옆으로 스윕할 Die 수(편도)
        //  ScribeStepDies : 스텝당 이동 Die 수(피치 배수)
        //  ScribeConvergeIter: 한 Die에서 Theta 수렴까지 재측정 반복 상한(3~6단계 반복)
        //  ScribeThetaMinDeg : 수렴 임계각(°, 미만이면 보정 생략)
        [ObservableProperty] private double scribeShiftX;      // (Recipe) 초기 Shift X
        [ObservableProperty] private double scribeShiftY;      // (Recipe) 초기 Shift Y
        [ObservableProperty] private int scribeSweepDies = 2;  // 편도 스윕 Die 수
        [ObservableProperty] private int scribeStepDies = 1;   // 스텝당 이동 Die 수
        [ObservableProperty] private int scribeConvergeIter = 3; // Die당 Theta 수렴 반복 상한
        [ObservableProperty] private double scribeThetaMinDeg = 0.01; // 수렴 임계각(°)
        [ObservableProperty] private double scribeThetaAngleDeg;      // 마지막 측정 기울기(°)

        private CancellationTokenSource? _alignCts;

        private void OnInterlockActivated()
        {
            try { _alignCts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// 진행 중인 Wafer 동작(중심 찾기 · Theta 보정 · 본딩)을 즉시 취소한다.
        /// _alignCts 기반 시퀀스(FindCenterStep2/3, ThetaCorrection)는 토큰 취소로,
        /// 본딩은 StepSeq 정지로 중단한다.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCancelOperation))]
        private async Task CancelOperation()
        {
            _logger.Information("Wafer 동작 취소 요청");

            try { _alignCts?.Cancel(); }
            catch (ObjectDisposedException) { }

            if (IsBonding)
                await StepSeqTab.Stop();
        }

        private bool CanCancelOperation() => IsAligning || IsBonding;

        // ── 1차: 저배율(HC_LOW) 3점(12/4/7시) 웨이퍼 엣지로 대략 중심 산출 ──
        //  Vision v1.0 회신: 저배율은 화소당 ~45µm라 스크라이브 신뢰도가 낮으므로,
        //  대략 중심은 웨이퍼 "엣지(원호)"를 3점 잡아 최소자승 원 피팅으로 구한다.
        //  저배율 FOV(≈110mm)에 3점 동시 촬상이 불가하므로, operator가 카메라를
        //  12→4→7시(≈120° 간격)로 조그해 각 위치에서 아래 버튼으로 1점씩 측정한다.

        /// <summary>12/4/7시 시계 위치 → _edgePoints 인덱스.</summary>
        private static int EdgeIndex(WaferClock clock) => clock switch
        {
            WaferClock.H12 => 0,
            WaferClock.H04 => 1,
            WaferClock.H07 => 2,
            _ => 0
        };

        private void SetEdgeText(int idx, string text)
        {
            switch (idx)
            {
                case 0: EdgePoint12Text = text; break;
                case 1: EdgePoint04Text = text; break;
                case 2: EdgePoint07Text = text; break;
            }
        }

        // ── 1차-측정: 현재 위치에서 저배율 웨이퍼 엣지 1점 측정(12/4/7시 개별 버튼) ──
        //  카메라가 해당 시계 위치의 엣지 위에 있다고 가정하고 1회 촬상.
        //  엣지 절대좌표 = 현재 스테이지 − 카메라→엣지 오프셋 (FindCenterStep2와 동일 부호 규약).
        [RelayCommand]
        private async Task MeasureEdgePoint(WaferClock clock)
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                int idx = EdgeIndex(clock);
                ScribeCenterStatus = $"저배율 웨이퍼 엣지 측정 중... ({(int)clock}시)";
                _logger.Information("Wafer 중심 1차 — HC_LOW 엣지 측정 시작 ({Clock}시)", (int)clock);

                var r = await _communication.RequestWaferEdge(clock, ct);
                if (r == null || r.Result == Result.NG)
                {
                    ScribeCenterStatus = $"엣지 측정 실패(NG) — {(int)clock}시";
                    _logger.Warning("HC_LOW 엣지 측정 NG ({Clock}시)", (int)clock);
                    return;
                }

                double curHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
                double curWY = await _sequenceService.GetCurrentPosition(YAxis, ct);

                var pt = Point2D.of(curHX - r.X, curWY - r.Y);
                _edgePoints[idx] = pt;
                SetEdgeText(idx, $"({pt.X:F4}, {pt.Y:F4})");

                ScribeCenterStatus =
                    $"{(int)clock}시 엣지 측정 완료 — ({pt.X:F4},{pt.Y:F4}) [{EdgeMeasuredCount}/3]";
                _logger.Information("HC_LOW 엣지 측정 완료 ({Clock}시) — abs=({X:F4},{Y:F4})",
                    (int)clock, pt.X, pt.Y);
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "엣지 측정 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "웨이퍼 엣지 측정 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        // ── 1차-측정값 초기화 ──
        [RelayCommand]
        private void ResetEdgePoints()
        {
            for (int i = 0; i < _edgePoints.Length; i++) _edgePoints[i] = null;
            EdgePoint12Text = EdgePoint04Text = EdgePoint07Text = "-";
            ScribeCenterStatus = "엣지 3점 초기화됨";
        }

        // ── 1차: 3점 원 피팅 → 대략 중심 산출 후 저배율 카메라를 중심으로 이동 ──
        [RelayCommand]
        private async Task FindCenterStep1()
        {
            if (IsAligning) return;
            if (_edgePoints.Any(p => p == null))
            {
                ScribeCenterStatus = $"3점(12/4/7시) 엣지 측정을 모두 완료하세요. [{EdgeMeasuredCount}/3]";
                return;
            }

            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                var pts = _edgePoints.Select(p => p!).ToList();
                var center = CalibrationMath.FitCircleCenter(pts);

                CoarseCenterX = center.X;
                CoarseCenterY = center.Y;

                ScribeCenterStatus = "1차 대략 중심으로 저배율 카메라 이동 중...";
                _logger.Information("Wafer 중심 1차 — 원 피팅 중심=({X:F4},{Y:F4})",
                    center.X, center.Y);

                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, center.X, ct),
                    _sequenceService.MotionsMove(YAxis, center.Y, ct));

                ScribeCenterStatus = $"1차 완료 — 대략 중심=({center.X:F4},{center.Y:F4})로 이동";
                _logger.Information("Wafer 중심 1차 완료 — 중심 이동 ({X:F4},{Y:F4})", center.X, center.Y);
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "1차 이동 중단됨"; }
            catch (InvalidOperationException e)
            {
                // 3점이 일직선(원 피팅 불가) — 시계 위치를 더 벌려 재측정 안내
                _logger.Warning(e, "Wafer 중심 1차 — 원 피팅 실패(점이 일직선)");
                ScribeCenterStatus = "1차 실패 — 3점이 일직선입니다. 12/4/7시로 더 벌려 재측정하세요.";
            }
            catch (Exception e)
            {
                _logger.Error(e, "Wafer 중심 1차 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        // ── 2차: 저배율(HC_LOW) Scribeline 기반 중심/Theta 정렬 ──
        //  절차(사용자 요청):
        //   (1) Recipe(ScribeShiftX/Y)만큼 H_X·W_Y를 기준 Scribe 위치로 Shift
        //   (2) Scribeline 측정으로 검출 확인
        //   (3) Scribeline을 비전 중심에 정렬(offset→0) 후 측정 = A
        //   (4) DieSize(피치)만큼 H_X를 옆으로 Shift 후 측정 = B
        //   (5) A·B로 Theta 산출 → W_T 보정
        //   (6) (3)~(5)를 임계각 이내로 수렴할 때까지 반복
        //   (7) ThetaCorrection처럼 기준 위치 양옆으로 스윕하며 (3)~(6) 반복
        //  ※ Step3(Shank 시프트)이 저배율카메라↔Shank(ShankLowOffset)를 사용하므로
        //    측정 카메라는 저배율(HC_LOW)로 유지한다. HC_LOW는 피사계 심도가 커서 AF 불필요.
        //    (Vision v1.0: 저배 스크라이브는 검출률이 낮을 수 있음 — 실패 시 조명/모델/끝 Die 확인)
        [RelayCommand]
        private async Task FindCenterStep2()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                double pitch = (DieSizeX + GapX) * Math.Max(1, ScribeStepDies); // 옆 Die까지 거리
                if (pitch <= 0)
                {
                    ScribeCenterStatus = "DieSize/Gap 값이 유효하지 않습니다.";
                    return;
                }

                // (1) Recipe 값만큼 기준 위치로 초기 Shift
                if (Math.Abs(ScribeShiftX) > 1e-9 || Math.Abs(ScribeShiftY) > 1e-9)
                {
                    ScribeCenterStatus = $"기준 위치로 Shift 중... ({ScribeShiftX:F3}, {ScribeShiftY:F3})";
                    _logger.Information("Wafer 중심 2차 — Recipe Shift ({X:F4},{Y:F4})", ScribeShiftX, ScribeShiftY);
                    if (Math.Abs(ScribeShiftX) > 1e-9) await _sequenceService.RelativeMotionsMove(XAxis, ScribeShiftX, ct);
                    if (Math.Abs(ScribeShiftY) > 1e-9) await _sequenceService.RelativeMotionsMove(YAxis, ScribeShiftY, ct);
                }

                // (2)+(3)~(6) 기준 Die에서 Theta 수렴
                ScribeCenterStatus = "기준 Die Scribeline 정렬/Theta 수렴 중...";
                var startAbs = await ConvergeThetaAtDieAsync(pitch, "기준", ct);
                if (startAbs == null)
                {
                    HasScribeMeasure = false;
                    ScribeCenterStatus = "기준 Scribeline 검출 실패(NG) — 위치/조명/모델 확인";
                    _logger.Warning("Wafer 중심 2차 — 기준 Scribeline 미검출");
                    return;
                }
                double startHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
                double startWY = await _sequenceService.GetCurrentPosition(YAxis, ct);

                int steps = Math.Max(0, ScribeSweepDies);

                // (7) 기준 위치 양옆으로 스윕 — 각 패스 시작 전 기준 위치로 복귀
                await RunScribeThetaSweepAsync(+1, pitch, steps, "정방향", ct);
                await _sequenceService.MotionsMove(XAxis, startHX, ct);
                await _sequenceService.MotionsMove(YAxis, startWY, ct);

                await RunScribeThetaSweepAsync(-1, pitch, steps, "역방향", ct);
                await _sequenceService.MotionsMove(XAxis, startHX, ct);
                await _sequenceService.MotionsMove(YAxis, startWY, ct);

                // 최종: 기준 Scribe를 다시 비전 중심에 정렬하고 절대좌표 기록(Step3용)
                var finalAbs = await CenterScribeAsync(ct);
                if (finalAbs == null)
                {
                    HasScribeMeasure = false;
                    ScribeCenterStatus = "정렬 후 기준 Scribeline 재검출 실패(NG)";
                    return;
                }

                ScribeAbsX = finalAbs.X;
                ScribeAbsY = finalAbs.Y;
                ScribeAbsT = await _sequenceService.GetCurrentPosition(TAxis, ct);
                HasScribeMeasure = true;

                ScribeCenterStatus =
                    $"2차 완료 — 교차점=({ScribeAbsX:F4},{ScribeAbsY:F4}), 최종 기울기 {ScribeThetaAngleDeg:F4}°";
                _logger.Information("Wafer 중심 2차 완료 — abs=({AX:F4},{AY:F4}), theta={A:F4}°",
                    ScribeAbsX, ScribeAbsY, ScribeThetaAngleDeg);
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "2차 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Scribeline 정렬 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        /// <summary>저배율(HC_LOW) Scribeline 1회 측정. 검출 실패(NG) 시 null.</summary>
        private async Task<ScribeLineResponse?> MeasureScribeAsync(CancellationToken ct)
        {
            var r = await _communication.RequestScribeLine(LowCam, ct);
            if (r == null || r.Result == Result.NG) return null;
            return r;
        }

        /// <summary>Scribe 1회 측정 후 교차점 절대(스테이지) 좌표 반환. 실패 시 null.</summary>
        private async Task<Point2D?> MeasureScribeAbsAsync(CancellationToken ct)
        {
            var r = await MeasureScribeAsync(ct);
            if (r == null) return null;
            double curHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
            double curWY = await _sequenceService.GetCurrentPosition(YAxis, ct);
            return Point2D.of(curHX - r.X, curWY - r.Y);
        }

        /// <summary>
        /// Scribeline을 비전 중심(offset≈0)에 정렬하고 정렬된 교차점의 절대(스테이지) 좌표를 반환한다.
        /// 매 반복마다 측정 offset(r.X,r.Y)만큼 H_X·W_Y를 역이동(−offset)해 교차점을 카메라 중심으로 끌어온다.
        /// tolMm 이내로 들어오거나 maxIter 소진 시 종료. 검출 실패 시 null.
        /// (절대좌표 = 현재 스테이지 − 카메라→교차점 오프셋, FindCenterStep 공통 규약)
        /// </summary>
        private async Task<Point2D?> CenterScribeAsync(CancellationToken ct, int maxIter = 4, double tolMm = 0.003)
        {
            for (int i = 0; i < maxIter; i++)
            {
                ct.ThrowIfCancellationRequested();
                var r = await MeasureScribeAsync(ct);
                if (r == null) return null;

                ScribeOffsetXUm = r.X * 1000.0;
                ScribeOffsetYUm = r.Y * 1000.0;

                double curHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
                double curWY = await _sequenceService.GetCurrentPosition(YAxis, ct);
                var abs = Point2D.of(curHX - r.X, curWY - r.Y);

                if (Math.Abs(r.X) <= tolMm && Math.Abs(r.Y) <= tolMm)
                    return abs; // 이미 비전 중심

                // 교차점을 카메라 중심으로 이동: 현재 → 절대(=현재−offset)
                await _sequenceService.RelativeMotionsMove(XAxis, -r.X, ct);
                await _sequenceService.RelativeMotionsMove(YAxis, -r.Y, ct);
            }

            return await MeasureScribeAbsAsync(ct); // 마지막 상태의 절대좌표
        }

        /// <summary>
        /// 한 Die에서 (3)~(6)을 수행한다.
        ///  (3) Scribe를 비전 중심에 정렬 = A → (4) 옆 Die로 pitch 이동 후 측정 = B → 기준(A)으로 복귀
        ///  (5) A·B 기울기(atan2)로 W_T 보정 → (6) 임계각 이내로 수렴할 때까지 반복.
        /// 반환: 수렴/정상 종료된 기준점 A의 절대좌표. 초기 정렬 실패 시 null(끝 Die/미검출).
        /// </summary>
        private async Task<Point2D?> ConvergeThetaAtDieAsync(double pitch, string pass, CancellationToken ct)
        {
            int iter = Math.Max(1, ScribeConvergeIter);
            Point2D? aAbs = null;

            for (int k = 0; k < iter; k++)
            {
                ct.ThrowIfCancellationRequested();

                // (3) 기준 Scribe를 비전 중심에 정렬 = A
                aAbs = await CenterScribeAsync(ct);
                if (aAbs == null) return null;

                // (4) 옆 Die로 pitch 이동 후 측정 = B, 이후 기준 위치(A)로 복귀
                await _sequenceService.RelativeMotionsMove(XAxis, pitch, ct);
                var bAbs = await MeasureScribeAbsAsync(ct);
                await _sequenceService.RelativeMotionsMove(XAxis, -pitch, ct);

                if (bAbs == null)
                    return aAbs; // 옆 Die 미검출 → 이 Die는 보정 없이 종료

                // (5) A·B 기울기 → W_T 보정 (±90° 정규화)
                double angleDeg = Math.Atan2(bAbs.Y - aAbs.Y, bAbs.X - aAbs.X) * (180.0 / Math.PI);
                if (angleDeg > 90) angleDeg -= 180;
                else if (angleDeg < -90) angleDeg += 180;
                ScribeThetaAngleDeg = angleDeg;

                if (Math.Abs(angleDeg) < ScribeThetaMinDeg)
                {
                    ScribeCenterStatus = $"{pass} — 기울기 {angleDeg:F4}° (수렴)";
                    return aAbs; // (6) 수렴
                }

                double corr = ThetaSign * angleDeg;
                ScribeCenterStatus = $"{pass} — 기울기 {angleDeg:F4}° → W_T {-corr:F4}° 보정 ({k + 1}/{iter})";
                _logger.Information("Wafer 중심 2차 — {Pass} 기울기={A:F4}°, W_T 보정={C:F4}° (iter {K})",
                    pass, angleDeg, -corr, k + 1);
                await _sequenceService.RelativeMotionsMove(TAxis, -corr, ct);
                // (6) 반복 — 다음 루프에서 A 재정렬
            }

            return aAbs;
        }

        /// <summary>
        /// (7) 한 방향(dir=+1:+X, −1:−X)으로 기준 위치에서 pitch씩 이동하며 매 Die에서 ConvergeThetaAtDieAsync 수행.
        /// steps(편도 Die 수)만큼 진행하되, Scribe 미검출(웨이퍼 끝)이면 직전 이동을 되돌리고 조기 종료한다.
        /// </summary>
        private async Task RunScribeThetaSweepAsync(int dir, double pitch, int steps, string pass, CancellationToken ct)
        {
            for (int s = 0; s < steps; s++)
            {
                ct.ThrowIfCancellationRequested();

                double shift = dir * pitch;
                ScribeCenterStatus = $"{pass} 스윕 — X {shift:+0.000;-0.000}mm 이동 ({s + 1}/{steps})...";
                await _sequenceService.RelativeMotionsMove(XAxis, shift, ct);

                var a = await ConvergeThetaAtDieAsync(pitch, pass, ct);
                if (a == null)
                {
                    // 끝 Die(미검출) — 직전 이동 되돌리고 종료
                    await _sequenceService.RelativeMotionsMove(XAxis, -shift, ct);
                    ScribeCenterStatus = $"{pass} 스윕 — 끝 Die 도달(미검출), {s} 스텝 완료";
                    _logger.Information("Wafer 중심 2차 — {Pass} 끝 Die 도달(step {S})", pass, s);
                    return;
                }
            }
            ScribeCenterStatus = $"{pass} 스윕 — {steps} 스텝 완료 (기울기 {ScribeThetaAngleDeg:F4}°)";
        }

        // ── 3차: (2차 측정 + 저배율카메라↔Shank 상대거리)만큼 Shank를 중심으로 시프트 ──
        [RelayCommand]
        private async Task FindCenterStep3()
        {
            if (IsAligning) return;
            if (!HasScribeMeasure)
            {
                ScribeCenterStatus = "먼저 2차 Scribeline 측정을 수행하세요.";
                return;
            }

            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                double shankLowX = _ecParamService.GetDouble("ShankLowOffsetX");
                double shankLowY = _ecParamService.GetDouble("ShankLowOffsetY");

                // Shank 중심을 스크라이브 교차점(웨이퍼 중심)으로:
                //   절대목표 = 교차점 절대좌표 + (저배율카메라 ↔ Shank 상대거리)
                double targetHX = ScribeAbsX + shankLowX;
                double targetWY = ScribeAbsY + shankLowY;

                ScribeCenterStatus = "Shank를 중심으로 시프트 중...";
                _logger.Information(
                    "Wafer 중심 3차 시프트 — target=({X:F4},{Y:F4}), shankLowOffset=({SX:F4},{SY:F4})",
                    targetHX, targetWY, shankLowX, shankLowY);

                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, targetHX, ct),
                    _sequenceService.MotionsMove(YAxis, targetWY, ct));

                ScribeCenterStatus = $"시프트 완료 — Shank 목표=({targetHX:F4},{targetWY:F4})";
                _logger.Information("Wafer 중심 3차 시프트 완료");
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "시프트 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "중심 시프트 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        #endregion

        // ═══════════════════════════════════════════════════════════════
        //  Wafer Theta 보정 (HC1 AlignMark · Die-to-Die 왕복 스윕)
        //   1) 현재 위치에서 HC1 AlignMark 측정.
        //       - 검출 실패 → Y 탐색 후에도 미검출이면 "H_X/W_Y/W_T 조정 후 재실행" 안내(수동).
        //   2) 정방향(+X): 피치(=DieSize+Gap)×스텝Die 만큼 X를 쉬프트하며, 인접 두 지점의
        //      AlignMark 기울기(atan2)가 0이 되도록 매 Die마다 W_T를 점진 보정한다.
        //      끝 Die(마크 소실) 또는 최대 스텝까지 진행.
        //   3) 역방향(-X): 끝 Die에서 시작 방향으로 역주행하며 동일하게 Die마다 보정한다.
        //
        //  전제: HC1 고배율 촬상 높이(Z)에 초점이 맞아 있어야 한다.
        // ═══════════════════════════════════════════════════════════════

        #region Wafer Theta 보정 (HC1 AlignMark)

        private const CameraType Hc1Cam = CameraType.HC1_HIGH;
        private const MarkType AlignMark = MarkType.ALIGN_MARK;
        private const string ThetaAxis = MotionExtensions.W_T;   // 웨이퍼 테타 축
        // W_T 회전 부호 (하드웨어 방향과 반대면 +1로 뒤집기)
        private const double ThetaSign = -1.0;

        [ObservableProperty] private string thetaStatus = "-";
        [ObservableProperty] private double thetaAngleDeg;       // 마지막 측정 기울기(°)
        [ObservableProperty] private int thetaShiftDies = 1;     // 스텝당 이동 Die 수(피치 배수)
        [ObservableProperty] private int thetaMaxIter = 10;      // 패스당 최대 스텝(안전 상한)
        [ObservableProperty] private double thetaMinDeg = 0.01; // 보정 임계각(°, 미만이면 해당 스텝 보정 생략)
        [ObservableProperty] private double thetaSearchStepMm = 0.30;   // FOV 이탈 시 Y 탐색 스텝(mm)
        [ObservableProperty] private double thetaSearchRangeMm = 0.60; // Y 탐색 최대 범위(mm, ±)

        // ── Theta 보정 버튼 ──
        [RelayCommand]
        private async Task ThetaCorrection()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                // 스텝(=피치×스텝Die)과 편도 스텝 수 산출
                double pitch = DieSizeX + GapX;                    // X 방향 Die 피치
                int stepDies = Math.Max(1, ThetaShiftDies);        // 스텝당 이동 Die 수
                double pitchStep = pitch * stepDies;
                if (pitchStep <= 0)
                {
                    ThetaStatus = "DieSize/Gap 값이 유효하지 않습니다.";
                    return;
                }
                int steps = Math.Max(1, ThetaMaxIter);             // 패스당 최대 스텝(끝 Die 소실 시 조기 종료)

                // ── 1) 정방향(+X): 시작 → 끝 Die 까지 Die마다 점진 보정 ──
                if (!await RunThetaSweepAsync(+1, pitchStep, stepDies, steps, "정방향", ct)) return;

                // ── 2) 역방향(-X): 끝 Die → 시작 방향 역주행, 동일 보정 ──
                if (!await RunThetaSweepAsync(-1, pitchStep, stepDies, steps, "역방향", ct)) return;

                ThetaStatus = $"Theta 보정 완료 — 왕복 스윕, 최종 기울기 {ThetaAngleDeg:F4}°";
                _logger.Information("Theta 보정 완료 — 왕복 스윕, 최종 기울기={A:F4}°", ThetaAngleDeg);
            }
            catch (OperationCanceledException) { ThetaStatus = "Theta 보정 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Theta 보정 오류");
                ThetaStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        /// <summary>
        /// 한 방향(dir=+1: +X, dir=-1: -X)으로 pitchStep 만큼 X를 쉬프트하며,
        /// 인접 두 지점의 AlignMark 기울기가 0이 되도록 매 스텝(Die)마다 W_T를 점진 보정한다.
        /// 지정 스텝 수(steps)를 채우거나, 마크를 회복 불가하게 놓치면(웨이퍼 끝 Die) 종료한다.
        /// 반환: 시작 마크 검출에 성공해 스윕을 진행했으면 true, 초기 미검출이면 false.
        /// </summary>
        private async Task<bool> RunThetaSweepAsync(int dir, double pitchStep, int stepDies, int steps, string pass, CancellationToken ct)
        {
            // 시작점(기준 Die) 측정 — FOV 이탈 시 Y 탐색으로 보강
            ThetaStatus = $"{pass} 스윕 — 시작 AlignMark 측정 중...";
            var prev = await MeasureHc1AlignAbsAsync(ct) ?? await SearchAlignByYAsync(ct);
            if (prev == null)
            {
                ThetaStatus = $"{pass} 시작 AlignMark 미검출 — H_X/W_Y/W_T로 마크가 보이는 위치로 이동 후 다시 실행하세요.";
                _logger.Warning("Theta 보정 — {Pass} 시작 AlignMark 미검출(수동 조정 필요)", pass);
                return false;
            }

            // Die 수 × 스텝 수로 스윕 범위를 사전 제한 → 화면(웨이퍼) 밖 이탈 방지.
            // 시작 위치에서 sweep 방향으로 남은 Die 수를 스텝당 이동 Die 수로 나눈 값과
            // 사용자가 설정한 최대 스텝(steps) 중 작은 값을 실제 스텝 수로 사용한다.
            int maxByWafer = MaxStepsWithinWafer(prev, dir, stepDies);
            int effectiveSteps = Math.Min(steps, maxByWafer);
            if (effectiveSteps < steps)
                _logger.Information("Theta 보정 — {Pass} 웨이퍼 경계 제한: {Eff}/{Req} 스텝(잔여 Die {Rem}개)",
                    pass, effectiveSteps, steps, maxByWafer * Math.Max(1, stepDies));
            if (effectiveSteps <= 0)
            {
                ThetaStatus = $"{pass} 스윕 — 이 방향으로 이동 가능한 Die 없음(웨이퍼 경계), 스킵";
                _logger.Information("Theta 보정 — {Pass} 이동 가능한 Die 없음(웨이퍼 경계)", pass);
                return true;
            }

            for (int s = 0; s < effectiveSteps; s++)
            {
                ct.ThrowIfCancellationRequested();

                // 다음 Die로 X 쉬프트 후 측정 (FOV 이탈 시 Y 탐색)
                double shift = dir * pitchStep;
                ThetaStatus = $"{pass} 스윕 — X {shift:+0.000;-0.000}mm 이동 후 측정 ({s + 1}/{effectiveSteps})...";
                await _sequenceService.RelativeMotionsMove(XAxis, shift, ct);

                var cur = await MeasureHc1AlignAbsAsync(ct);
                if (cur == null)
                {
                    _logger.Warning("Theta 보정 — {Pass} AlignMark FOV 이탈, Y 탐색 시작(step {S})", pass, s + 1);
                    cur = await SearchAlignByYAsync(ct);
                }
                if (cur == null)
                {
                    // 회복 불가 → 끝 Die 도달로 간주. 직전 이동 되돌리고 스윕 종료.
                    await _sequenceService.RelativeMotionsMove(XAxis, shift, ct);
                    ThetaStatus = $"{pass} 스윕 — 끝 Die 도달(마크 소실), {s} 스텝 보정 완료";
                    _logger.Information("Theta 보정 — {Pass} 끝 Die 도달(step {S})", pass, s);
                    return true;
                }

                // 인접 두 점 기울기(수평 대비, ±90° 정규화 — 역방향도 동일 부호로 수렴)
                double angleDeg = Math.Atan2(cur.Y - prev.Y, cur.X - prev.X) * (180.0 / Math.PI);
                if (angleDeg > 90) angleDeg -= 180;
                else if (angleDeg < -90) angleDeg += 180;
                ThetaAngleDeg = angleDeg;

                if (Math.Abs(angleDeg) >= ThetaMinDeg)
                {
                    // 기울기를 상쇄하도록 W_T 회전
                    double corr = ThetaSign * angleDeg;
                    ThetaStatus = $"{pass} 스윕 — 기울기 {angleDeg:F4}° → W_T {-corr:F4}° 회전 ({s + 1}/{effectiveSteps})...";
                    _logger.Information("Theta 보정 — {Pass} 기울기={A:F4}°, W_T 회전={C:F4}° (step {S})",
                        pass, angleDeg, -corr, s + 1);
                    await _sequenceService.RelativeMotionsMove(ThetaAxis, -corr, ct);

                    // 회전으로 현재 마크가 이동 → 다음 세그먼트 기준점 재측정
                    prev = await MeasureHc1AlignAbsAsync(ct) ?? cur;
                }
                else
                {
                    prev = cur;
                }
            }

            ThetaStatus = $"{pass} 스윕 — {effectiveSteps} 스텝 완료 (기울기 {ThetaAngleDeg:F4}°)";
            _logger.Information("Theta 보정 — {Pass} {S} 스텝 완료, 기울기={A:F4}°", pass, effectiveSteps, ThetaAngleDeg);
            return true;
        }

        /// <summary>
        /// 시작 위치(startAbs)에서 sweep 방향(dir=+1:+X, -1:-X)으로 웨이퍼 가장자리(가장 바깥 Die)까지
        /// 남은 Die 수를 스텝당 이동 Die 수(stepDies)로 나눠, 화면(웨이퍼) 밖으로 벗어나지 않는
        /// 최대 스텝 수를 계산한다. DieList(웨이퍼 맵)가 있으면 해당 Row의 Col 범위로 정확히,
        /// 없으면 웨이퍼 반경(Die 단위)으로 근사한다.
        /// </summary>
        private int MaxStepsWithinWafer(Point2D startAbs, int dir, int stepDies)
        {
            double pitchX = DieSizeX + GapX;
            if (pitchX <= 0) return 0;

            double halfCol = (WaferSize - 1) / 2.0;
            int startCol = (int)Math.Round((startAbs.X - CenterX) / pitchX + halfCol);

            // 반경(Die 단위) 기반 근사 — DieList/센터 정보가 없을 때의 안전 상한
            int radiusDies = (int)Math.Floor((WaferSize + 1) / 2.0);
            int availableDies = radiusDies;

            if (DieList != null && DieList.Count > 0)
            {
                double pitchY = DieSizeY + GapY;
                double halfRow = (WaferSize - 1) / 2.0;
                int startRow = pitchY > 0
                    ? (int)Math.Round(halfRow - (startAbs.Y - CenterY) / pitchY)
                    : (int)Math.Round(halfRow);

                var cols = DieList.Where(d => d.Row == startRow).Select(d => d.Col).ToList();
                if (cols.Count > 0)
                {
                    int minCol = cols.Min();
                    int maxCol = cols.Max();
                    availableDies = dir > 0 ? (maxCol - startCol) : (startCol - minCol);
                }
            }

            if (availableDies < 0) availableDies = 0;
            return availableDies / Math.Max(1, stepDies);   // 정수 나눗셈(내림)
        }

        /// <summary>
        /// HC1 고배율로 AlignMark를 AF 후 측정하고, 마크의 절대(스테이지) 좌표를 반환한다.
        /// 검출 실패 시 null.  (절대좌표 = 현재 스테이지 − 카메라→마크 오프셋)
        /// </summary>
        private async Task<Point2D?> MeasureHc1AlignAbsAsync(CancellationToken ct)
        {
            try
            {
                await _communication.RequestAFStart(Hc1Cam, AlignMark, ct);
                var r = await _communication.RequestVisionMarkPosition(AlignMark, Hc1Cam, DirectType.LEFT.ToString());
                if (r == null || r.Result == Result.NG) return null;

                double curHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
                double curWY = await _sequenceService.GetCurrentPosition(YAxis, ct);
                return Point2D.of(curHX - r.X, curWY - r.Y);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Warning(e, "HC1 AlignMark 측정 예외");
                return null;
            }
        }

        /// <summary>
        /// AlignMark가 FOV를 벗어났을 때의 탐색 전략.
        /// 기울기로 인한 변위는 주로 Y 방향이므로 W_Y를 0 기준 좌우로 확장 스캔
        /// (+step, -step, +2step, -2step ...)하며 재측정한다.
        /// 마크를 찾으면 그 절대좌표를 반환하고, 성공/실패와 무관하게 W_Y는 원위치로 복귀한다
        /// (절대좌표는 W_Y 이동에 불변이므로 결과에 영향 없음).
        /// </summary>
        private async Task<Point2D?> SearchAlignByYAsync(CancellationToken ct)
        {
            double step = Math.Max(0.1, ThetaSearchStepMm);
            double maxRange = Math.Max(step, ThetaSearchRangeMm);
            double applied = 0;      // 현재까지 적용된 W_Y 상대 이동량
            Point2D? found = null;

            try
            {
                for (int k = 1; k * step <= maxRange + 1e-9 && found == null; k++)
                {
                    foreach (int sign in new[] { +1, -1 })
                    {
                        ct.ThrowIfCancellationRequested();
                        double target = sign * k * step;
                        await _sequenceService.RelativeMotionsMove(YAxis, target - applied, ct);
                        applied = target;

                        var r = await MeasureHc1AlignAbsAsync(ct);
                        if (r != null)
                        {
                            found = r;
                            _logger.Information("Theta 보정 — Y 탐색 성공(W_Y {D:F3}mm 지점)", target);
                            break;
                        }
                    }
                }
            }
            finally
            {
                // W_Y 원위치 복귀 (취소되어도 복귀 보장)
                if (Math.Abs(applied) > 1e-9)
                    await _sequenceService.RelativeMotionsMove(YAxis, -applied, CancellationToken.None);
            }

            return found;
        }

        #endregion
    }
}
