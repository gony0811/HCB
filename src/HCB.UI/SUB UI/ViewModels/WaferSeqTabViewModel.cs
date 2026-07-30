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

        [ObservableProperty] private bool isAligning;          // 측정/시프트 진행 중 busy 플래그
        [ObservableProperty] private string scribeCenterStatus = "-";
        [ObservableProperty] private double scribeOffsetXUm;   // 카메라 중심 → 스크라이브 교차점 오프셋(μm)
        [ObservableProperty] private double scribeOffsetYUm;
        [ObservableProperty] private double scribeAbsX;        // 스크라이브 교차점 절대(스테이지) 좌표
        [ObservableProperty] private double scribeAbsY;
        [ObservableProperty] private double scribeAbsT;
        [ObservableProperty] private bool hasScribeMeasure;    // 2차 측정 완료 여부(3차 활성화용)

        private CancellationTokenSource? _alignCts;

        private void OnInterlockActivated()
        {
            try { _alignCts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        // ── 1차: 저배율 3점 측정 (미구현 — 측정 방법 미정이라 스킵) ──
        [RelayCommand]
        private void FindCenterStep1()
        {
            ScribeCenterStatus = "1차(저배율 3점) 측정 방법 미정 — 스킵";
            _logger.Information("Wafer 중심 1차(저배율 3점) 스킵 — 측정 방법 미구현");
        }

        // ── 2차: 저배율(HC_LOW) Scribeline(교차점) 측정 ──
        //  카메라가 이미 Scribeline 위에 있다고 가정하고 현재 위치에서 1회 촬상.
        //  HC_LOW는 피사계 심도가 커서 AF 불필요(Vision 회신 규약).
        [RelayCommand]
        private async Task FindCenterStep2()
        {
            if (IsAligning) return;
            IsAligning = true;
            _alignCts = new CancellationTokenSource();
            var ct = _alignCts.Token;

            try
            {
                ScribeCenterStatus = "저배율 Scribeline 측정 중...";
                _logger.Information("Wafer 중심 2차 — HC_LOW Scribeline 측정 시작");

                var r = await _communication.RequestVisionMarkPosition(MarkType.DIE_CENTER_BOTTOM, CameraType.HC_LOW, "");
                if (r == null || r.Result == Result.NG)
                {
                    HasScribeMeasure = false;
                    ScribeCenterStatus = "Scribeline 측정 실패(NG)";
                    _logger.Warning("HC_LOW Scribeline 측정 NG");
                    return;
                }

                double curHX = await _sequenceService.GetCurrentPosition(XAxis, ct);
                double curWY = await _sequenceService.GetCurrentPosition(YAxis, ct);
                double curWT = await _sequenceService.GetCurrentPosition(TAxis, ct);

                // 스크라이브 교차점 절대좌표 = 현재 스테이지 − 카메라→교차점 오프셋
                ScribeAbsX = curHX - r.X;
                ScribeAbsY = curWY - r.Y;
                ScribeAbsT = curWY - r.Theta;
                ScribeOffsetXUm = r.X * 1000.0;
                ScribeOffsetYUm = r.Y * 1000.0;
                HasScribeMeasure = true;

                ScribeCenterStatus =
                    $"측정 완료 — 오프셋=({ScribeOffsetXUm:F1},{ScribeOffsetYUm:F1})μm, " +
                    $"교차점=({ScribeAbsX:F4},{ScribeAbsY:F4})";
                _logger.Information(
                    "HC_LOW Scribeline 측정 완료 — offset=({X:F4},{Y:F4}), abs=({AX:F4},{AY:F4})",
                    r.X, r.Y, ScribeAbsX, ScribeAbsY);
            }
            catch (OperationCanceledException) { ScribeCenterStatus = "측정 중단됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Scribeline 측정 오류");
                ScribeCenterStatus = $"오류: {e.Message}";
            }
            finally { IsAligning = false; }
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
                    _sequenceService.MotionsMove(YAxis, targetWY, ct),
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
    }
}
