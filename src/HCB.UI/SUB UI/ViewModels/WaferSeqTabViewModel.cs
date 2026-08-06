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
        // 저배율 카메라 센터 (기존 Wafer 중심 — 측정으로 산출)
        [ObservableProperty] private double centerX;
        [ObservableProperty] private double centerY;
        // 고배율 카메라 센터 (= 저배 센터 + ShankLowOffset + HcCenterError)
        [ObservableProperty] private double highCenterX;
        [ObservableProperty] private double highCenterY;
        // 저배 → 고배 센터 변환 오프셋 (ShankLowOffset + HcCenterError). GenerateWaferMap에서 Die별 고배 위치 산출에 사용.
        private double _highOffsetX;
        private double _highOffsetY;

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
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        [NotifyPropertyChangedFor(nameof(IsNotBonding))]
        private bool isBonding;

        /// <summary>본딩·정렬 등 장비 동작 진행 중 여부 (버튼 비활성 조건).</summary>
        public bool IsBusy => IsAligning || IsBonding;

        /// <summary>본딩 진행 중이 아님 (BONDING 버튼 표시 조건 — CANCEL과 자리를 바꿔 표시).</summary>
        public bool IsNotBonding => !IsBonding;

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

                    double posX = CenterX + (col - halfCol) * (DieSizeX + GapX);
                    double posY = CenterY - (row - halfRow) * (DieSizeY + GapY);

                    dies.Add(new DieData
                    {
                        Row = row,
                        Col = col,
                        PositionX = posX,                       // 저배율 카메라 센터 기준
                        PositionY = posY,
                        HighPositionX = posX + _highOffsetX,    // 고배율 카메라 센터 기준
                        HighPositionY = posY + _highOffsetY,
                        DieBrush = DefaultDieBrush,
                        Information = "Ready"
                    });
                }
            }

            DieList = dies;
            SelectedDie = null;
            HasDieSelected = false;
        }

        /// <summary>
        /// 찾은 Wafer 중심을 격자 원점으로 삼아, 현재 Wafer Setting(WaferSize·DieSize·Gap)에 맞춰
        /// 모든 Die의 절대 위치값을 두 가지 센터 기준으로 재계산한다.
        ///  · 저배율 카메라 센터(CenterX/Y)          = 인자로 받은 측정 중심(기존 정보 유지)
        ///  · 고배율 카메라 센터(HighCenterX/Y)      = 저배 센터 + ShankLowOffset + HcCenterError
        /// Theta는 이미 보정되어 Die 격자가 기계 축과 정렬됐다고 보고 축 정렬 격자로 계산한다.
        /// </summary>
        private async Task ApplyWaferCenter(double centerAbsX, double centerAbsY)
        {
            // 1) 저배율 카메라 센터 (기존)
            CenterX = centerAbsX;
            CenterY = centerAbsY;

            // 2) 고배율 카메라 센터 = 저배 센터 + ShankLowOffset + HcCenterError
            double shankLowX = _ecParamService.GetDouble("ShankLowOffsetX");
            double shankLowY = _ecParamService.GetDouble("ShankLowOffsetY");
            double hcCenterErrorX = await GetRecipeSafe("HcCenterErrorX");
            double hcCenterErrorY = await GetRecipeSafe("HcCenterErrorY");

            _highOffsetX = shankLowX + hcCenterErrorX;
            _highOffsetY = shankLowY + hcCenterErrorY;
            HighCenterX = CenterX + _highOffsetX;
            HighCenterY = CenterY + _highOffsetY;

            GenerateWaferMap();   // Center/HighCenter 기준으로 DieList의 Position/HighPosition 재생성
        }

        /// <summary>레시피 double 값을 안전하게 조회(미설정·형식오류 시 0 반환, 경고 로그).</summary>
        private async Task<double> GetRecipeSafe(string name)
        {
            try { return await _sequenceService.GetRecipe(name); }
            catch (Exception e)
            {
                _logger.Warning("레시피 {Name} 조회 실패 — 0 사용: {Msg}", name, e.Message);
                return 0;
            }
        }

        /// <summary>
        /// 찾은 Wafer 중심을 기준으로 나머지 Die들의 위치값을 전부 재계산한다.
        ///  · 2차(정밀) 완료 시: 기준 Scribe 절대좌표에서 초기 Shift(ScribeShiftX/Y)를 되돌린 값이 Wafer 중심
        ///  · 2차 미완료·1차만 완료 시: 1차 원 피팅 대략 중심 사용
        /// </summary>
        [RelayCommand]
        private async Task ComputeDiePositions()
        {
            double cx, cy;
            string src;

            if (HasScribeMeasure)
            {
                // 기준 Scribe → Wafer 중심 (2차에서 실제 적용된 Shift 되돌림.
                // X는 십자마크가 있는 선으로 스냅되므로 Recipe의 ScribeShiftX와 다를 수 있다)
                cx = ScribeAbsX - _refScribeOffsetX;
                cy = ScribeAbsY - _refScribeOffsetY;
                src = "2차 정밀 중심";
            }
            else if (HasCoarseCenter)
            {
                cx = CoarseCenterX;
                cy = CoarseCenterY;
                src = "1차 대략 중심";
            }
            else
            {
                ScribeCenterStatus = "중심을 먼저 계산하세요 (1차 또는 2차).";
                return;
            }

            await ApplyWaferCenter(cx, cy);
            ScribeCenterStatus =
                $"Die 위치 계산 완료 — 저배 센터=({CenterX:F4},{CenterY:F4}), 고배 센터=({HighCenterX:F4},{HighCenterY:F4}), {DieList.Count}개 Die";
            _logger.Information("Die 위치 전체 계산 — {Src} 저배=({X:F4},{Y:F4}), 고배=({HX:F4},{HY:F4}), {N}개",
                src, CenterX, CenterY, HighCenterX, HighCenterY, DieList.Count);
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

            // 중심 미계산 시 본딩 차단 — Die Center(고배 위치)가 아직 산출되지 않았으면 진행 불가
            if (!HasCoarseCenter && !HasScribeMeasure)
            {
                SelectedDie.Information = "중심 미계산 — 1차(또는 2차) 실행 후 본딩하세요";
                OnPropertyChanged(nameof(SelectedDie));
                ScribeCenterStatus = "Wafer 중심 미계산 — 본딩 차단(1차/2차로 중심을 먼저 계산하세요)";
                _logger.Warning("Wafer Bonding 차단 — Wafer 중심 미계산(1/2차 미완료)");
                return;
            }

            IsBonding = true;
            SelectedDie.Information = "Bonding...";
            OnPropertyChanged(nameof(SelectedDie));

            // 클릭한 Die의 Center(고배 절대좌표) — BtmHighAlign에서 PLACE_CENTER 대신 여기로 이동
            var dieCenter = Point2D.of(SelectedDie.HighPositionX, SelectedDie.HighPositionY);

            // 카메라 거리·회전중심(MeasureCamDistAndHcro) 측정을 WaferCenter(고배 위치)에서 수행하도록 지정.
            // (StepSeqTab 자체 본딩에 누수되지 않도록 본딩 직후 finally에서 원복)
            var waferCenter = Point2D.of(HighCenterX, HighCenterY);
            _sequenceService.CamHcroCenterOverride = waferCenter;

            _logger.Information("Wafer Bonding 시작 — Die({Row},{Col}), 고배 Center=({X:F4},{Y:F4}), Cam/HCRO 측정 위치=WaferCenter({WX:F4},{WY:F4})",
                SelectedDie.Row, SelectedDie.Col, dieCenter.X, dieCenter.Y, waferCenter.X, waferCenter.Y);

            try
            {
                await StepSeqTab.WaferBonding(dieCenter);
            }
            finally
            {
                _sequenceService.CamHcroCenterOverride = null;
            }

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

        // ── 선택 Die를 저배/고배 카메라로 보기 (해당 Die 위치로 모션 이동 후 그 카메라로 측정) ──
        //  측정 결과(카메라 중심 → AlignMark 오프셋, mm)는 Low/Hc1/Hc2MeasureText에 표시된다.
        [ObservableProperty] private string lowMeasureText = "-";   // HC_LOW 측정 결과
        [ObservableProperty] private string hc1MeasureText = "-";   // HC1 고배 측정 결과
        [ObservableProperty] private string hc2MeasureText = "-";   // HC2 고배 측정 결과

        [RelayCommand]
        private Task ViewDieLowMag() => MoveToSelectedDie(highMag: false);

        [RelayCommand]
        private Task ViewDieHighMag() => MoveToSelectedDie(highMag: true);

        // highMag=false: 저배 카메라 위치(Die 저배 좌표), true: 고배 카메라 위치(Die 고배 좌표)로 이동.
        // 규칙1에 따라 Z(h_z→H_Z)를 먼저 이동한 뒤 X/Y를 이동하고, 마지막에 해당 카메라로 측정한다.
        private async Task MoveToSelectedDie(bool highMag)
        {
            if (SelectedDie == null) return;
            if (IsAligning || IsBonding) return;

            // Die 위치는 중심 계산 후에만 유효
            if (!HasCoarseCenter && !HasScribeMeasure)
            {
                ScribeCenterStatus = "중심 미계산 — 1차(또는 2차)로 Die 위치를 먼저 계산하세요.";
                return;
            }

            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            string mag = highMag ? "고배" : "저배";
            double tx = highMag ? SelectedDie.HighPositionX : SelectedDie.PositionX;
            double ty = highMag ? SelectedDie.HighPositionY : SelectedDie.PositionY;

            try
            {
                // 규칙1: Z 먼저(h_z→H_Z)
                ScribeCenterStatus = $"{mag} Z 이동 중...";
                if (highMag) await MoveZForHighMagAsync(ct);
                else await MoveZForLowMagAsync(ct);

                // 해당 Die로 X/Y 이동
                ScribeCenterStatus = $"Die({SelectedDie.Row},{SelectedDie.Col}) {mag}로 이동 중...";
                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, tx, ct),
                    _sequenceService.MotionsMove(YAxis, ty, ct));

                _logger.Information("Die({Row},{Col}) {Mag} 이동 — ({X:F4},{Y:F4})",
                    SelectedDie.Row, SelectedDie.Col, mag, tx, ty);

                // 이동 완료 후 해당 카메라로 AlignMark 측정 (고배는 HC1/HC2 모두)
                string measured = highMag
                    ? await MeasureHighMagAsync(ct)
                    : await MeasureLowMagAsync(ct);

                ScribeCenterStatus =
                    $"Die({SelectedDie.Row},{SelectedDie.Col}) {mag} 이동 완료 — ({tx:F4},{ty:F4}) / 측정 {measured}";
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "Die 이동 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Die 이동/측정 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        /// <summary>
        /// 저배율(HC_LOW)로 현재 위치의 AlignMark를 측정한다.
        /// HC_LOW는 피사계 심도가 커서 AF는 수행하지 않는다.
        /// </summary>
        private async Task<string> MeasureLowMagAsync(CancellationToken ct)
        {
            Hc1MeasureText = Hc2MeasureText = "-";
            ScribeCenterStatus = "저배(HC_LOW) 측정 중...";
            await _communication.RequestVisionMarkPosition(MarkType.DIE_CENTER_BOTTOM, CameraType.HC_LOW, "", true);
            //= await MeasureMarkTextAsync(LowCam, "", af: false, ct);
            _logger.Information("Die 저배 측정 완료");
            return "WAFER 저배 측정 완료";
        }

        /// <summary>
        /// 고배율 HC1(LEFT)·HC2(RIGHT)로 현재 위치의 AlignMark를 각각 AF 후 측정한다.
        /// 비전 통신은 단일 채널이므로 순차 실행한다.
        /// </summary>
        private async Task<string> MeasureHighMagAsync(CancellationToken ct)
        {
            LowMeasureText = "-";

            ScribeCenterStatus = "고배 HC1 측정 중...";
            await MeasureMarkTextAsync(Hc1Cam, DirectType.LEFT.ToString(), af: true, ct);

            ct.ThrowIfCancellationRequested();

            ScribeCenterStatus = "고배 HC2 측정 중...";
            await MeasureMarkTextAsync(Hc2Cam, DirectType.RIGHT.ToString(), af: true, ct);

            _logger.Information("Die 고배 측정 완료");
            return "WAFER 고배 측정 완료";
        }

        /// <summary>
        /// 지정 카메라로 AlignMark를 1회 측정하고 표시용 문자열(카메라 중심 → 마크 오프셋, mm)을 반환한다.
        /// 검출 실패(NG)·예외는 모션을 멈추지 않고 문자열로만 보고한다.
        /// </summary>
        private async Task<string> MeasureMarkTextAsync(CameraType cam, string direct, bool af, CancellationToken ct)
        {
            try
            {
                if (af) await _communication.RequestAFStart(cam, AlignMark, ct);

                var r = await _communication.RequestVisionMarkPosition(AlignMark, cam, direct);
                if (r == null || r.Result == Result.NG) return "NG";

                return $"({r.X:F4}, {r.Y:F4})";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Warning(e, "{Cam} AlignMark 측정 예외", cam);
                return $"오류({e.Message})";
            }
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

        private const CameraType LowCam = CameraType.HC_LOW; // 저배율 카메라(Scribe 측정용)
        private const string XAxis = MotionExtensions.H_X;   // 스테이지 X
        private const string YAxis = MotionExtensions.W_Y;   // 웨이퍼 테이블 Y
        private const string TAxis = MotionExtensions.W_T;   // 웨이퍼 테이블 Y

        // 현재 Z가 고배율 위치에 있는지 여부. 고배→저배 전환 시 H_Z를 먼저 이동하기 위해 추적.
        private bool _zAtHighMag;

        // ── Z축 이동 규칙 ──
        //  · 기본(저배/신규): h_z 먼저 → H_Z
        //  · 고배 → 저배 전환: H_Z 먼저 → h_z
        /// <summary>저배율 측정 Z 위치로 이동. h_z(SAFTY) / H_Z(저배율 측정).</summary>
        private async Task MoveZForLowMagAsync(CancellationToken ct)
        {
            if (_zAtHighMag)
            {
                // 고배 → 저배: H_Z 먼저 이동
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "저배확인", ct);
                await _sequenceService.MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, ct);
            }
            else
            {
                // 기본: h_z 먼저 이동
                await _sequenceService.MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "저배확인", ct);
            }
            _zAtHighMag = false;
        }

        /// <summary>고배율 측정 Z 위치로 이동. h_z → H_Z 순(레시피/파라미터 기반 절대 이동).</summary>
        private async Task MoveZForHighMagAsync(CancellationToken ct)
        {
            double fidAlignGap = RecipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);

            // h_z 먼저
            //double safty = await _sequenceService.GetRecipe("SAFTY");
            await _sequenceService.MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, -fidAlignGap, ct);

            // H_Z
            double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
            double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
            double shankToWaferOffset = _ecParamService.GetDouble("ShankToWaferOffset");
            await _sequenceService.MotionsMove(MotionExtensions.H_Z,
                shankToWaferOffset - topDieThickness - btmDieThickness + fidAlignGap - 0.1, ct);

            _zAtHighMag = true;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        private bool isAligning;          // 측정/시프트 진행 중 busy 플래그
        [ObservableProperty] private string scribeCenterStatus = "-";
        [ObservableProperty] private double scribeOffsetXUm;   // 카메라 중심 → 스크라이브 교차점 오프셋(μm)
        [ObservableProperty] private double scribeOffsetYUm;
        [ObservableProperty] private double scribeAbsX;        // 스크라이브 교차점 절대(스테이지) 좌표
        [ObservableProperty] private double scribeAbsY;
        [ObservableProperty] private double scribeAbsT;
        [ObservableProperty] private bool hasScribeMeasure;    // 2차 측정 완료 여부(3차 활성화용)

        // ── 1차: 저배율 3점(11/4/7시) 웨이퍼 엣지 절대좌표 ──
        //  operator가 저배율 카메라를 각 시계 위치로 조그한 뒤 해당 버튼으로 1점씩 측정.
        //  3점이 모두 채워지면 FindCenterStep1이 원 피팅으로 대략 중심을 산출·이동한다.
        private readonly Point2D?[] _edgePoints = new Point2D?[3]; // [0]=11시, [1]=4시, [2]=7시
        [ObservableProperty] private string edgePoint12Text = "-";
        [ObservableProperty] private string edgePoint04Text = "-";
        [ObservableProperty] private string edgePoint07Text = "-";
        [ObservableProperty] private double coarseCenterX;     // 1차 원 피팅 대략 중심(스테이지 절대)
        [ObservableProperty] private double coarseCenterY;
        [ObservableProperty] private bool hasCoarseCenter;     // 1차 대략 중심 산출 완료 여부

        // ── 2차: Scribeline 기반 중심/Theta 정렬 설정 ──
        //  ScribeShiftX/Y : (Recipe) 대략 중심에서 기준 Scribe 측정 위치까지의 초기 Shift(mm)
        //  ScribeStepDies : 좌우 대칭 쌍의 확장 단위(선 인덱스 간격 = Die 수)
        //  ScribeThetaMinDeg : θ 보정 임계각(°, 미만이면 보정 생략)
        //  ※ θ 보정은 중심 대칭 쌍을 안쪽→바깥으로 확장해, 검출되는 "가장 바깥"(긴 baseline) 쌍에서 1회 수행한다
        //    (CorrectThetaBySymmetricPairAsync). 좌우 유효 확장 배수는 중심 기준 십자마크 범위[firstC,lastC]에서 자동 산출.
        [ObservableProperty] private double scribeShiftX;      // (Recipe) 초기 Shift X
        [ObservableProperty] private double scribeShiftY;      // (Recipe) 초기 Shift Y
        [ObservableProperty] private int scribeStepDies = 1;   // 대칭 쌍 확장 단위(Die 수)
        [ObservableProperty] private double scribeThetaMinDeg = 0.01; // θ 보정 임계각(°)
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

        // ── 1차: 저배율(HC_LOW) 3점 웨이퍼 엣지로 대략 중심 산출 ──
        //  Vision v1.0 회신: 저배율은 화소당 ~45µm라 스크라이브 신뢰도가 낮으므로,
        //  대략 중심은 웨이퍼 "엣지(원호)"를 3점 잡아 최소자승 원 피팅으로 구한다.
        //  저배율 FOV(≈110mm)에 3점 동시 촬상이 불가하므로, 지정 Position(WAFER_ALIGN_1/2/3,
        //  ≈120° 간격)으로 자동 이동하며 각 위치에서 1점씩 측정한다.

        private void SetEdgeText(int idx, string text)
        {
            switch (idx)
            {
                case 0: EdgePoint12Text = text; break;
                case 1: EdgePoint04Text = text; break;
                case 2: EdgePoint07Text = text; break;
            }
        }

        // 저배 3점 측정 Position(WAFER_ALIGN_1/2/3)과 엣지 검출 시계 위치(11/4/7시) 매핑
        // ※ 11시 위치는 비전 통신 시 코드 12로 전송된다(WaferClock.H11 = 12).
        private static readonly (string pos, WaferClock clock)[] EdgeStations =
        {
            (MotionExtensions.WAFER_ALIGN_1, WaferClock.H11),
            (MotionExtensions.WAFER_ALIGN_2, WaferClock.H04),
            (MotionExtensions.WAFER_ALIGN_3, WaferClock.H07),
        };

        // ── 1차-측정: 현재 위치에서 저배율 웨이퍼 엣지 1점 측정 → 절대좌표 반환(실패 시 null) ──
        //  엣지 절대좌표 = 현재 스테이지 − 카메라→엣지 오프셋 (FindCenterStep2와 동일 부호 규약).
        private async Task<Point2D?> MeasureEdgeAbsAsync(WaferClock clock, CancellationToken ct)
        {
            var r = await _communication.RequestWaferEdge(clock, ct);
            if (r == null || r.Result == Result.NG) return null;

            double curHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
            double curWY = await _sequenceService.GetCurrentPosition(YAxis, ct);
            return Point2D.of(curHX - r.X, curWY - r.Y);
        }

        // ── 1차: 저배 3점을 Position(WAFER_ALIGN_1/2/3)으로 이동하며 자동 측정 →
        //         원 피팅으로 대략 중심 산출 후 저배 카메라를 중심으로 이동, Die 위치 전체 계산 ──
        [RelayCommand]
        private async Task FindCenterStep1()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                // 규칙1: 저배 Z 먼저(h_z→H_Z)
                ScribeCenterStatus = "저배 Z 이동 중...";
                await MoveZForLowMagAsync(ct);

                // 3점을 지정 Position으로 이동하며 자동 측정
                for (int i = 0; i < EdgeStations.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var (pos, clock) = EdgeStations[i];

                    ScribeCenterStatus = $"{pos} 위치로 이동 후 측정 중... ({i + 1}/3)";
                    _logger.Information("Wafer 중심 1차 — {Pos} 이동 후 엣지 측정", pos);
                    await _sequenceService.MotionsMove(new[] { XAxis, YAxis }, pos, ct);

                    var pt = await MeasureEdgeAbsAsync(clock, ct);
                    if (pt == null)
                    {
                        _edgePoints[i] = null;
                        SetEdgeText(i, "-");
                        ScribeCenterStatus = $"엣지 측정 실패(NG) — {pos}";
                        _logger.Warning("HC_LOW 엣지 측정 NG ({Pos})", pos);
                        return;
                    }

                    _edgePoints[i] = pt;
                    SetEdgeText(i, $"({pt.X:F4}, {pt.Y:F4})");
                    _logger.Information("HC_LOW 엣지 측정 완료 ({Pos}) — abs=({X:F4},{Y:F4})", pos, pt.X, pt.Y);
                }

                // 원 피팅 → 대략 중심
                var center = CalibrationMath.FitCircleCenter(_edgePoints.Select(p => p!).ToList());
                CoarseCenterX = center.X;
                CoarseCenterY = center.Y;
                HasCoarseCenter = true;

                ScribeCenterStatus = "1차 대략 중심으로 저배율 카메라 이동 중...";
                _logger.Information("Wafer 중심 1차 — 원 피팅 중심=({X:F4},{Y:F4})", center.X, center.Y);

                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, center.X, ct),
                    _sequenceService.MotionsMove(YAxis, center.Y, ct));

                // 대략 중심(저배) 기준으로 나머지 Die 위치값 전부 계산 (고배 센터도 함께 산출)
                await ApplyWaferCenter(center.X, center.Y);

                ScribeCenterStatus =
                    $"1차 완료 — 저배 센터=({CenterX:F4},{CenterY:F4}), 고배 센터=({HighCenterX:F4},{HighCenterY:F4}), Die {DieList.Count}개";
                _logger.Information("Wafer 중심 1차 완료 — 중심 이동/Die 위치 계산 ({X:F4},{Y:F4})", center.X, center.Y);
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "1차 중단됨"; }
            catch (InvalidOperationException e)
            {
                // 3점이 일직선(원 피팅 불가)
                _logger.Warning(e, "Wafer 중심 1차 — 원 피팅 실패(점이 일직선)");
                ScribeCenterStatus = "1차 실패 — 3점이 일직선입니다. WAFER_ALIGN_1/2/3 위치를 확인하세요.";
            }
            catch (Exception e)
            {
                _logger.Error(e, "Wafer 중심 1차 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        // ═══════ Scribeline 격자 (십자마크 유효 범위 산출) ═══════
        //  십자(+)마크는 네 Die (r,c)·(r,c+1)·(r+1,c)·(r+1,c+1)이 모두 있는 "내부" 교차점에만 생긴다.
        //  웨이퍼 최외곽 경계선은 한쪽에 Die가 없어 십자가 아니므로(ㅜ/ㅏ 모양) 측정할 수 없다.
        //
        //   · 세로선 인덱스 c = 왼쪽 Die의 Col → 중심 기준 X 오프셋 = (c − half + 0.5) × pitchX
        //   · 가로선 인덱스 r = 위쪽 Die의 Row → 중심 기준 Y 오프셋 = −(r − half + 0.5) × pitchY
        //     (GenerateWaferMap의 posX/posY 규칙과 동일 부호, half = (WaferSize−1)/2)
        //
        //  WaferSize 짝수 → 오프셋이 피치의 정수배   → 웨이퍼 중심에 Scribeline이 있다
        //  WaferSize 홀수 → 오프셋이 피치의 반정수배 → 중심에는 Die가 있고 선은 ±pitch/2에 있다
        //   ⇒ "중심에 선이 있다"고 가정하면 홀수에서 반 피치가 통째로 어긋난다.
        //  또 원형 웨이퍼라 Row마다 Die 수가 달라, 기준 선 좌/우의 측정 가능한 선 수가 서로 다르다(비대칭).
        private double DiePitchX => DieSizeX + GapX;
        private double DiePitchY => DieSizeY + GapY;
        private double HalfIndex => (WaferSize - 1) / 2.0;

        /// <summary>세로 Scribeline c의 중심(CenterX) 기준 X 오프셋(mm).</summary>
        private double ScribeLineOffsetX(int c) => (c - HalfIndex + 0.5) * DiePitchX;

        /// <summary>가로 Scribeline r의 중심(CenterY) 기준 Y 오프셋(mm).</summary>
        private double ScribeLineOffsetY(int r) => -(r - HalfIndex + 0.5) * DiePitchY;

        /// <summary>절대(스테이지) X에서 가장 가까운 세로 Scribeline 인덱스. (ScribeLineOffsetX 역산)</summary>
        private int ScribeLineIndexAtX(double absX) => DiePitchX > 0
            ? (int)Math.Round((absX - CenterX) / DiePitchX + HalfIndex - 0.5, MidpointRounding.AwayFromZero)
            : 0;

        /// <summary>절대(스테이지) Y에서 가장 가까운 가로 Scribeline 인덱스. (ScribeLineOffsetY 역산)</summary>
        private int ScribeLineIndexAtY(double absY) => DiePitchY > 0
            ? (int)Math.Round(HalfIndex - 0.5 - (absY - CenterY) / DiePitchY, MidpointRounding.AwayFromZero)
            : 0;

        /// <summary>
        /// 가로선 r 위에서 십자마크가 존재하는 세로선 인덱스 범위 [first, last]를 구한다.
        /// 교차점 (r,c)가 십자이려면 네 Die (r,c)·(r,c+1)·(r+1,c)·(r+1,c+1)이 모두 있어야 한다.
        /// 십자가 하나도 없으면(웨이퍼 밖·Die 부족) null.
        /// </summary>
        private (int first, int last)? ScribeLineRange(int r)
        {
            if (DieList == null || DieList.Count == 0) return null;

            var upper = DieList.Where(d => d.Row == r).Select(d => d.Col).ToHashSet();
            var lower = DieList.Where(d => d.Row == r + 1).Select(d => d.Col).ToHashSet();
            upper.IntersectWith(lower);                                   // 위·아래 Row에 모두 Die가 있는 Col
            var cross = upper.Where(c => upper.Contains(c + 1)).ToList(); // c·c+1이 모두 있어야 십자
            if (cross.Count == 0) return null;

            return (cross.Min(), cross.Max());
        }

        /// <summary>
        /// 요청한 가로선(preferredRow)에서 시작해 십자마크가 존재하는 가장 가까운 가로선을 찾는다.
        /// (요청 위치가 웨이퍼 최외곽이라 십자가 없으면 안쪽으로 한 줄씩 옮겨 탐색)
        /// </summary>
        private (int row, int first, int last)? FindScribeCrossRow(int preferredRow)
        {
            for (int d = 0; d <= WaferSize; d++)
            {
                foreach (int r in d == 0 ? new[] { preferredRow } : new[] { preferredRow - d, preferredRow + d })
                {
                    var range = ScribeLineRange(r);
                    if (range != null) return (r, range.Value.first, range.Value.last);
                }
            }
            return null;
        }

        // 2차에서 실제 적용된 기준 Scribe 오프셋(저배 Center → 기준 교차점, mm).
        // 십자마크가 있는 선으로 스냅되므로 Recipe의 ScribeShiftX/Y와 다를 수 있다.
        // 측정한 Scribe 절대좌표에서 Wafer 중심을 역산할 때 반드시 이 값을 되돌려야 한다.
        private double _refScribeOffsetX;
        private double _refScribeOffsetY;

        // ── 2차: 저배율(HC_LOW) Scribeline 기반 중심/Theta 정렬 ──
        //  절차(사용자 요청):
        //   (1) Recipe(ScribeShiftX/Y)에서 가장 가까운 "십자마크가 있는" Scribeline으로 이동
        //       (Y는 Recipe 값 그대로 Row를 선택, X는 유효 선으로 스냅 — 최외곽 선은 십자가 아니라 제외)
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
                int stepDies = Math.Max(1, ScribeStepDies);
                if (DiePitchX <= 0)
                {
                    ScribeCenterStatus = "DieSize/Gap 값이 유효하지 않습니다.";
                    return;
                }

                // 저배 Center는 1차(또는 이전 2차)에서 산출되어 있어야 한다
                if (!HasCoarseCenter && !HasScribeMeasure)
                {
                    ScribeCenterStatus = "저배 Center 미산출 — 1차를 먼저 실행하세요.";
                    _logger.Warning("Wafer 중심 2차 — 저배 Center 미산출(1차 미완료)");
                    return;
                }

                // (1) 기준 교차점 결정 — Recipe Shift(ScribeShiftX/Y)에서 가장 가까운
                //     "십자마크가 있는" 교차점으로 스냅한다. 최외곽 선은 십자가 아니라 제외되고,
                //     WaferSize가 홀수면 중심에 Die가 있어 선이 ±pitch/2에 놓인다.
                var cross = FindScribeCrossRow(ScribeLineIndexAtY(CenterY + ScribeShiftY));
                if (cross == null)
                {
                    ScribeCenterStatus = "십자마크 Scribeline이 없습니다 — WaferSize/DieSize/중심값을 확인하세요.";
                    _logger.Warning("Wafer 중심 2차 — 십자마크 교차점 없음(WaferSize={W}, Die {N}개)",
                        WaferSize, DieList?.Count ?? 0);
                    return;
                }
                var (refR, firstC, lastC) = cross.Value;

                int refC = Math.Clamp(ScribeLineIndexAtX(CenterX + ScribeShiftX), firstC, lastC);
                _refScribeOffsetX = ScribeLineOffsetX(refC);
                _refScribeOffsetY = ScribeLineOffsetY(refR);
                double refX = CenterX + _refScribeOffsetX;
                double refY = CenterY + _refScribeOffsetY;

                _logger.Information(
                    "Wafer 중심 2차 — 기준 교차점: 가로선={R}, 세로선={C} (십자 유효 세로선 {F}~{L}, WaferSize={W}), " +
                    "Recipe Shift=({SX:F4},{SY:F4}) → 스냅 오프셋=({OX:F4},{OY:F4})",
                    refR, refC, firstC, lastC, WaferSize, ScribeShiftX, ScribeShiftY,
                    _refScribeOffsetX, _refScribeOffsetY);

                // 규칙1: 저배 측정 전 Z 이동(h_z 먼저)
                ScribeCenterStatus = "저배 Z 이동 중...";
                await MoveZForLowMagAsync(ct);

                // 기준 교차점으로 절대 이동
                ScribeCenterStatus = $"기준 십자마크(가로선 {refR}, 세로선 {refC})로 이동 중... ({refX:F4},{refY:F4})";
                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, refX, ct),
                    _sequenceService.MotionsMove(YAxis, refY, ct));

                // (2) 기준(중심) 교차점을 비전 중심에 정렬 → 중심 절대좌표 확정
                ScribeCenterStatus = "기준 교차점 정렬 중...";
                var centerAbs = await CenterScribeAsync(ct);
                if (centerAbs == null)
                {
                    HasScribeMeasure = false;
                    ScribeCenterStatus = "기준 Scribeline 검출 실패(NG) — 위치/조명/모델 확인";
                    _logger.Warning("Wafer 중심 2차 — 기준 Scribeline 미검출");
                    return;
                }

                // 정렬 후 실제로 잡힌 교차점을 절대좌표에서 역산(중심 오프셋 확정)
                int startC = Math.Clamp(ScribeLineIndexAtX(centerAbs.X), firstC, lastC);
                int startR = ScribeLineIndexAtY(centerAbs.Y);
                _refScribeOffsetX = ScribeLineOffsetX(startC);
                _refScribeOffsetY = ScribeLineOffsetY(startR);
                if (startC != refC || startR != refR)
                    _logger.Information("Wafer 중심 2차 — 정렬된 교차점이 기준과 다름: ({RR},{RC}) → ({AR},{AC})",
                        refR, refC, startR, startC);

                // (3) θ 보정 — 중심 기준 좌우 대칭 쌍 중 "가장 바깥"(긴 baseline)에서 1회 측정·보정.
                //     인접 선 비교(짧은 baseline)가 아니라, 중심을 피벗으로 좌 −m·우 +m 대칭점을 잡아
                //     두 점 사이 긴 baseline으로 기울기를 산출한다(바깥일수록 각도 정밀도↑).
                //     W_T를 -기울기만큼 1회 회전하면 행 기울기가 0이 되고(피벗 오프셋은 위치만 이동),
                //     회전 후 (4)에서 중심을 재정렬하므로 중심 위치도 정확히 복원된다.
                ScribeCenterStatus = "θ 측정 — 좌우 대칭 쌍 확장 중...";
                await CorrectThetaBySymmetricPairAsync(startC, firstC, lastC, stepDies, ct);

                // (4) 회전 후 중심 교차점 재정렬 → 최종 중심 절대좌표 기록(Step3용)
                ScribeCenterStatus = "θ 보정 후 중심 재정렬 중...";
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

                // 정밀 중심(기준 Scribe에서 실제 적용된 Shift 되돌림, 저배) 기준으로 Die 위치 전부 계산
                // (X는 십자마크 선으로 스냅됐으므로 ScribeShiftX가 아니라 _refScribeOffsetX를 되돌린다)
                double waferCenterX = ScribeAbsX - _refScribeOffsetX;
                double waferCenterY = ScribeAbsY - _refScribeOffsetY;
                await ApplyWaferCenter(waferCenterX, waferCenterY);

                ScribeCenterStatus =
                    $"2차 완료 — 저배 센터=({CenterX:F4},{CenterY:F4}), 고배 센터=({HighCenterX:F4},{HighCenterY:F4}), 기울기 {ScribeThetaAngleDeg:F4}°";
                _logger.Information("Wafer 중심 2차 완료 — 저배=({CX:F4},{CY:F4}), 고배=({HX:F4},{HY:F4}), theta={A:F4}°, Die {N}개",
                    CenterX, CenterY, HighCenterX, HighCenterY, ScribeThetaAngleDeg, DieList.Count);
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
        /// 저배 Scribeline θ 보정 — 중심(refC) 십자마크를 피벗으로, 좌우 대칭 쌍으로 기울기를 측정·보정한다.
        /// 유효한(양옆에 십자마크가 있는) 최대 확장 배수 mMax를 구해 공용 코어에 위임한다.
        /// (검색은 안쪽→바깥, 보정은 검출되는 가장 바깥=최장 baseline에서 1회)
        /// </summary>
        private async Task<bool> CorrectThetaBySymmetricPairAsync(
            int refC, int firstC, int lastC, int stepDies, CancellationToken ct)
        {
            int step = Math.Max(1, stepDies);
            int mMax = Math.Min(refC - firstC, lastC - refC) / step;   // 좌우 모두 유효한 최대 확장 배수
            if (mMax < 1)
            {
                ScribeCenterStatus = "중심 좌우 대칭 십자마크 쌍이 없어 θ 보정 생략";
                _logger.Warning("Wafer 중심 2차 — 대칭 쌍 없음(refC={C}, 유효 {F}~{L})", refC, firstC, lastC);
                return false;
            }

            return await CorrectThetaBySymmetricSweepAsync(
                mMax, step * DiePitchX, ScribeThetaMinDeg,
                MeasureScribeAbsAsync,
                s => ScribeCenterStatus = s, a => ScribeThetaAngleDeg = a,
                "2차 θ", ct);
        }

        /// <summary>
        /// 대칭 쌍 θ 보정 공용 코어 (저배 Scribe · 고배 AlignMark 공통).
        /// 현재 위치를 피벗(offset 0)으로, 좌 −m·우 +m 대칭 쌍을 "안쪽(m=1)→바깥(m=mMax)"으로 확장하며
        /// 매 단계 두 점을 measureAbs로 측정한다. 한쪽이라도 미검출이면 그 지점을 웨이퍼 끝으로 보고
        /// 확장을 멈추고, 그때까지 성공한 "가장 바깥(넓은)" 쌍의 긴 baseline으로 기울기를 1회 산출한다.
        /// |기울기| ≥ minDeg 이면 W_T를 −기울기만큼 1회 회전 보정한다(회전 피벗이 중심과 달라도 각도는 0이 됨).
        /// 측정·보정 후 X는 피벗으로 복귀한 상태로 끝난다.
        /// 반환: 유효 쌍을 하나라도 찾았으면 true(보정 또는 임계 미만 생략), 전무하면 false.
        /// </summary>
        private async Task<bool> CorrectThetaBySymmetricSweepAsync(
            int mMax, double pitchStep, double minDeg,
            Func<CancellationToken, Task<Point2D?>> measureAbs,
            Action<string> setStatus, Action<double> setAngle,
            string tag, CancellationToken ct)
        {
            Point2D? bestL = null, bestR = null;
            int bestM = 0;
            double bestBase = 0;

            for (int m = 1; m <= mMax; m++)
            {
                ct.ThrowIfCancellationRequested();
                double off = m * pitchStep;

                // 좌측(−m) 측정
                setStatus($"{tag} — 대칭 쌍 ±{m} 좌측 측정 (baseline {2 * off:F1}mm)...");
                await _sequenceService.RelativeMotionsMove(XAxis, -off, ct);
                var l = await measureAbs(ct);

                // 우측(+m) 측정
                setStatus($"{tag} — 대칭 쌍 ±{m} 우측 측정 (baseline {2 * off:F1}mm)...");
                await _sequenceService.RelativeMotionsMove(XAxis, 2 * off, ct);
                var r = await measureAbs(ct);

                // 피벗(중심)으로 복귀
                await _sequenceService.RelativeMotionsMove(XAxis, -off, ct);

                if (l == null || r == null)
                {
                    _logger.Information("{Tag} — 대칭 쌍 ±{M} 한쪽 미검출(웨이퍼 끝), 확장 종료", tag, m);
                    break;   // 끝 도달 → 지금까지 가장 넓은 쌍 사용
                }

                bestL = l; bestR = r; bestM = m; bestBase = 2 * off;
            }

            if (bestM == 0 || bestL == null || bestR == null)
            {
                setStatus($"{tag} — 유효 대칭 쌍 없음, θ 보정 생략");
                _logger.Warning("{Tag} — 유효 대칭 쌍 없음(mMax={M})", tag, mMax);
                return false;
            }

            // 최장 baseline 기울기 (±90° 정규화)
            double angleDeg = Math.Atan2(bestR.Y - bestL.Y, bestR.X - bestL.X) * (180.0 / Math.PI);
            if (angleDeg > 90) angleDeg -= 180;
            else if (angleDeg < -90) angleDeg += 180;
            setAngle(angleDeg);

            if (Math.Abs(angleDeg) < minDeg)
            {
                setStatus($"{tag} — 기울기 {angleDeg:F4}° < 임계 {minDeg:F4}°, 보정 생략 (baseline {bestBase:F1}mm, ±{bestM})");
                _logger.Information("{Tag} — 기울기 {A:F4}° 임계 미만, 보정 생략 (baseline {B:F2}mm, ±{M})",
                    tag, angleDeg, bestBase, bestM);
                return true;
            }

            double corr = ThetaSign * angleDeg;
            setStatus($"{tag} — 기울기 {angleDeg:F4}° → W_T {-corr:F4}° 회전 (baseline {bestBase:F1}mm, ±{bestM})");
            _logger.Information("{Tag} — θ 1회 보정: 기울기={A:F4}°, W_T={C:F4}°, baseline={B:F2}mm, ±{M}",
                tag, angleDeg, -corr, bestBase, bestM);
            await _sequenceService.RelativeMotionsMove(ThetaAxis, -corr, ct);
            return true;
        }

        // ── 3차: 저배 Center → 고배율 Center로 전환 ──
        //  Scribe 측정 위치가 아니라 웨이퍼의 저배 Center(CenterX/Y)를 기준으로,
        //  저배→고배 센터 오프셋(ShankLowOffset + HcCenterError)을 더한 절대좌표로 이동한다.
        //  규칙1에 따라 Z(h_z→H_Z)를 먼저 고배 위치로 이동한 뒤 X/Y를 이동한다.
        [RelayCommand]
        private async Task FindCenterStep3()
        {
            if (IsAligning) return;
            if (!HasCoarseCenter && !HasScribeMeasure)
            {
                ScribeCenterStatus = "저배 Center 미산출 — 1차(또는 2차)를 먼저 수행하세요.";
                return;
            }

            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                double shankLowX = _ecParamService.GetDouble("ShankLowOffsetX");
                double shankLowY = _ecParamService.GetDouble("ShankLowOffsetY");
                double hcErrX = await GetRecipeSafe("HcCenterErrorX");
                double hcErrY = await GetRecipeSafe("HcCenterErrorY");

                // 저배 Center 기준 고배 Center 절대좌표 (= 저배 Center + ShankLowOffset + HcCenterError)
                double targetHX = CenterX + shankLowX + hcErrX;
                double targetWY = CenterY + shankLowY + hcErrY;

                // 규칙1: Z 먼저(h_z→H_Z) 고배 위치로
                ScribeCenterStatus = "고배 Z 이동 중...";
                await MoveZForHighMagAsync(ct);

                // 저배 Center → 고배 Center 절대 이동 (X/Y 전환)
                ScribeCenterStatus = $"저배 Center에서 고배 Center로 전환 중... ({targetHX:F4},{targetWY:F4})";
                _logger.Information("Wafer 중심 3차 — 저배 Center=({CX:F4},{CY:F4}) → 고배 Center=({TX:F4},{TY:F4})",
                    CenterX, CenterY, targetHX, targetWY);
                await Task.WhenAll(
                    _sequenceService.MotionsMove(XAxis, targetHX, ct),
                    _sequenceService.MotionsMove(YAxis, targetWY, ct));

                ScribeCenterStatus = $"3차 완료 — 고배율 Center 전환 ({targetHX:F4},{targetWY:F4})";
                _logger.Information("Wafer 중심 3차 — 고배 전환 완료");
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "고배 전환 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "고배 전환 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        // ── 4차: 고배율 카메라(HC1)로 Align Mark 기반 Theta 보정 (Shift 불필요) ──
        //  규칙1에 따라 고배 Z(h_z→H_Z)를 먼저 이동한 뒤, 인접 Die AlignMark 기울기를
        //  0으로 맞추는 교대 스윕(정/역 번갈아, WaferSize 자동 범위)을 수행한다.
        //  ※ 스크라이브가 아닌 AlignMark를 사용하며 별도 Shift는 하지 않는다.
        [RelayCommand]
        private async Task FindCenterStep4()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                double pitch = DieSizeX + GapX;
                int stepDies = Math.Max(1, ThetaShiftDies);
                double pitchStep = pitch * stepDies;
                if (pitchStep <= 0)
                {
                    ScribeCenterStatus = "DieSize/Gap 값이 유효하지 않습니다.";
                    return;
                }

                // 규칙1: 고배 Z 먼저(h_z→H_Z)
                ScribeCenterStatus = "고배 Z 이동 중...";
                await MoveZForHighMagAsync(ct);

                // 시작 AlignMark 측정(피벗 = 현재=중심) — 좌우 확장 범위 산출용
                ScribeCenterStatus = "고배 시작 AlignMark 측정 중...";
                var startAbs = await MeasureHc1AlignAbsAsync(ct) ?? await SearchAlignByYAsync(ct);
                if (startAbs == null)
                {
                    ScribeCenterStatus = "4차 중단 — 시작 AlignMark 미검출(수동 조정 후 재실행)";
                    return;
                }

                // 좌우 대칭 확장 가능한 최대 배수(WaferSize·기준 위치 기반, 양쪽 중 작은 값)
                int mMax = Math.Min(
                    MaxStepsWithinWafer(startAbs, +1, stepDies, true),
                    MaxStepsWithinWafer(startAbs, -1, stepDies, true));
                _logger.Information("Wafer 중심 4차 — 대칭 확장 최대 ±{M} 스텝(WaferSize={W})", mMax, WaferSize);

                // 대칭 쌍 θ 보정 — 안쪽→바깥 확장, 검출되는 가장 바깥(최장 baseline)에서 1회 보정
                await CorrectThetaBySymmetricSweepAsync(
                    mMax, pitchStep, ThetaMinDeg,
                    async c => await MeasureHc1AlignAbsAsync(c) ?? await SearchAlignByYAsync(c),
                    s => ThetaStatus = s, a => ThetaAngleDeg = a,
                    "4차 θ", ct);

                ScribeCenterStatus = $"4차 완료 — 고배 AlignMark θ 보정, 최종 기울기 {ThetaAngleDeg:F4}°";
                _logger.Information("Wafer 중심 4차 완료 — 고배 AlignMark θ, 기울기={A:F4}°", ThetaAngleDeg);

                // 4차(및 전체 시퀀스) 완료 → Die 위치 전체 자동 재계산
                if (HasScribeMeasure || HasCoarseCenter)
                    await ComputeDiePositions();
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "4차 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "고배 AlignMark Theta 보정 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
        }

        // ── 전체 시퀀스: 1차 → 2차 → 3차 → 4차 순차 실행 ──
        //  각 단계는 자체 busy/취소를 관리한다. 단계 실패(1차 미검출·2차 미측정) 또는
        //  취소가 감지되면 이후 단계로 진행하지 않고 중단한다.
        [RelayCommand]
        private async Task RunFullSequence()
        {
            if (IsAligning) return;

            bool Cancelled() => _alignCts?.IsCancellationRequested == true;

            ScribeCenterStatus = "전체 시퀀스(1~4) 시작...";
            _logger.Information("Wafer 중심 전체 시퀀스 시작");

            // 1차: 저배 3점 중심
            await FindCenterStep1();
            if (!HasCoarseCenter || Cancelled())
            {
                ScribeCenterStatus = "전체 시퀀스 중단 — 1차 실패/취소";
                return;
            }

            // 2차: 저배 Scribe Theta
            await FindCenterStep2();
            if (!HasScribeMeasure || Cancelled())
            {
                ScribeCenterStatus = "전체 시퀀스 중단 — 2차 실패/취소";
                return;
            }

            // 3차: 고배 전환
            await FindCenterStep3();
            if (Cancelled())
            {
                ScribeCenterStatus = "전체 시퀀스 중단 — 3차 취소";
                return;
            }

            // 4차: 고배 Theta
            await FindCenterStep4();
            if (Cancelled())
            {
                ScribeCenterStatus = "전체 시퀀스 중단 — 4차 취소";
                return;
            }

            ScribeCenterStatus = "전체 시퀀스(1~4) 완료";
            _logger.Information("Wafer 중심 전체 시퀀스 완료");
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

        private const CameraType Hc1Cam = CameraType.HC1_HIGH;   // W-Table 좌측
        private const CameraType Hc2Cam = CameraType.HC2_HIGH;   // W-Table 우측
        private const MarkType AlignMark = MarkType.ALIGN_MARK;
        private const string ThetaAxis = MotionExtensions.W_T;   // 웨이퍼 테타 축
        // W_T 회전 부호 (하드웨어 방향과 반대면 +1로 뒤집기)
        private const double ThetaSign = -1.0;

        [ObservableProperty] private string thetaStatus = "-";
        [ObservableProperty] private double thetaAngleDeg;       // 마지막 측정 기울기(°)
        [ObservableProperty] private int thetaShiftDies = 1;     // 스텝당 이동 Die 수(피치 배수)
        // ※ 편도 최대 스텝은 WaferSize·기준 위치로 자동 산출한다(MaxStepsWithinWafer).
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
                double pitch = DieSizeX + GapX;                    // X 방향 Die 피치
                int stepDies = Math.Max(1, ThetaShiftDies);        // 스텝당 이동 Die 수
                double pitchStep = pitch * stepDies;
                if (pitchStep <= 0)
                {
                    ThetaStatus = "DieSize/Gap 값이 유효하지 않습니다.";
                    return;
                }

                // 시작 AlignMark 측정(피벗 = 현재 위치) — 좌우 확장 범위 산출용
                ThetaStatus = "시작 AlignMark 측정 중...";
                var startAbs = await MeasureHc1AlignAbsAsync(ct) ?? await SearchAlignByYAsync(ct);
                if (startAbs == null)
                {
                    ThetaStatus = "시작 AlignMark 미검출 — H_X/W_Y/W_T로 마크가 보이는 위치로 이동 후 다시 실행하세요.";
                    _logger.Warning("Theta 보정 — 시작 AlignMark 미검출(수동 조정 필요)");
                    return;
                }

                // 좌우 대칭 확장 가능한 최대 배수(양쪽 중 작은 값)
                int mMax = Math.Min(
                    MaxStepsWithinWafer(startAbs, +1, stepDies, true),
                    MaxStepsWithinWafer(startAbs, -1, stepDies, true));

                // 대칭 쌍 θ 보정 — 안쪽→바깥 확장, 검출되는 가장 바깥(최장 baseline)에서 1회 보정
                bool ok = await CorrectThetaBySymmetricSweepAsync(
                    mMax, pitchStep, ThetaMinDeg,
                    async c => await MeasureHc1AlignAbsAsync(c) ?? await SearchAlignByYAsync(c),
                    s => ThetaStatus = s, a => ThetaAngleDeg = a,
                    "θ 보정", ct);
                if (!ok)
                {
                    ThetaStatus = "유효 대칭 쌍 없음 — 중심 부근에 마크가 보이는 위치로 이동 후 다시 실행하세요.";
                    return;
                }

                ThetaStatus = $"Theta 보정 완료 — 대칭 쌍(최장 baseline), 최종 기울기 {ThetaAngleDeg:F4}°";
                _logger.Information("Theta 보정 완료 — 대칭 쌍 1회, 최종 기울기={A:F4}°", ThetaAngleDeg);
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
        /// 시작 위치(startAbs)에서 sweep 방향(dir=+1:+X, -1:-X)으로 웨이퍼 가장자리(가장 바깥 Die)까지
        /// 남은 Die 수를 스텝당 이동 Die 수(stepDies)로 나눠, 화면(웨이퍼) 밖으로 벗어나지 않는
        /// 최대 스텝 수를 계산한다. DieList(웨이퍼 맵)가 있으면 해당 Row의 Col 범위로 정확히,
        /// 없으면 웨이퍼 반경(Die 단위)으로 근사한다.
        /// </summary>
        private int MaxStepsWithinWafer(Point2D startAbs, int dir, int stepDies,
                                 bool highMag = false)
        {
            double pitchX = DieSizeX + GapX;
            if (pitchX <= 0) return 0;

            double halfCol = (WaferSize - 1) / 2.0;
            double absX = highMag ? startAbs.X - _highOffsetX : startAbs.X;
            int startCol = (int)Math.Round((absX - CenterX) / pitchX + halfCol);

            // 반경(Die 단위) 기반 근사 — DieList/센터 정보가 없을 때의 안전 상한
            int radiusDies = (int)Math.Floor((WaferSize + 1) / 2.0);
            int availableDies = radiusDies;

            if (DieList != null && DieList.Count > 0)
            {
                double pitchY = DieSizeY + GapY;
                double halfRow = (WaferSize - 1) / 2.0;
                double absY = highMag ? startAbs.Y - _highOffsetY : startAbs.Y;
                int startRow = pitchY > 0
                    ? (int)Math.Round(halfRow - (absY - CenterY) / pitchY)
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
