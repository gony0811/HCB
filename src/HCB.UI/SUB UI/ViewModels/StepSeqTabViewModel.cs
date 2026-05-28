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
using System.Windows;
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

        [ObservableProperty] private VisionMarkResult btmRightAlign;
        [ObservableProperty] private VisionMarkResult btmRightFid;
        [ObservableProperty] private VisionMarkResult btmLeftAlign;
        [ObservableProperty] private VisionMarkResult btmLeftFid;

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

            var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            if (ioDevice != null)
            {
                for (var i = 0; i < _dTableNameList.Count; i++)
                {
                    var vm = _ioManager.CreateIoVM(_dTableNameList[i], _dIoNameList[i], _dTableNameList[i]);
                    if (vm != null) DTableList.Add(vm);
                }
            }
        }

        // ═════════════════════════════════════════════════════
        //  STOP
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task Stop()
        {
            if (_cts == null || _cts.IsCancellationRequested) return;
            _cts.Cancel();
            await _sequenceService.StopAsync(_cts.Token);

            InitState = StepState.Idle;
            BtmLowAlignState = BtmPickupState = BtmPlaceState = StepState.Idle;
            TopLowAlignState = TopPickupState = TopHighAlignState
                             = TopCorrState = TopBondingState = StepState.Idle;
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
            catch (Exception e) { InitState = StepState.Failed; _logger.Warning(e.Message); }
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
            catch (Exception e) { _logger.Error(e.Message); }
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
        public async Task BtmLowAlign()
        {
            ResetCts();
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }
                BtmLowAlignState = StepState.InProgress;
                VisionBtmLowAlign = await _sequenceService.BtmCarrierAlign(
                    BottomDie, MarkType.DIE_CENTER_BOTTOM, _cts.Token);
                BtmLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmLowAlignState = StepState.Idle; }
            catch (Exception e) { BtmLowAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmPickup()
        {
            ResetCts();
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }
                if (VisionBtmLowAlign == null) { _logger.Information("Bottom Die Align 해주세요"); return; }
                BtmPickupState = StepState.InProgress;
                await _sequenceService.DTablePickup(DieType.BOTTOM, BottomDie, VisionBtmLowAlign, _cts.Token);
                BtmPickupState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmPickupState = StepState.Idle; }
            catch (Exception e) { BtmPickupState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmPlace()
        {
            ResetCts();
            try
            {
                BtmPlaceState = StepState.InProgress;
                await _sequenceService.BtmDieDrop(1, _cts.Token);
                BtmPlaceState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmPlaceState = StepState.Idle; }
            catch (Exception e) { BtmPlaceState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmFullSequence()
        {
            ResetCts();
            var ct = _cts.Token;
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }

                BtmLowAlignState = StepState.InProgress;
                VisionBtmLowAlign = await _sequenceService.BtmCarrierAlign(BottomDie, MarkType.DIE_CENTER_BOTTOM, ct);
                BtmLowAlignState = StepState.Completed;
                if (VisionBtmLowAlign == null) { _logger.Information("Bottom Die Align 실패"); return; }

                BtmPickupState = StepState.InProgress;
                await _sequenceService.DTablePickup(DieType.BOTTOM, BottomDie, VisionBtmLowAlign, ct);
                BtmPickupState = StepState.Completed;

                BtmPlaceState = StepState.InProgress;
                await _sequenceService.BtmDieDrop(1, ct);
                BtmPlaceState = StepState.Completed;
            }
            catch (OperationCanceledException)
            {
                BtmLowAlignState = IfInProgress(BtmLowAlignState, StepState.Idle);
                BtmPickupState = IfInProgress(BtmPickupState, StepState.Idle);
                BtmPlaceState = IfInProgress(BtmPlaceState, StepState.Idle);
            }
            catch (Exception e)
            {
                BtmLowAlignState = IfInProgress(BtmLowAlignState, StepState.Failed);
                BtmPickupState = IfInProgress(BtmPickupState, StepState.Failed);
                BtmPlaceState = IfInProgress(BtmPlaceState, StepState.Failed);
                _logger.Warning(e.Message);
            }
        }

        // ═════════════════════════════════════════════════════
        //  TOP 시퀀스 — 개별 (6단계)
        //  1. 저배율 보정  2. Pickup  3. 고배율(Top)
        //  4. 고배율(Btm)  5. 보정    6. 본딩
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task TopLowAlign()
        {
            ResetCts();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                TopLowAlignState = StepState.InProgress;
                VisionTopLowAlign = await _sequenceService.TopLowAlign(TopDie, _cts.Token);
                TopLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopLowAlignState = StepState.Idle; }
            catch (Exception e) { TopLowAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopPickup()
        {
            ResetCts();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                if (VisionTopLowAlign == null) { _logger.Information("Top Die Align 해주세요"); return; }
                TopPickupState = StepState.InProgress;
                await _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, _cts.Token);
                TopPickupState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopPickupState = StepState.Idle; }
            catch (Exception e) { TopPickupState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopHighAlign()
        {
            ResetCts();
            try
            {
                TopHighAlignState = StepState.InProgress;
                var data = new AlignData { AvgMove = AvgMode };
                hcbData = await _sequenceService.TopHighAlign(data, _cts.Token);
                ComputeDistances();
                TopRightFid = hcbData.TopRightFidRaw;
                TopRightAlign = hcbData.TopRightAlignRaw;
                TopLeftFid = hcbData.TopLeftFidRaw;
                TopLeftAlign = hcbData.TopLeftAlignRaw;
                TopHighAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopHighAlignState = StepState.Idle; }
            catch (Exception e) { TopHighAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmHighAlign()
        {
            ResetCts();
            try
            {
                BtmHighAlignState = StepState.InProgress;
                hcbData = await _sequenceService.BtmHighAlign(hcbData, _cts.Token);
                ComputeDistances();
                BtmRightFid = hcbData.BtmRightFidRaw;
                BtmRightAlign = hcbData.BtmRightAlignRaw;
                BtmLeftFid = hcbData.BtmLeftFidRaw;
                BtmLeftAlign = hcbData.BtmLeftAlignRaw;
                BtmHighAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmHighAlignState = StepState.Idle; }
            catch (Exception e) { BtmHighAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopCorr()
        {
            ResetCts();
            try
            {
                TopCorrState = StepState.InProgress;
                await _sequenceService.TopPlace(hcbData, _cts.Token);
                ComputeDistances();
                await _sequenceService.BondingCorr(hcbData, _cts.Token);
                TopCorrState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopCorrState = StepState.Idle; }
            catch (Exception e) { TopCorrState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopBonding()
        {
            ResetCts();
            try
            {
                TopBondingState = StepState.InProgress;
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                await _sequenceService.BondingPress(BondingHistory, _cts.Token);
                TopBondingState = StepState.Completed;
                ExportHcbData();
            }
            catch (OperationCanceledException) { TopBondingState = StepState.Idle; }
            catch (Exception e) { TopBondingState = StepState.Failed; _logger.Warning(e.Message); }
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
                // 1. 저배율 + 2. Pickup
                TopLowAlignState = StepState.InProgress;
                VisionTopLowAlign = await _sequenceService.TopLowAlign(TopDie, ct);
                TopLowAlignState = StepState.Completed;

                TopPickupState = StepState.InProgress;
                await _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, ct);
                TopPickupState = StepState.Completed;

                // 3~5 반복
                for (int i = 0; i < 3000; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    TopHighAlignState = StepState.InProgress;
                    var data = new AlignData { AvgMove = AvgMode };
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

                    TopCorrState = StepState.InProgress;
                    await _sequenceService.TopPlace(hcbData, ct);
                    ComputeDistances();
                    TopCorrState = StepState.Completed;

                    ExportHcbData();
                }

                // 6. 본딩
                TopBondingState = StepState.InProgress;
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                await _sequenceService.BondingCorr(hcbData, ct);
                await _sequenceService.BondingPress(BondingHistory, ct);
                TopBondingState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopBondingState = StepState.Idle; }
            catch (Exception e) { TopBondingState = StepState.Failed; _logger.Warning(e.Message); }
        }

        // ═════════════════════════════════════════════════════
        //  TOP Full (1→2→3→4→5→6)
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task TopRunFullSequence()
        {
            ResetCts();
            var ct = _cts.Token;
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }

                // 1. 저배율 보정
                TopLowAlignState = StepState.InProgress;
                VisionTopLowAlign = await _sequenceService.TopLowAlign(TopDie, ct);
                TopLowAlignState = StepState.Completed;

                // 2. Pickup
                TopPickupState = StepState.InProgress;
                await _sequenceService.DTablePickup(DieType.TOP, TopDie, VisionTopLowAlign, ct);
                TopPickupState = StepState.Completed;

                // 3. 고배율 측정 (Top)
                TopHighAlignState = StepState.InProgress;
                var data = new AlignData { AvgMove = AvgMode };
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

                // 5. 보정
                TopCorrState = StepState.InProgress;
                await _sequenceService.TopPlace(hcbData, ct);
                ComputeDistances();
                await _sequenceService.BondingCorr(hcbData, ct);
                TopCorrState = StepState.Completed;

                // 6. 본딩
                TopBondingState = StepState.InProgress;
                BondingHistory = new ObservableCollection<BondingDataPoint>();
                await _sequenceService.BondingPress(BondingHistory, ct);
                TopBondingState = StepState.Completed;
                ExportHcbData();
            }
            catch (OperationCanceledException)
            {
                TopLowAlignState = IfInProgress(TopLowAlignState, StepState.Idle);
                TopPickupState = IfInProgress(TopPickupState, StepState.Idle);
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Idle);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Idle);
                TopCorrState = IfInProgress(TopCorrState, StepState.Idle);
                TopBondingState = IfInProgress(TopBondingState, StepState.Idle);
            }
            catch (Exception e)
            {
                TopLowAlignState = IfInProgress(TopLowAlignState, StepState.Failed);
                TopPickupState = IfInProgress(TopPickupState, StepState.Failed);
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Failed);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Failed);
                TopCorrState = IfInProgress(TopCorrState, StepState.Failed);
                TopBondingState = IfInProgress(TopBondingState, StepState.Failed);
                _logger.Warning(e.Message);
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
                await _sequenceService.TopDiePlace(_cts.Token);
                var result = await _sequenceService.GetVernier(_cts.Token);
                VernierResult = result;

                var names = new[] { "1", "3", "5", "7", "9" };
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
            catch (Exception e) { _logger.Warning("Vernier 측정 실패: {Msg}", e.Message); }
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
        //  CSV 내보내기
        // ═════════════════════════════════════════════════════

        public void ExportHighResult()
        {
            if (VernierRows.Count == 0)
            {
                _logger.Information("저장할 Vernier 결과가 없습니다.");
                return;
            }
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "결과 데이터");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"bonding_hcb_{DateTime.Now:yyyyMMdd}.csv");

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
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "데이터");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"bonding_hcb_{DateTime.Now:yyyyMMdd}.csv");

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
                    "BtmAlignDist", "TopAlignDist", "BtmFidDist", "TopFidDist"));
            }

            sb.AppendLine(string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                hcbData?.AvgMove ?? true,
                hcbData != null ? MarkFields(hcbData.TopRightFidRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.TopRightAlignRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.TopLeftFidRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.TopLeftAlignRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.BtmRightFidRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.BtmRightAlignRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.BtmLeftFidRaw) : NullMark(),
                hcbData != null ? MarkFields(hcbData.BtmLeftAlignRaw) : NullMark(),
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
                F(hcbData?.BtmFidDist), F(hcbData?.TopFidDist)));

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

        private static string Pt(Point2D p) => p == null ? "," : $"{F(p.X)},{F(p.Y)}";
        private static string MarkFields(VisionMarkResult m) =>
            m == null ? ",,,,," : string.Join(",", F(m.StageX), F(m.StageY), F(m.DxCamToMark), F(m.DyCamToMark), F(m.CenterX), F(m.CenterY));
        private static string NullMark() => ",,,,,";
        private static string NullPt() => ",";
        private static string F(double? v) => v?.ToString("F6") ?? "";
        private static string Fn(double? v) => v.HasValue ? v.Value.ToString("F6") : string.Empty;
    }
}