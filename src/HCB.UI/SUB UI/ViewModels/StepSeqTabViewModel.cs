using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Telerik.Windows.Persistence.Core;
using static HCB.UI.SequenceService;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class StepSeqTabViewModel : ObservableObject
    {
        // ── 의존성 ────────────────────────────────────────────
        private readonly ILogger _logger;
        private readonly SequenceHelper _sequenceHelper;
        private readonly SequenceService _sequenceService;
        private readonly DeviceManager _deviceManager;
        private readonly RecipeService _recipeService;
        private readonly ECParamService _ecParamService;
        private readonly IOManager _ioManager;
        private readonly SettingsViewModel _settings;

        public RecipeService RecipeService => _recipeService;
        public SettingsViewModel Settings => _settings;

        // 사용 레시피의 Component가 WAFER로 바뀌면 발생 (DIE면 현재 화면 유지, WAFER면 화면 전환 요청)
        public event Action<ComponentType> RecipeComponentChanged;

        // ── CancellationToken ─────────────────────────────────
        private CancellationTokenSource _cts;

        private AlignData hcbData;
        private readonly Dictionary<string, Stopwatch> _sw = new();
        private readonly DispatcherTimer _elapsedTimer;

        // ── Die 번호 ──────────────────────────────────────────
        [ObservableProperty] private int topDie = 1;
        [ObservableProperty] private int bottomDie = 1;

        // ── Low Align 비전 결과 ───────────────────────────────
        [ObservableProperty] private VisionMarkPositionResponse visionBtmLowAlign;
        [ObservableProperty] private VisionMarkPositionResponse visionTopLowAlign;

        // ── High Align 비전 결과 (UI 표시용) ─────────────────
        [ObservableProperty] private VisionMarkResult topRightAlign;
        [ObservableProperty] private VisionMarkResult topRightFid;
        [ObservableProperty] private VisionMarkResult topLeftAlign;
        [ObservableProperty] private VisionMarkResult topLeftFid;

        [ObservableProperty] private Point2D btmRightAlign;
        [ObservableProperty] private Point2D btmRightFid;
        [ObservableProperty] private Point2D btmLeftAlign;
        [ObservableProperty] private Point2D btmLeftFid;

        // ── Offset 표시용 ─────────────────────────────────────
        [ObservableProperty] private double topAlignRelOffsetX;
        [ObservableProperty] private double topAlignRelOffsetY;
        [ObservableProperty] private double topAlignRelOffsetT;

        [ObservableProperty] private double topOffsetX;
        [ObservableProperty] private double topOffsetY;
        [ObservableProperty] private double topOffsetT;

        [ObservableProperty] private double btmOffsetX;
        [ObservableProperty] private double btmOffsetY;
        [ObservableProperty] private double btmOffsetT;

        // ── 기타 UI ───────────────────────────────────────────
        [ObservableProperty] private RecipeDto selectedRecipe;
        [ObservableProperty] private bool isInitInfoOpen;

        [ObservableProperty]
        private ObservableCollection<BondingDataPoint> bondingHistory
            = new ObservableCollection<BondingDataPoint>();

        // ── Step Lamp States ──────────────────────────────────
        [ObservableProperty] private StepState initState = StepState.Idle;
        [ObservableProperty] private StepState dieLoadState = StepState.Idle;
        [ObservableProperty] private StepState waferLoadState = StepState.Idle;
        [ObservableProperty] private StepState recipeSelectState = StepState.Idle;

        [ObservableProperty] private StepState btmLowAlignState = StepState.Idle;
        [ObservableProperty] private StepState btmPickupState = StepState.Idle;
        [ObservableProperty] private StepState btmHighAlignState = StepState.Idle;
        [ObservableProperty] private StepState btmPlaceState = StepState.Idle;

        [ObservableProperty] private StepState topLowAlignState = StepState.Idle;
        [ObservableProperty] private StepState topPickupState = StepState.Idle;
        [ObservableProperty] private StepState topHighAlignState = StepState.Idle;
        [ObservableProperty] private StepState topCorrState = StepState.Idle;
        [ObservableProperty] private StepState topBondingState = StepState.Idle;

        // ── Step Elapsed Time ────────────────────────────────
        [ObservableProperty] private string initElapsed = "";
        [ObservableProperty] private string btmFullElapsed = "";
        [ObservableProperty] private string btmLowAlignElapsed = "";
        [ObservableProperty] private string btmPlaceElapsed = "";
        [ObservableProperty] private string topFullElapsed = "";
        [ObservableProperty] private string topFullExMeasureElapsed = "";
        [ObservableProperty] private string topLowAlignElapsed = "";
        [ObservableProperty] private string topHighAlignElapsed = "";
        [ObservableProperty] private string btmHighAlignElapsed = "";
        [ObservableProperty] private string topCorrElapsed = "";
        [ObservableProperty] private string topBondingElapsed = "";

        [ObservableProperty] private double hzPosition;
        [ObservableProperty] private double detailX;
        [ObservableProperty] private double detailY;
        [ObservableProperty] private double detailT;

        // ── FidAF 측정 설정 ───────────────────────────────────
        [ObservableProperty] private int fidAfRepeatCount = 10;
        [ObservableProperty] private double fidAfIntervalSeconds = 1.0;

        // ── 반복 진행 상태 ────────────────────────────────────
        [ObservableProperty] private bool isRepeatRunning;
        [ObservableProperty] private int repeatCurrent;
        [ObservableProperty] private int repeatTotal;

        // ── HighResult 회전 보정 결과 ─────────────────────────
        [ObservableProperty] private double hrBlX, hrBlY, hrBrX, hrBrY;
        [ObservableProperty] private double hrTlX, hrTlY, hrTrX, hrTrY;

        [ObservableProperty] private VernierResult vernierResult;
        [ObservableProperty] private ObservableCollection<VernierRow> vernierRows = new();
        [ObservableProperty] private bool avgMode = true;
        [ObservableProperty] private bool use2DMapping = true;
        [ObservableProperty] private bool measureVernierAfterBonding = false;
        [ObservableProperty] private TracingMode tracingMode = TracingMode.Manual;
        [ObservableProperty] private bool useBtmIndividualMeasure = true;
        [ObservableProperty] private bool useFiducialTracking = true;

        // ── 피듀셜 각도 추적 결과 ────────────────────────────
        [ObservableProperty] private double fiducialPcAngle;
        [ObservableProperty] private double fiducialHcAngle;
        [ObservableProperty] private double fiducialWaferAngle;

        // ── 재측정(보정 후 P-TABLE 복귀) 결과 ─────────────────
        //   이번 사이클에서 재측정을 수행하면 그 측정 데이터 전체를 보관하고,
        //   본딩 데이터 CSV에 별도 행(Kind=ReMeasure)으로 함께 기록한다. null 이면 미수행.
        private AlignData _reMeasureData;

        // ── CSV 저장 설정 ─────────────────────────────────────
        [ObservableProperty] private string csvVernierDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "결과 데이터");
        [ObservableProperty] private string csvVernierFileName = "버니어 측정 데이터_{date}.csv";
        [ObservableProperty] private string csvDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "데이터");
        [ObservableProperty] private string csvDataFileName = "bonding_hcb_{date}.csv";

        public ObservableCollection<IAxis> JitterAxes { get; } = new();
        public PowerPmacDevice Pmac { get; private set; }

        [RelayCommand]
        public void ChangeMeasureVernier() => MeasureVernierAfterBonding = !MeasureVernierAfterBonding;

        [RelayCommand]
        public void Change2DMapping() => Use2DMapping = !Use2DMapping;

        [RelayCommand]
        public void CycleTracingMode()
        {
            TracingMode = TracingMode switch
            {
                TracingMode.Auto => TracingMode.Manual,
                TracingMode.Manual => TracingMode.None,
                _ => TracingMode.Auto
            };
        }

        [RelayCommand]
        public void ChangeBtmMeasureMode() => UseBtmIndividualMeasure = !UseBtmIndividualMeasure;

        [RelayCommand]
        public void ChangeFiducialTracking() => UseFiducialTracking = !UseFiducialTracking;

        [RelayCommand]
        public async Task RunFiducialAngleTracking()
        {
            ResetCts();
            try
            {
                var result = await _sequenceService.FiducialAngleTracking(AvgMode, _cts.Token);
                FiducialPcAngle = result.PcAngleDeg;
                FiducialHcAngle = result.HcAngleDeg;
                FiducialWaferAngle = result.WaferAngleDeg;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { _logger.Error(e, "FiducialAngleTracking Failed"); }
        }

        [ObservableProperty] private bool isWTableMappingOn;
        [ObservableProperty] private bool isPTableMappingOn;

        [RelayCommand]
        private async Task ToggleWTableMapping()
        {
            try
            {
                if (IsWTableMappingOn)
                {
                    await _sequenceService.MappingOff();
                    IsWTableMappingOn = false;
                    _logger.Information("W Table 2D Mapping OFF");
                }
                else
                {
                    await _sequenceService.WTable2DMappingOn();
                    IsWTableMappingOn = true;
                    IsPTableMappingOn = false;
                    _logger.Information("W Table 2D Mapping ON");
                }
            }
            catch (Exception ex) { _logger.Error(ex, "W Table 2D Mapping 전환 실패"); }
        }

        [RelayCommand]
        private async Task TogglePTableMapping()
        {
            try
            {
                if (IsPTableMappingOn)
                {
                    await _sequenceService.MappingOff();
                    IsPTableMappingOn = false;
                    _logger.Information("P Table 2D Mapping OFF");
                }
                else
                {
                    await _sequenceService.PTable2DMappingOn();
                    IsPTableMappingOn = true;
                    IsWTableMappingOn = false;
                    _logger.Information("P Table 2D Mapping ON");
                }
            }
            catch (Exception ex) { _logger.Error(ex, "P Table 2D Mapping 전환 실패"); }
        }

        [RelayCommand]
        private async Task HeaderVacOff()
        {
            ResetCts();
            try
            {
                await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
                _logger.Information("Header Vacuum OFF 완료");
            }
            catch (Exception ex) { _logger.Error(ex, "Header Vacuum OFF 실패"); }
        }

        [RelayCommand]
        private async Task WaferVacOff()
        {
            ResetCts();
            try
            {
                await _sequenceHelper.WTableVacuum(1, eOnOff.Off, _cts.Token, 5000);
                _logger.Information("Wafer Vacuum OFF 완료");
            }
            catch (Exception ex) { _logger.Error(ex, "Wafer Vacuum OFF 실패"); }
        }

        public string RepeatProgressText =>
            IsRepeatRunning ? $"{RepeatCurrent} / {RepeatTotal}" : string.Empty;

        partial void OnRepeatCurrentChanged(int value) => OnPropertyChanged(nameof(RepeatProgressText));
        partial void OnRepeatTotalChanged(int value) => OnPropertyChanged(nameof(RepeatProgressText));

        // ── D-Table IO ────────────────────────────────────────
        public SequenceServiceVM SequenceServiceVM { get; }

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new();

        private readonly List<string> _dTableNameList = new()
        {
            "DIE 1","DIE 2","DIE 3","DIE 4","DIE 5","DIE 6","DIE 7","DIE 8","DIE 9",
        };

        private readonly List<string> _dIoNameList = new()
        {
            IoExtensions.DO_DTABLE_VAC_1_ON, IoExtensions.DO_DTABLE_VAC_2_ON,
            IoExtensions.DO_DTABLE_VAC_3_ON, IoExtensions.DO_DTABLE_VAC_4_ON,
            IoExtensions.DO_DTABLE_VAC_5_ON, IoExtensions.DO_DTABLE_VAC_6_ON,
            IoExtensions.DO_DTABLE_VAC_7_ON, IoExtensions.DO_DTABLE_VAC_8_ON,
            IoExtensions.DO_DTABLE_VAC_9_ON,
        };

        // ═════════════════════════════════════════════════════
        //  생성자
        // ═════════════════════════════════════════════════════

        public StepSeqTabViewModel(
            SequenceServiceVM sequenceServiceVM,
            SequenceService sequenceService,
            SequenceHelper sequenceHelper,
            DeviceManager deviceManager,
            IOManager ioManager,
            ECParamService eCParamService,
            RecipeService recipeService,
            SettingsViewModel settingsViewModel,
            ILogger logger)
        {
            _logger = logger.ForContext<StepSeqTabViewModel>();
            _settings = settingsViewModel;
            SequenceServiceVM = sequenceServiceVM;
            _sequenceService = sequenceService;
            _sequenceHelper = sequenceHelper;
            _deviceManager = deviceManager;
            _recipeService = recipeService;
            _ioManager = ioManager;
            _ecParamService = eCParamService;

            Pmac = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            if (Pmac != null)
            {
                foreach (var axis in Pmac.MotionList)
                    JitterAxes.Add(axis);
            }

            var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            if (ioDevice != null)
            {
                for (var i = 0; i < _dTableNameList.Count; i++)
                {
                    var vm = _ioManager.CreateIoVM(_dTableNameList[i], _dIoNameList[i], _dTableNameList[i]);
                    if (vm != null) DTableList.Add(vm);
                }
            }

            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _elapsedTimer.Tick += (_, _) => RefreshElapsed();
            _elapsedTimer.Start();

            _sequenceService.InterlockActivated += OnInterlockActivated;
        }

        private void OnInterlockActivated()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        // ═════════════════════════════════════════════════════
        //  STOP
        // ═════════════════════════════════════════════════════

        // 1. 플래그 추가
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool canStop = true;

        // 2. Stop 커맨드에 CanExecute 조건 추가
        [RelayCommand(CanExecute = nameof(CanStop))]
        public async Task Stop()
        {
            if (_cts == null || _cts.IsCancellationRequested) return;
            _cts.Cancel();
            await _sequenceService.StopAsync(CancellationToken.None);

            InitState = StepState.Idle;
            BtmLowAlignState = BtmPickupState = BtmHighAlignState = BtmPlaceState = StepState.Idle;
            TopLowAlignState = TopPickupState = TopHighAlignState = TopCorrState = TopBondingState = StepState.Idle;
        }
        private async Task RunNoStop(Func<Task> action)
        {
            CanStop = false;
            try { await action(); }
            finally { CanStop = true; }
        }
        // ═════════════════════════════════════════════════════
        //  INIT
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task Init()
        {
            ResetCts();
            try
            {
                SequenceServiceVM.ResetInitProgress();
                InitState = StepState.InProgress;
                await _sequenceService.MachineInitAsync(_cts.Token);
                InitState = StepState.Completed;
            }
            catch (OperationCanceledException) { InitState = StepState.Idle; }
            catch (Exception e) { InitState = StepState.Failed; _logger.Error(e, "Init Failed"); }
        }

        // ═════════════════════════════════════════════════════
        //  DIE LOAD / WAFER LOAD
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task DieLoad()
        {
            ResetCts();
            try
            {
                await _sequenceService.DTableLoading(_cts.Token);

                bool confirmed = false;
                List<int> topList = new(), botList = new();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var dialog = new VacuumSelector { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                    dialog.ShowDialog();
                    confirmed = dialog.DialogResult == true;
                    if (confirmed) { topList = dialog.TopDieVacuums; botList = dialog.BotDieVacuums; }
                });

                if (!confirmed) return;
                if (topList.Count > 0) TopDie = topList[0];
                if (botList.Count > 0) BottomDie = botList[0];

                _logger.Information("Die Load 선택 완료 — TOP: [{Top}]  BOT: [{Bot}]",
                    string.Join(", ", topList), string.Join(", ", botList));
            }
            catch (Exception e) { _logger.Error(e, "DieLoad Failed"); }
        }

        [RelayCommand]
        public async Task WaferLoad()
        {
            ResetCts();
            await _sequenceService.Init_Head(_cts.Token);
            await _sequenceService.MotionsMove(MotionExtensions.W_Y, 0, _cts.Token);
        }

        // ═════════════════════════════════════════════════════
        //  Info 팝업
        // ═════════════════════════════════════════════════════

        [RelayCommand] public void InitInfo() => IsInitInfoOpen = true;
        [RelayCommand] public void CloseInitInfo() => IsInitInfoOpen = false;

        [RelayCommand]
        public void OpenTopHighAlignInfo()
        {
            var (refTop, refBtm) = GetRefAlignDists();
            _ = RunDialogOnNewThread(() =>
                new AlignResultWindow(() => { _sequenceService.ComputeDistances(hcbData); return hcbData; }, refTop, refBtm)
                { Header = "정렬 결과 — 실시간", WindowStartupLocation = WindowStartupLocation.CenterScreen }
                .ShowDialog());
        }

        [RelayCommand]
        public void BtmHighAlignInfo()
        {
            var (refTop, refBtm) = GetRefAlignDists();
            _ = RunDialogOnNewThread(() =>
                new AlignResultWindow(() => { _sequenceService.ComputeDistances(hcbData); return hcbData; }, refTop, refBtm)
                { Header = "정렬 결과 — 실시간", WindowStartupLocation = WindowStartupLocation.CenterScreen }
                .ShowDialog());
        }

        [RelayCommand]
        public void TopHighAlignInfo()
        {
            var history = BondingHistory.ToList();
            _ = RunDialogOnNewThread(() =>
                new BondingInfoWindow(_recipeService, history)
                { WindowStartupLocation = WindowStartupLocation.CenterScreen }
                .ShowDialog());
        }

        // ═════════════════════════════════════════════════════
        //  BOTTOM 시퀀스 — 개별
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task BtmAlignPickup()
        {
            ResetCts();
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }
                BtmLowAlignState = StepState.InProgress;
                VisionBtmLowAlign = await _sequenceService.BtmLowMeasure(
                    BottomDie, MarkType.DIE_CENTER_BOTTOM, _cts.Token);
                await RunNoStop(() => _sequenceService.DTablePickup(DieType.BOTTOM, BottomDie, VisionBtmLowAlign, _cts.Token));
                BtmLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmLowAlignState = StepState.Idle; }
            catch (Exception e) { BtmLowAlignState = StepState.Failed; _logger.Error(e, "BtmAlignPickup Failed"); }
        }


        [RelayCommand]
        public async Task BtmPlace()
        {
            ResetCts();
            try
            {
                BtmPlaceState = StepState.InProgress;
                await _sequenceService.DieDrop(1, _cts.Token);
                BtmPlaceState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmPlaceState = StepState.Idle; }
            catch (Exception e) { BtmPlaceState = StepState.Failed; _logger.Error(e, "BtmPlace Failed"); }
        }

        [RelayCommand]
        public async Task BtmFullSequence()
        {
            ResetCts();
            var ct = _cts.Token;
            var total = Stopwatch.StartNew();
            TrackStep("BtmFull", StepState.InProgress);
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); total.Stop(); TrackStep("BtmFull", StepState.Idle); return; }

                BtmLowAlignState = StepState.InProgress;
                VisionBtmLowAlign = await _sequenceService.BtmLowMeasure(BottomDie, MarkType.DIE_CENTER_BOTTOM, ct);
                if (VisionBtmLowAlign == null) { _logger.Information("Bottom Die Align 실패"); total.Stop(); TrackStep("BtmFull", StepState.Idle); return; }
                await RunNoStop(() => _sequenceService.DTablePickup(DieType.BOTTOM, BottomDie, VisionBtmLowAlign, ct));
                BtmLowAlignState = StepState.Completed;

                BtmPlaceState = StepState.InProgress;
                await _sequenceService.DieDrop(1, ct);
                BtmPlaceState = StepState.Completed;

                TrackStep("BtmFull", StepState.Completed);
            }
            catch (OperationCanceledException)
            {
                BtmLowAlignState = IfInProgress(BtmLowAlignState, StepState.Idle);
                BtmPlaceState = IfInProgress(BtmPlaceState, StepState.Idle);
                TrackStep("BtmFull", StepState.Idle);
            }
            catch (Exception e)
            {
                BtmLowAlignState = IfInProgress(BtmLowAlignState, StepState.Failed);
                BtmPlaceState = IfInProgress(BtmPlaceState, StepState.Failed);
                TrackStep("BtmFull", StepState.Failed);
                _logger.Error(e, "BtmFullSequence Failed");
            }
        }

        // ═════════════════════════════════════════════════════
        //  TOP 시퀀스 — 개별 (6단계)
        //  1. 저배율 보정  2. Pickup  3. 고배율(Top)
        //  4. 고배율(Btm)  5. 보정    6. 본딩
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task TopAlignPickup()
        {
            ResetCts();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                TopLowAlignState = StepState.InProgress;
                // 회전중심 + 카메라 거리 측정 (Pickup 이전)
                hcbData = await _sequenceService.MeasureCamDistAndHcro(NewAlignData(), _cts.Token);
                VisionTopLowAlign = await _sequenceService.TopLowMeasure(TopDie, MarkType.DIE_CENTER_TOP, _cts.Token);
                await RunNoStop(() => _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, _cts.Token));
                TopLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopLowAlignState = StepState.Idle; }
            catch (Exception e) { TopLowAlignState = StepState.Failed; _logger.Error(e, "TopAlignPickup Failed"); }
        }


        [RelayCommand]
        public async Task TopHighAlign()
        {
            ResetCts();
            try
            {
                TopHighAlignState = StepState.InProgress;
                hcbData = await _sequenceService.TopHighAlign(hcbData ?? NewAlignData(), _cts.Token);
                UpdateTopMarks();
                TopHighAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopHighAlignState = StepState.Idle; }
            catch (Exception e) { TopHighAlignState = StepState.Failed; _logger.Error(e, "TopHighAlign Failed"); }
        }

        [RelayCommand]
        public async Task BtmHighAlign()
        {
            ResetCts();
            try
            {
                BtmHighAlignState = StepState.InProgress;
                hcbData = await _sequenceService.BtmHighAlign(hcbData, _cts.Token);
                await _sequenceService.CoordinateSystemIntegration(hcbData, _cts.Token);
                _sequenceService.ProcessMeasurement(hcbData, 1);
                _sequenceService.ProcessMeasurement(hcbData, 2);
                _sequenceService.ProcessMeasurement(hcbData, 3);
                UpdateBtmMarks();
                BtmHighAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmHighAlignState = StepState.Idle; }
            catch (Exception e) { BtmHighAlignState = StepState.Failed; _logger.Error(e, "BtmHighAlign Failed"); }
        }

        [RelayCommand]
        public async Task TopCorr()
        {
            ResetCts();
            try
            {
                TopCorrState = StepState.InProgress;
                await _sequenceService.BondingCorr(hcbData, _cts.Token);
                TopCorrState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopCorrState = StepState.Idle; }
            catch (Exception e) { TopCorrState = StepState.Failed; _logger.Error(e, "TopCorr Failed"); }
        }

        [RelayCommand]
        public async Task TopBonding()
        {
            ResetCts();
            try
            {
                TopBondingState = StepState.InProgress;
                
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                await RunNoStop(() => _sequenceService.BondingPress(BondingHistory, _cts.Token));
                TopBondingState = StepState.Completed;
                ExportHcbData();
            }
            catch (OperationCanceledException) { TopBondingState = StepState.Idle; }
            catch (Exception e) { TopBondingState = StepState.Failed; _logger.Error(e, "TopBonding Failed"); }
            
        }

        [RelayCommand]
        public async Task NoVacOffBonding()
        {
            ResetCts();
            try
            {
                TopBondingState = StepState.InProgress;
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                
                await RunNoStop(() => _sequenceService.BondingTest(BondingHistory, _cts.Token));
                ExportHcbData();
                TopBondingState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopBondingState = StepState.Idle; }
            catch (Exception e) { TopBondingState = StepState.Failed; _logger.Error(e, "TopBonding Failed"); }
        }

        // ═════════════════════════════════════════════════════
        //  AlignTest (반복 테스트)
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task AlignTest()
        {
            ResetCts();
            var ct = _cts.Token;
            try
            {
                // 1. 회전중심 + 카메라 거리 측정 (Pickup 이전, 1회) + 저배율 보정 + Pickup
                TopLowAlignState = StepState.InProgress;
                hcbData = await _sequenceService.MeasureCamDistAndHcro(NewAlignData(), ct);
                VisionTopLowAlign = await _sequenceService.TopLowMeasure(TopDie, MarkType.DIE_CENTER_TOP, ct);
                await RunNoStop(() => _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, ct));
                TopLowAlignState = StepState.Completed;

                // 2~4 반복 — 0단계 캘리브레이션 값 보존을 위해 hcbData 재사용
                for (int i = 0; i < 3000; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    TopHighAlignState = StepState.InProgress;
                    hcbData = await _sequenceService.TopHighAlign(hcbData, ct);
                    _sequenceService.ProcessMeasurement(hcbData, 1);
                    UpdateTopMarks();
                    TopHighAlignState = StepState.Completed;

                    BtmHighAlignState = StepState.InProgress;
                    hcbData = await _sequenceService.BtmHighAlign(hcbData, ct);
                    _sequenceService.ProcessMeasurement(hcbData, 3);
                    UpdateBtmMarks();
                    BtmHighAlignState = StepState.Completed;

                    if (!ValidateAlignDistances())
                        throw new Exception("Top/Btm 선분 길이 오차가 허용 범위를 초과했습니다.");

                    TopCorrState = StepState.InProgress;
                    await _sequenceService.CoordinateSystemIntegration(hcbData, ct);
                    _sequenceService.ProcessMeasurement(hcbData, 2);
                    TopCorrState = StepState.Completed;

                    ExportHcbData();
                }

                // 6. 본딩
                TopBondingState = StepState.InProgress;
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                await RunNoStop(async () =>
                {
                    await _sequenceService.BondingCorr(hcbData, ct);
                    await _sequenceService.BondingPress(BondingHistory, ct);
                });
                TopBondingState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopBondingState = StepState.Idle; }
            catch (Exception e) { TopBondingState = StepState.Failed; _logger.Error(e, "AlignTest Failed"); }
        }

        // ═════════════════════════════════════════════════════
        //  TOP Full (1→2→3→4→5→6)
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task TopRunFullSequence() => await RunTopFullSequence(null);

        // Wafer 본딩: BtmHighAlign 시 PLACE_CENTER 대신 클릭한 Die의 Center(고배 절대좌표)로 이동.
        // 그 외 단계는 TopRunFullSequence와 동일하게 진행한다.
        public async Task WaferBonding(Point2D dieCenter) => await RunTopFullSequence(dieCenter);

        private async Task RunTopFullSequence(Point2D placeCenter)
        {
            ResetCts();
            var ct = _cts.Token;
            TrackStep("TopFull", StepState.InProgress);
            TrackStep("TopFullExMeasure", StepState.InProgress);
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); TrackStep("TopFull", StepState.Idle); TrackStep("TopFullExMeasure", StepState.Idle); return; }

                // 이번 사이클 재측정 결과 초기화 (미수행 시 ReMeasure 행 미기록)
                _reMeasureData = null;

                // 1. 회전중심 + 카메라 거리 측정 (Pickup 이전) + 저배율 보정 + Pickup
                TopLowAlignState = StepState.InProgress;
                hcbData = await _sequenceService.MeasureCamDistAndHcro(NewAlignData(), ct);
                VisionTopLowAlign = await _sequenceService.TopLowMeasure(TopDie, MarkType.DIE_CENTER_TOP, ct);
                await RunNoStop(() => _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, ct));
                TopLowAlignState = StepState.Completed;

                // 2. 고배율 측정 (Top) — 0단계에서 측정한 캘리브레이션 값 보존을 위해 hcbData 재사용
                TopHighAlignState = StepState.InProgress;
                hcbData = await _sequenceService.TopHighAlign(hcbData, ct);
                _sequenceService.ProcessMeasurement(hcbData, 1);
                UpdateTopMarks();
                TopHighAlignState = StepState.Completed;

                // 4. 고배율 측정 (Btm) — placeCenter != null 이면 클릭한 Die Center로 이동
                BtmHighAlignState = StepState.InProgress;
                hcbData = await _sequenceService.BtmHighAlign(hcbData, ct, placeCenter);
                _sequenceService.ProcessMeasurement(hcbData, 3);
                UpdateBtmMarks();
                BtmHighAlignState = StepState.Completed;

                // 5. 보정
                TopCorrState = StepState.InProgress;
                await _sequenceService.CoordinateSystemIntegration(hcbData, ct);
                _sequenceService.ProcessMeasurement(hcbData, 2);
                await _sequenceService.BondingCorr(hcbData, ct);
                TopCorrState = StepState.Completed;

                if (!ValidateAlignDistances())
                    throw new Exception("Top/Btm 선분 길이 오차가 허용 범위를 초과했습니다.");

                // 5.5 재측정 (옵션) — 보정 직후 P-TABLE로 복귀해 재측정 → 잔차 기록 → 재보정
                if (Settings.ReMeasureAfterCorr)
                    await ReMeasure(placeCenter, ct);

                // 6. 본딩
                TopBondingState = StepState.InProgress;
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                await RunNoStop(() => _sequenceService.BondingPress(BondingHistory, ct));
                TopBondingState = StepState.Completed;
                TrackStep("TopFullExMeasure", StepState.Completed);
                
                // 7. 버니어 측정 (옵션)
                if (Settings.MeasureVernierAfterBonding)
                {
                    var vernier = await _sequenceService.GetVernier(ct);
                    double distX = double.Parse(_recipeService.FindByParam("버니어_거리_X").Value);
                    double distY = double.Parse(_recipeService.FindByParam("버니어_거리_Y").Value);
                    vernier.Preprocess(distX, distY);

                    VernierResult = vernier;
                    VernierRows.Clear();
                    var names = new[] { "1", "3" };
                    for (int i = 0; i < vernier.v1.Count; i++)
                    {
                        VernierRows.Add(new VernierRow
                        {
                            Name = i < names.Length ? names[i] : i.ToString(),
                            V1X = vernier.v1[i].X,
                            V1Y = vernier.v1[i].Y,
                            V3X = vernier.v3.Count > i ? vernier.v3[i].X : null,
                            V3Y = vernier.v3.Count > i ? vernier.v3[i].Y : null,
                        });
                    }
                    ExportHighResult();
                    _logger.Information(
                        "버니어 결과 — OffsetX: {X:F4}, OffsetY: {Y:F4}, OffsetT: {T:F4}",
                        vernier.OffsetX, vernier.OffsetY, vernier.OffsetT);
                }

                TrackStep("TopFull", StepState.Completed);
            }
            catch (OperationCanceledException)
            {
                TopLowAlignState = IfInProgress(TopLowAlignState, StepState.Idle);
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Idle);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Idle);
                TopCorrState = IfInProgress(TopCorrState, StepState.Idle);
                TopBondingState = IfInProgress(TopBondingState, StepState.Idle);
                TrackStep("TopFull", StepState.Idle);
                TrackStep("TopFullExMeasure", StepState.Idle);
            }
            catch (Exception e)
            {
                TopLowAlignState = IfInProgress(TopLowAlignState, StepState.Failed);
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Failed);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Failed);
                TopCorrState = IfInProgress(TopCorrState, StepState.Failed);
                TopBondingState = IfInProgress(TopBondingState, StepState.Failed);
                TrackStep("TopFull", StepState.Failed);
                TrackStep("TopFullExMeasure", StepState.Failed);
                _logger.Error(e, "TopRunFullSequence Failed");
            }finally
            {
                ExportHcbData(hcbData, "Bonding");
                if (_reMeasureData != null)
                    ExportHcbData(_reMeasureData, "ReMeasure");
            }
        }

        // ═════════════════════════════════════════════════════
        //  재측정 (보정 후 P-TABLE 복귀 → 재측정 → 재보정)
        // ═════════════════════════════════════════════════════

        /// <summary>
        /// 보정 완료 시점에 다시 P-TABLE로 복귀해 Top/Btm 고배율을 재측정하고,
        /// 남은 잔차(ResultX/Y/T)를 계산해 CSV용 필드에 기록한 뒤 한 번 더 BondingCorr로 재보정한다.
        /// 원본 측정 데이터(hcbData)는 훼손하지 않도록 복사본(reData)에서 수행한다.
        /// </summary>
        private async Task ReMeasure(Point2D placeCenter, CancellationToken ct)
        {
            // 캘리브레이션·모드 플래그를 그대로 이어받되 원본 마크는 보존
            var reData = hcbData.Clone();

            // Top 고배율 재측정 (P-TABLE 복귀) — 보정으로 회전된 H_T 유지 (원복 생략)
            TopHighAlignState = StepState.InProgress;
            reData = await _sequenceService.TopHighAlign(reData, ct, resetHt: false);
            _sequenceService.ProcessMeasurement(reData, 1);
            TopHighAlignState = StepState.Completed;

            // Btm 고배율 재측정
            BtmHighAlignState = StepState.InProgress;
            reData = await _sequenceService.BtmHighAlign(reData, ct, placeCenter);
            _sequenceService.ProcessMeasurement(reData, 3);
            BtmHighAlignState = StepState.Completed;

            // 좌표계 통합 → 잔차 계산
            await _sequenceService.CoordinateSystemIntegration(reData, ct);
            _sequenceService.ProcessMeasurement(reData, 2);

            // 재측정 측정 데이터 전체 보관 → CSV에 Kind=ReMeasure 행으로 기록
            _reMeasureData = reData;

            _logger.Information("재측정 잔차 — X={X:F6}, Y={Y:F6}, T={T:F6}",
                reData.ResultX, reData.ResultY, reData.ResultT);

            // 재보정 — 잔차만큼 축 이동
            await _sequenceService.BondingCorr(reData, ct);
        }

        // ═════════════════════════════════════════════════════
        //  HighResult (Vernier)
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task HighResult()
        {
            ResetCts();
            try
            {
                var result = await _sequenceService.GetVernier(_cts.Token);
                VernierResult = result;

                var names = new[] { "1", "3" };
                VernierRows.Clear();
                for (int i = 0; i < result.v1.Count; i++)
                {
                    VernierRows.Add(new VernierRow
                    {
                        Name = i < names.Length ? names[i] : i.ToString(),
                        V1X = result.v1[i].X,
                        V1Y = result.v1[i].Y,
                        V3X = result.v3.Count > i ? result.v3[i].X : null,
                        V3Y = result.v3.Count > i ? result.v3[i].Y : null,
                    });
                }
                ExportHighResult();
                _logger.Information("Vernier 측정 완료 — {Count}포인트", result.v1.Count);
            }
            catch (OperationCanceledException) { _logger.Information("Vernier 측정 정지됨"); }
            catch (Exception e) { _logger.Error(e, "Vernier 측정 실패"); }
        }


        public async Task AccuracyMode()
        {
            ResetCts();
            var result = await _sequenceService.GetVernier(_cts.Token);
        }

        // ═════════════════════════════════════════════════════
        //  Reset 메서드
        // ═════════════════════════════════════════════════════

        public async Task BtmInit()
        {
            ResetCts();
            await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
            BottomDie = 0;
            VisionBtmLowAlign = null;
            BtmLowAlignState = BtmPickupState = BtmPlaceState = StepState.Idle;
        }

        public async Task TopInit()
        {
            ResetCts();
            await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
            TopDie = 0;
            VisionTopLowAlign = null;
            TopLowAlignState = TopPickupState = TopHighAlignState
                             = TopCorrState = TopBondingState = StepState.Idle;
        }

        // ═════════════════════════════════════════════════════
        //  유틸
        // ═════════════════════════════════════════════════════

        private void ResetCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        private static StepState IfInProgress(StepState current, StepState next)
            => current == StepState.InProgress ? next : current;

        // ── Elapsed Time Tracking ─────────────────────────────

        private void TrackStep(string key, StepState state)
        {
            if (!_sw.TryGetValue(key, out var sw))
            {
                sw = new Stopwatch();
                _sw[key] = sw;
            }
            switch (state)
            {
                case StepState.InProgress: sw.Restart(); break;
                case StepState.Idle: sw.Reset(); break;
                default: sw.Stop(); break;
            }
            RefreshElapsed();
        }

        private void RefreshElapsed()
        {
            InitElapsed = FmtSw(_sw.GetValueOrDefault("Init"));
            BtmFullElapsed = FmtSw(_sw.GetValueOrDefault("BtmFull"));
            BtmLowAlignElapsed = FmtSw(_sw.GetValueOrDefault("BtmLowAlign"));
            BtmPlaceElapsed = FmtSw(_sw.GetValueOrDefault("BtmPlace"));
            TopFullElapsed = FmtSw(_sw.GetValueOrDefault("TopFull"));
            TopFullExMeasureElapsed = FmtSw(_sw.GetValueOrDefault("TopFullExMeasure"));
            TopLowAlignElapsed = FmtSw(_sw.GetValueOrDefault("TopLowAlign"));
            TopHighAlignElapsed = FmtSw(_sw.GetValueOrDefault("TopHighAlign"));
            BtmHighAlignElapsed = FmtSw(_sw.GetValueOrDefault("BtmHighAlign"));
            TopCorrElapsed = FmtSw(_sw.GetValueOrDefault("TopCorr"));
            TopBondingElapsed = FmtSw(_sw.GetValueOrDefault("TopBonding"));
        }

        private static string FmtSw(Stopwatch sw) =>
            sw != null && sw.ElapsedMilliseconds > 0 ? sw.Elapsed.ToString(@"mm\:ss\.f") : "";

        partial void OnInitStateChanged(StepState value) => TrackStep("Init", value);
        partial void OnBtmLowAlignStateChanged(StepState value) => TrackStep("BtmLowAlign", value);
        partial void OnBtmPlaceStateChanged(StepState value) => TrackStep("BtmPlace", value);
        partial void OnTopLowAlignStateChanged(StepState value) => TrackStep("TopLowAlign", value);
        partial void OnTopHighAlignStateChanged(StepState value) => TrackStep("TopHighAlign", value);
        partial void OnBtmHighAlignStateChanged(StepState value) => TrackStep("BtmHighAlign", value);
        partial void OnTopCorrStateChanged(StepState value) => TrackStep("TopCorr", value);
        partial void OnTopBondingStateChanged(StepState value) => TrackStep("TopBonding", value);

        private static Task RunDialogOnNewThread(Action dialogAction)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { dialogAction(); tcs.SetResult(true); }
                catch (Exception ex) { tcs.SetException(ex); }
                finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        // ═════════════════════════════════════════════════════
        //  UI 바인딩 헬퍼
        // ═════════════════════════════════════════════════════

        private AlignData NewAlignData() => new AlignData
        {
            AvgMove = Settings.AvgMode,
            Use2DMapping = Settings.Use2DMapping,
            TracingMode = Settings.TracingMode,
            UseBtmIndividualMeasure = Settings.BtmIndividualMeasure,
            UseFiducialTracking = Settings.FiducialTracing,
            UseRightFidSimilarity = Settings.RightFidSimilarity
        };

        private void UpdateTopMarks()
        {
            if (hcbData == null) return;
            TopRightFid = hcbData.TopRightFidRaw;
            TopRightAlign = hcbData.TopRightAlignRaw;
            TopLeftFid = hcbData.TopLeftFidRaw;
            TopLeftAlign = hcbData.TopLeftAlignRaw;
        }

        private void UpdateBtmMarks()
        {
            if (hcbData == null) return;
            BtmRightFid = hcbData.BtmRightFidRaw;
            BtmRightAlign = hcbData.BtmRightAlignRaw;
            BtmLeftFid = hcbData.BtmLeftFidRaw;
            BtmLeftAlign = hcbData.BtmLeftAlignRaw;
        }

        // ViewModel - GetRefAlignDist
        private (double refTop, double refBtm) GetRefAlignDists()
        {
            double refTop = double.NaN, refBtm = double.NaN;
            var recipe = _recipeService?.UseRecipe;
            if (recipe != null)
            {
                var pt = recipe.ParamList.FirstOrDefault(p => p.Name == "RefTopAlignDist");
                var pb = recipe.ParamList.FirstOrDefault(p => p.Name == "RefBtmAlignDist");
                if (pt != null && double.TryParse(pt.Value, out double t)) refTop = t;
                if (pb != null && double.TryParse(pb.Value, out double b)) refBtm = b;
            }
            return (refTop, refBtm);
        }

        // ═════════════════════════════════════════════════════
        //  선분 길이 오차 검증
        // ═════════════════════════════════════════════════════

        private bool ValidateAlignDistances()
        {
            if (hcbData == null) return true;
            if (hcbData.TopAlignDist == 0 || hcbData.BtmAlignDist == 0) return true;

            var param = _ecParamService.FindByName("AlignDistTolerance");
            if (string.IsNullOrEmpty(param?.Value) || !double.TryParse(param.Value, out double tolerance) || tolerance <= 0)
                return true;

            double diff = Math.Abs(hcbData.TopAlignDist - hcbData.BtmAlignDist);
            if (diff > tolerance)
            {
                _logger.Warning(
                    "선분 길이 오차 초과 — TopAlign: {Top:F4}mm, BtmAlign: {Btm:F4}mm, 차이: {Diff:F4}mm, 허용: {Tol:F4}mm",
                    hcbData.TopAlignDist, hcbData.BtmAlignDist, diff, tolerance);
                return false;
            }

            _logger.Information(
                "선분 길이 검증 통과 — TopAlign: {Top:F4}mm, BtmAlign: {Btm:F4}mm, 차이: {Diff:F4}mm, 허용: {Tol:F4}mm",
                hcbData.TopAlignDist, hcbData.BtmAlignDist, diff, tolerance);
            return true;
        }

        // ═════════════════════════════════════════════════════
        //  CSV 내보내기
        // ═════════════════════════════════════════════════════

        public void ExportHighResult()
        {
            if (VernierRows.Count == 0)
            {
                _logger.Information("저장할 Vernier 결과가 없습니다.");
                return;
            }
            Directory.CreateDirectory(Settings.CsvVernierDir);
            var path = Settings.ResolveCsvPath(Settings.CsvVernierDir, Settings.CsvVernierFileName);

            bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            var sb = new StringBuilder();
            if (writeHeader) sb.AppendLine("Time,Pos,V1_X,V1_Y,V3_X,V3_Y");

            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            foreach (var row in VernierRows)
                sb.AppendLine($"{ts},{row.Name},{Fn(row.V1X)},{Fn(row.V1Y)},{Fn(row.V3X)},{Fn(row.V3Y)}");

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            _logger.Information("Vernier CSV 저장: {Path}", path);
        }

        private void ExportHcbData() => ExportHcbData(hcbData, "Bonding");

        // kind: 행 구분 태그 ("Bonding" = 본딩 측정, "ReMeasure" = 보정 후 재측정)
        private void ExportHcbData(AlignData data, string kind)
        {
            if (data == null) return;

            Directory.CreateDirectory(Settings.CsvDataDir);
            var path = Settings.ResolveCsvPath(Settings.CsvDataDir, Settings.CsvDataFileName);

            _sequenceService.ComputeDistances(data);

            bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            var sb = new StringBuilder();

            if (writeHeader)
            {
                sb.AppendLine(string.Join(",",
                    "Time", "AvgMode",
                    "TopRF_StageX", "TopRF_StageY", "TopRF_DxCam", "TopRF_DyCam", "TopRF_CenterX", "TopRF_CenterY",
                    "TopRA_StageX", "TopRA_StageY", "TopRA_DxCam", "TopRA_DyCam", "TopRA_CenterX", "TopRA_CenterY",
                    "TopLF_StageX", "TopLF_StageY", "TopLF_DxCam", "TopLF_DyCam", "TopLF_CenterX", "TopLF_CenterY",
                    "TopLA_StageX", "TopLA_StageY", "TopLA_DxCam", "TopLA_DyCam", "TopLA_CenterX", "TopLA_CenterY",
                    "BtmRF_StageX", "BtmRF_StageY", "BtmRF_DxCam", "BtmRF_DyCam", "BtmRF_CenterX", "BtmRF_CenterY",
                    "BtmRA_StageX", "BtmRA_StageY", "BtmRA_DxCam", "BtmRA_DyCam", "BtmRA_CenterX", "BtmRA_CenterY",
                    "BtmLF_StageX", "BtmLF_StageY", "BtmLF_DxCam", "BtmLF_DyCam", "BtmLF_CenterX", "BtmLF_CenterY",
                    "BtmLA_StageX", "BtmLA_StageY", "BtmLA_DxCam", "BtmLA_DyCam", "BtmLA_CenterX", "BtmLA_CenterY",
                    "PcTRad", "Hc1Rad", "Hc2Rad",
                    "Hcro_X", "Hcro_Y", "Hc2Offset_X", "Hc2Offset_Y",
                    "OffsetX", "OffsetY", "OffsetT",
                    "LDist_X", "LDist_Y", "RDist_X", "RDist_Y",
                    "BFL_X", "BFL_Y", "BFR_X", "BFR_Y",
                    "BL_X", "BL_Y", "BR_X", "BR_Y",
                    "TL_X", "TL_Y", "TR_X", "TR_Y",
                    "SpecTheta", "BTheta", "TTheta", "ThetaF", "ThetaFRad",
                    "TCenter_X", "TCenter_Y", "BCenter_X", "BCenter_Y",
                    "ResultX", "ResultY", "ResultT",
                    "BtmAlignDist", "BtmAlignDistX", "BtmAlignDistY",
                    "TopAlignDist", "TopAlignDistX", "TopAlignDistY",
                    "BtmFidDist", "BtmFidDistX", "BtmFidDistY",
                    "TopFidDist", "TopFidDistX", "TopFidDistY",
                    "Vernier_OffsetX", "Vernier_OffsetY", "Vernier_OffsetT",
                    "HC1_Cur_X", "HC1_Cur_Y", "HC1_Ref_X", "HC1_Ref_Y", "HC1_Drift_X", "HC1_Drift_Y",
                    "HC2_Cur_X", "HC2_Cur_Y", "HC2_Ref_X", "HC2_Ref_Y", "HC2_Drift_X", "HC2_Drift_Y",
                    "Fid_CurDist",
                    "P_PC_Fid_DX", "P_PC_Fid_DY", "P_PC_Fid_Dist", "P_PC_Fid_Theta",
                    "P_PC_Align_DX", "P_PC_Align_DY", "P_PC_Align_Dist", "P_PC_Align_Theta",
                    "P_HC_Fid_L_X", "P_HC_Fid_L_Y", "P_HC_Fid_R_X", "P_HC_Fid_R_Y",
                    "P_HC_Fid_DX", "P_HC_Fid_DY", "P_HC_Fid_Dist", "P_HC_Fid_Theta",
                    "P_HC_Align_L_X", "P_HC_Align_L_Y", "P_HC_Align_R_X", "P_HC_Align_R_Y",
                    "P_HC_Align_DX", "P_HC_Align_DY", "P_HC_Align_Dist", "P_HC_Align_Theta",
                    "W_HC_Fid_L_X", "W_HC_Fid_L_Y", "W_HC_Fid_R_X", "W_HC_Fid_R_Y",
                    "W_HC_Fid_DX", "W_HC_Fid_DY", "W_HC_Fid_Dist", "W_HC_Fid_Theta",
                    "W_HC_Align_L_X", "W_HC_Align_L_Y", "W_HC_Align_R_X", "W_HC_Align_R_Y",
                    "W_HC_Align_DX", "W_HC_Align_DY", "W_HC_Align_Dist", "W_HC_Align_Theta",
                    "RightFidSimTheta", "RightFidSimScale", "Kind"));
            }

            sb.AppendLine(string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                data.AvgMove,
                MarkFields(data.TopRightFidRaw),
                MarkFields(data.TopRightAlignRaw),
                MarkFields(data.TopLeftFidRaw),
                MarkFields(data.TopLeftAlignRaw),
                PointAsMark(data.BtmRightFidRaw),
                PointAsMark(data.BtmRightAlignRaw),
                PointAsMark(data.BtmLeftFidRaw),
                PointAsMark(data.BtmLeftAlignRaw),
                F(data.PcTRad), F(data.Hc1Rad), F(data.Hc2Rad),
                data.Hcro != null ? F(data.Hcro.X) : "", data.Hcro != null ? F(data.Hcro.Y) : "",
                data.Hc2Offset != null ? F(data.Hc2Offset.X) : "", data.Hc2Offset != null ? F(data.Hc2Offset.Y) : "",
                data.OffsetXY != null ? F(data.OffsetXY.X) : "", data.OffsetXY != null ? F(data.OffsetXY.Y) : "",
                F(data.OffsetT),
                Pt(data.LDist), Pt(data.RDist),
                Pt(data.BFL), Pt(data.BFR),
                Pt(data.BL), Pt(data.BR),
                Pt(data.TL), Pt(data.TR),
                F(data.SpecTheta), F(data.BTheta), F(data.TTheta),
                F(data.ThetaF), F(data.ThetaFRad),
                Pt(data.TCenter), Pt(data.BCenter),
                F(data.ResultX), F(data.ResultY), F(data.ResultT),
                F(data.BtmAlignDist), F(data.BtmAlignDistX), F(data.BtmAlignDistY),
                F(data.TopAlignDist), F(data.TopAlignDistX), F(data.TopAlignDistY),
                F(data.BtmFidDist), F(data.BtmFidDistX), F(data.BtmFidDistY),
                F(data.TopFidDist), F(data.TopFidDistX), F(data.TopFidDistY),
                F(VernierResult?.OffsetX), F(VernierResult?.OffsetY), F(VernierResult?.OffsetT),
                Pt(data.Hc1FidCurrent),
                Pt(data.Hc1FidRef),
                Pt(data.Hc1FidDrift),
                Pt(data.Hc2FidCurrent),
                Pt(data.Hc2FidRef),
                Pt(data.Hc2FidDrift),
                F(data.FidCurrentDist),
                CsvMeasurementData(data),
                F(data.RightFidSimTheta), F(data.RightFidSimScale), kind));

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);

            _logger.Information(
                "선분 길이({Kind}) — BtmAlign: {BA:F4}mm, TopAlign: {TA:F4}mm, BtmFid: {BF:F4}mm, TopFid: {TF:F4}mm",
                kind, data.BtmAlignDist, data.TopAlignDist,
                data.BtmFidDist, data.TopFidDist);

            _logger.Information("본딩 데이터 저장({Kind}): {Path}", kind, path);
        }

        private string CsvMeasurementData(AlignData data)
        {
            var vals = new List<string>(40);
            var offset = data?.Hc2Offset;

            // ── 측정1: P_TABLE PC_Camera ──
            if (data?.TopLeftFidRaw != null && data?.TopRightFidRaw != null)
            {
                var r = CalibrationMath.CalcRelative(data.TopLeftFidRaw.CenterX, data.TopLeftFidRaw.CenterY,
                    data.TopRightFidRaw.CenterX, data.TopRightFidRaw.CenterY);
                vals.AddRange(new[] { F(r.dx), F(r.dy), F(r.dist), F(r.theta) });
            }
            else vals.AddRange(new[] { "", "", "", "" });

            if (data?.TopLeftAlignRaw != null && data?.TopRightAlignRaw != null)
            {
                var r = CalibrationMath.CalcRelative(data.TopLeftAlignRaw.CenterX, data.TopLeftAlignRaw.CenterY,
                    data.TopRightAlignRaw.CenterX, data.TopRightAlignRaw.CenterY);
                vals.AddRange(new[] { F(r.dx), F(r.dy), F(r.dist), F(r.theta) });
            }
            else vals.AddRange(new[] { "", "", "", "" });

            // ── 측정2: P_TABLE HC1/HC2 ──
            if (offset != null && data?.Hc1FidCurrent != null && data?.Hc2FidCurrent != null)
            {
                double lx = -data.Hc1FidCurrent.X, ly = -data.Hc1FidCurrent.Y;
                double rx = offset.X - data.Hc2FidCurrent.X, ry = offset.Y - data.Hc2FidCurrent.Y;
                var r = CalibrationMath.CalcRelative(lx, ly, rx, ry);
                vals.AddRange(new[] { F(lx), F(ly), F(rx), F(ry), F(r.dx), F(r.dy), F(r.dist), F(r.theta) });
            }
            else vals.AddRange(new[] { "", "", "", "", "", "", "", "" });

            if (data?.TL != null && data?.TR != null)
            {
                var r = CalibrationMath.CalcRelative(data.TL.X, data.TL.Y, data.TR.X, data.TR.Y);
                vals.AddRange(new[] { F(data.TL.X), F(data.TL.Y), F(data.TR.X), F(data.TR.Y),
                    F(r.dx), F(r.dy), F(r.dist), F(r.theta) });
            }
            else vals.AddRange(new[] { "", "", "", "", "", "", "", "" });

            // ── 측정3: W_TABLE HC1/HC2 ──
            if (offset != null && data?.BtmLeftFidRaw != null && data?.BtmRightFidRaw != null)
            {
                double lx = -data.BtmLeftFidRaw.X, ly = -data.BtmLeftFidRaw.Y;
                double rx = offset.X - data.BtmRightFidRaw.X, ry = offset.Y - data.BtmRightFidRaw.Y;
                var r = CalibrationMath.CalcRelative(lx, ly, rx, ry);
                vals.AddRange(new[] { F(lx), F(ly), F(rx), F(ry), F(r.dx), F(r.dy), F(r.dist), F(r.theta) });
            }
            else vals.AddRange(new[] { "", "", "", "", "", "", "", "" });

            if (offset != null && data?.BtmLeftAlignRaw != null && data?.BtmRightAlignRaw != null)
            {
                double lx = -data.BtmLeftAlignRaw.X, ly = -data.BtmLeftAlignRaw.Y;
                double rx = offset.X - data.BtmRightAlignRaw.X, ry = offset.Y - data.BtmRightAlignRaw.Y;
                var r = CalibrationMath.CalcRelative(lx, ly, rx, ry);
                vals.AddRange(new[] { F(lx), F(ly), F(rx), F(ry), F(r.dx), F(r.dy), F(r.dist), F(r.theta) });
            }
            else vals.AddRange(new[] { "", "", "", "", "", "", "", "" });

            return string.Join(",", vals);
        }

        // 사용 레시피 변경 중 재진입 방지
        private bool _isSettingUseRecipe;

        // SelectedRecipe가 바뀌면 자동 호출됨 (콤보박스 선택 포함)
        partial void OnSelectedRecipeChanged(RecipeDto value)
        {
            if (value == null)
            {
                RecipeSelectState = StepState.Idle;
                return;
            }
            _ = SetUseRecipeAsync(value);
        }

        private async Task SetUseRecipeAsync(RecipeDto recipe)
        {
            if (_isSettingUseRecipe) return;

            _isSettingUseRecipe = true;
            try
            {
                RecipeSelectState = StepState.InProgress;

                bool visionNotified = await _recipeService.SetUseRecipeAsync(recipe);
                if (!visionNotified)
                    _logger.Warning("Vision Recipe 파라미터가 없어 비전에 통보하지 못했습니다 — {Name}", recipe.Name);

                RecipeSelectState = StepState.Completed;
                _logger.Information("사용 레시피 변경: {Name}", recipe.Name);

                RecipeComponentChanged?.Invoke(recipe.Component);
            }
            catch (Exception e)
            {
                RecipeSelectState = StepState.Failed;
                _logger.Error(e, "사용 레시피 변경 실패");
            }
            finally
            {
                _isSettingUseRecipe = false;
            }
        }

        private static string Pt(Point2D p) => p == null ? "," : $"{F(p.X)},{F(p.Y)}";
        private static string MarkFields(VisionMarkResult m) =>
            m == null ? ",,,,," : string.Join(",", F(m.StageX), F(m.StageY), F(m.DxCamToMark), F(m.DyCamToMark), F(m.CenterX), F(m.CenterY));
        private static string PointAsMark(Point2D p) =>
            p == null ? ",,,,," : string.Join(",", "", "", F(p.X), F(p.Y), "", "");
        private static string NullMark() => ",,,,,";
        private static string NullPt() => ",";
        private static string F(double? v) => v?.ToString("F6") ?? "";
        private static string Fn(double? v) => v.HasValue ? v.Value.ToString("F6") : string.Empty;
    }
}