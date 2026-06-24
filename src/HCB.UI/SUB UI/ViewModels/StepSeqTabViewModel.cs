using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
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

        public RecipeService RecipeService => _recipeService;

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
        [ObservableProperty] private bool useAutoTracing = false;
        [ObservableProperty] private bool useBtmIndividualMeasure = false;
        [ObservableProperty] private bool useFiducialTracking = false;

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
        public void ChangeAutoTracing() => UseAutoTracing = !UseAutoTracing;

        [RelayCommand]
        public void ChangeBtmMeasureMode() => UseBtmIndividualMeasure = !UseBtmIndividualMeasure;

        [RelayCommand]
        public void ChangeFiducialTracking() => UseFiducialTracking = !UseFiducialTracking;

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

        [RelayCommand]
        private void BrowseVernierDir()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Vernier CSV 저장 폴더 선택",
                InitialDirectory = Directory.Exists(CsvVernierDir) ? CsvVernierDir : ""
            };
            if (dlg.ShowDialog() == true)
                CsvVernierDir = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseDataDir()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "본딩 데이터 CSV 저장 폴더 선택",
                InitialDirectory = Directory.Exists(CsvDataDir) ? CsvDataDir : ""
            };
            if (dlg.ShowDialog() == true)
                CsvDataDir = dlg.FolderName;
        }

        private string ResolveCsvPath(string dir, string fileNamePattern)
        {
            var resolved = fileNamePattern.Replace("{date}", DateTime.Now.ToString("yyyyMMdd"));
            return Path.Combine(dir, resolved);
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
            ILogger logger)
        {
            _logger = logger.ForContext<StepSeqTabViewModel>();
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
            await _sequenceService.StopAsync(_cts.Token);

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
                new AlignResultWindow(() => { ComputeDistances(); return hcbData; }, refTop, refBtm)
                { Header = "정렬 결과 — 실시간", WindowStartupLocation = WindowStartupLocation.CenterScreen }
                .ShowDialog());
        }

        [RelayCommand]
        public void BtmHighAlignInfo()
        {
            var (refTop, refBtm) = GetRefAlignDists();
            _ = RunDialogOnNewThread(() =>
                new AlignResultWindow(() => { ComputeDistances(); return hcbData; }, refTop, refBtm)
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
                await _sequenceService.DieDrop(1, _cts.Token);
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
                var data = new AlignData { AvgMove = AvgMode, Use2DMapping = Use2DMapping, UseAutoTracing = UseAutoTracing, UseBtmIndividualMeasure = UseBtmIndividualMeasure, UseFiducialTracking = UseFiducialTracking };
                hcbData = await _sequenceService.TopHighAlign(data, _cts.Token);
                ComputeDistances();
                TopRightFid = hcbData.TopRightFidRaw;
                TopRightAlign = hcbData.TopRightAlignRaw;
                TopLeftFid = hcbData.TopLeftFidRaw;
                TopLeftAlign = hcbData.TopLeftAlignRaw;
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
                //hcbData = await _sequenceService.BtmHighAlign(hcbData, _cts.Token);
                hcbData = await _sequenceService.BtmHighAlign(hcbData, _cts.Token);
                ComputeDistances();
                BtmRightFid = hcbData.BtmRightFidRaw;
                BtmRightAlign = hcbData.BtmRightAlignRaw;
                BtmLeftFid = hcbData.BtmLeftFidRaw;
                BtmLeftAlign = hcbData.BtmLeftAlignRaw;
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
                await _sequenceService.CoordinateSystemIntegration(hcbData, _cts.Token);
                ComputeDistances();
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
                // 1. 저배율 보정 + Pickup
                TopLowAlignState = StepState.InProgress;
                VisionTopLowAlign = await _sequenceService.TopLowMeasure(TopDie, MarkType.DIE_CENTER_TOP, ct);
                await RunNoStop(() => _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, ct));
                TopLowAlignState = StepState.Completed;

                // 2~4 반복
                for (int i = 0; i < 3000; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    TopHighAlignState = StepState.InProgress;
                    var data = new AlignData { AvgMove = AvgMode, Use2DMapping = Use2DMapping, UseAutoTracing = UseAutoTracing, UseBtmIndividualMeasure = UseBtmIndividualMeasure, UseFiducialTracking = UseFiducialTracking };
                    hcbData = await _sequenceService.TopHighAlign(data, ct);
                    TopRightFid = hcbData.TopRightFidRaw;
                    TopRightAlign = hcbData.TopRightAlignRaw;
                    TopLeftFid = hcbData.TopLeftFidRaw;
                    TopLeftAlign = hcbData.TopLeftAlignRaw;
                    TopHighAlignState = StepState.Completed;

                    BtmHighAlignState = StepState.InProgress;
                    hcbData = await _sequenceService.BtmHighAlign(hcbData, ct);
                    BtmRightFid = hcbData.BtmRightFidRaw;
                    BtmRightAlign = hcbData.BtmRightAlignRaw;
                    BtmLeftFid = hcbData.BtmLeftFidRaw;
                    BtmLeftAlign = hcbData.BtmLeftAlignRaw;
                    BtmHighAlignState = StepState.Completed;

                    ComputeDistances();
                    if (!ValidateAlignDistances())
                        throw new Exception("Top/Btm 선분 길이 오차가 허용 범위를 초과했습니다.");

                    TopCorrState = StepState.InProgress;
                    await _sequenceService.CoordinateSystemIntegration(hcbData, ct);
                    ComputeDistances();
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
        public async Task TopRunFullSequence()
        {
            ResetCts();
            var ct = _cts.Token;
            TrackStep("TopFull", StepState.InProgress);
            TrackStep("TopFullExMeasure", StepState.InProgress);
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); TrackStep("TopFull", StepState.Idle); TrackStep("TopFullExMeasure", StepState.Idle); return; }

                // 1. 저배율 보정 + Pickup
                TopLowAlignState = StepState.InProgress;
                VisionTopLowAlign = await _sequenceService.TopLowMeasure(TopDie, MarkType.DIE_CENTER_TOP, ct);
                await RunNoStop(() => _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, ct));
                TopLowAlignState = StepState.Completed;

                // 2. 고배율 측정 (Top)
                TopHighAlignState = StepState.InProgress;
                var data = new AlignData { AvgMove = AvgMode, Use2DMapping = Use2DMapping, UseAutoTracing = UseAutoTracing, UseBtmIndividualMeasure = UseBtmIndividualMeasure, UseFiducialTracking = UseFiducialTracking };
                hcbData = await _sequenceService.TopHighAlign(data, ct);
                TopRightFid = hcbData.TopRightFidRaw;
                TopRightAlign = hcbData.TopRightAlignRaw;
                TopLeftFid = hcbData.TopLeftFidRaw;
                TopLeftAlign = hcbData.TopLeftAlignRaw;
                TopHighAlignState = StepState.Completed;

                // 4. 고배율 측정 (Btm)
                BtmHighAlignState = StepState.InProgress;
                hcbData = await _sequenceService.BtmHighAlign(hcbData, ct);
                BtmRightFid = hcbData.BtmRightFidRaw;
                BtmRightAlign = hcbData.BtmRightAlignRaw;
                BtmLeftFid = hcbData.BtmLeftFidRaw;
                BtmLeftAlign = hcbData.BtmLeftAlignRaw;
                BtmHighAlignState = StepState.Completed;

                // 4-1. 선분 길이 오차 검증
                ComputeDistances();
                if (!ValidateAlignDistances())
                    throw new Exception("Top/Btm 선분 길이 오차가 허용 범위를 초과했습니다.");

                // 5. 보정
                TopCorrState = StepState.InProgress;
                await _sequenceService.CoordinateSystemIntegration(hcbData, ct);
                await _sequenceService.BondingCorr(hcbData, ct);
                TopCorrState = StepState.Completed;

                // 6. 본딩
                TopBondingState = StepState.InProgress;
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                await RunNoStop(() => _sequenceService.BondingPress(BondingHistory, ct));
                TopBondingState = StepState.Completed;
                TrackStep("TopFullExMeasure", StepState.Completed);

                // 7. 버니어 측정 (옵션)
                if (MeasureVernierAfterBonding)
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
                ExportHcbData();
            }
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
            catch (Exception e) { _logger.Error(e, "Vernier 측정 실패"); }
        }


        public async Task AccuracyMode()
        {
            var result = await _sequenceService.GetVernier(_cts.Token);
        }
        [RelayCommand]
        public void ChangeAvgMode() => AvgMode = !AvgMode;

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
        //  선분 길이 계산 & 기준 거리 헬퍼
        // ═════════════════════════════════════════════════════

        private void ComputeDistances()
        {
            if (hcbData == null) return;

            if (hcbData.BL != null && hcbData.BR != null)
                hcbData.BtmAlignDist = CalibrationMath.Dist(hcbData.BR, hcbData.BL);
            
            if (hcbData.BFL != null && hcbData.BFR != null)
                hcbData.BtmFidDist = CalibrationMath.Dist(hcbData.BFR, hcbData.BFL);

            if (hcbData.TopLeftAlignRaw != null && hcbData.TopRightAlignRaw != null)
            {
                var dx = hcbData.TopRightAlignRaw.CenterX - hcbData.TopLeftAlignRaw.CenterX;
                var dy = hcbData.TopRightAlignRaw.CenterY - hcbData.TopLeftAlignRaw.CenterY;
                hcbData.TopAlignDist = Math.Sqrt(dx * dx + dy * dy);
            }

            if (hcbData.TopLeftFidRaw != null && hcbData.TopRightFidRaw != null)
            {
                var dx = hcbData.TopRightFidRaw.CenterX - hcbData.TopLeftFidRaw.CenterX;
                var dy = hcbData.TopRightFidRaw.CenterY - hcbData.TopLeftFidRaw.CenterY;
                hcbData.TopFidDist = Math.Sqrt(dx * dx + dy * dy);
            }
        }

        // ViewModel - GetRefAlignDist → 두 개로 분리
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
            Directory.CreateDirectory(CsvVernierDir);
            var path = ResolveCsvPath(CsvVernierDir, CsvVernierFileName);

            bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            var sb = new StringBuilder();
            if (writeHeader) sb.AppendLine("Time,Pos,V1_X,V1_Y,V3_X,V3_Y");

            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            foreach (var row in VernierRows)
                sb.AppendLine($"{ts},{row.Name},{Fn(row.V1X)},{Fn(row.V1Y)},{Fn(row.V3X)},{Fn(row.V3Y)}");

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            _logger.Information("Vernier CSV 저장: {Path}", path);
        }

        private void ExportHcbData()
        {
            Directory.CreateDirectory(CsvDataDir);
            var path = ResolveCsvPath(CsvDataDir, CsvDataFileName);

            ComputeDistances();

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
                    "BtmAlignDist", "TopAlignDist", "BtmFidDist", "TopFidDist",
                    "Vernier_OffsetX", "Vernier_OffsetY", "Vernier_OffsetT",
                    "HC1_Cur_X", "HC1_Cur_Y", "HC1_Ref_X", "HC1_Ref_Y", "HC1_Drift_X", "HC1_Drift_Y",
                    "HC2_Cur_X", "HC2_Cur_Y", "HC2_Ref_X", "HC2_Ref_Y", "HC2_Drift_X", "HC2_Drift_Y"));
            }

            sb.AppendLine(string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                hcbData?.AvgMove ?? true,
                hcbData != null ? MarkFields(hcbData.TopRightFidRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.TopRightAlignRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.TopLeftFidRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.TopLeftAlignRaw) : NullMark(),
                hcbData != null ? PointAsMark(hcbData.BtmRightFidRaw) : NullMark(),
                hcbData != null ? PointAsMark(hcbData.BtmRightAlignRaw) : NullMark(),
                hcbData != null ? PointAsMark(hcbData.BtmLeftFidRaw) : NullMark(),
                hcbData != null ? PointAsMark(hcbData.BtmLeftAlignRaw) : NullMark(),
                F(hcbData?.PcTRad), F(hcbData?.Hc1Rad), F(hcbData?.Hc2Rad),
                hcbData?.Hcro != null ? F(hcbData.Hcro.X) : "", hcbData?.Hcro != null ? F(hcbData.Hcro.Y) : "",
                hcbData?.Hc2Offset != null ? F(hcbData.Hc2Offset.X) : "", hcbData?.Hc2Offset != null ? F(hcbData.Hc2Offset.Y) : "",
                hcbData?.OffsetXY != null ? F(hcbData.OffsetXY.X) : "", hcbData?.OffsetXY != null ? F(hcbData.OffsetXY.Y) : "",
                F(hcbData?.OffsetT),
                hcbData != null ? Pt(hcbData.LDist) : NullPt(), hcbData != null ? Pt(hcbData.RDist) : NullPt(),
                hcbData != null ? Pt(hcbData.BFL) : NullPt(), hcbData != null ? Pt(hcbData.BFR) : NullPt(),
                hcbData != null ? Pt(hcbData.BL) : NullPt(), hcbData != null ? Pt(hcbData.BR) : NullPt(),
                hcbData != null ? Pt(hcbData.TL) : NullPt(), hcbData != null ? Pt(hcbData.TR) : NullPt(),
                F(hcbData?.SpecTheta), F(hcbData?.BTheta), F(hcbData?.TTheta),
                F(hcbData?.ThetaF), F(hcbData?.ThetaFRad),
                hcbData != null ? Pt(hcbData.TCenter) : NullPt(), hcbData != null ? Pt(hcbData.BCenter) : NullPt(),
                F(hcbData?.ResultX), F(hcbData?.ResultY), F(hcbData?.ResultT),
                F(hcbData?.BtmAlignDist), F(hcbData?.TopAlignDist),
                F(hcbData?.BtmFidDist), F(hcbData?.TopFidDist),
                F(VernierResult?.OffsetX), F(VernierResult?.OffsetY), F(VernierResult?.OffsetT),
                hcbData != null ? Pt(hcbData.Hc1FidCurrent) : NullPt(),
                hcbData != null ? Pt(hcbData.Hc1FidRef) : NullPt(),
                hcbData != null ? Pt(hcbData.Hc1FidDrift) : NullPt(),
                hcbData != null ? Pt(hcbData.Hc2FidCurrent) : NullPt(),
                hcbData != null ? Pt(hcbData.Hc2FidRef) : NullPt(),
                hcbData != null ? Pt(hcbData.Hc2FidDrift) : NullPt()));

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);

            if (hcbData != null)
            {
                _logger.Information(
                    "선분 길이 — BtmAlign: {BA:F4}mm, TopAlign: {TA:F4}mm, BtmFid: {BF:F4}mm, TopFid: {TF:F4}mm",
                    hcbData.BtmAlignDist, hcbData.TopAlignDist,
                    hcbData.BtmFidDist, hcbData.TopFidDist);
            }

            _logger.Information("본딩 데이터 저장: {Path}", path);
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