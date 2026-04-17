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
using Telerik.Windows.Controls.DataVisualization.Map.BingRest;
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

        private AlignContext _alignCtx;

        // ── Die 번호 ──────────────────────────────────────────
        [ObservableProperty] private int topDie = 0;
        [ObservableProperty] private int bottomDie = 0;

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
        [ObservableProperty] private StepState topPlaceState = StepState.Idle;
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
        [ObservableProperty] private double hrBlX;
        [ObservableProperty] private double hrBlY;
        [ObservableProperty] private double hrBrX;
        [ObservableProperty] private double hrBrY;
        [ObservableProperty] private double hrTlX;
        [ObservableProperty] private double hrTlY;
        [ObservableProperty] private double hrTrX;
        [ObservableProperty] private double hrTrY;

        [ObservableProperty] private VernierResult vernierResult;
        // 화면에 표시할 텍스트 (예: "3 / 5")
        public string RepeatProgressText =>
            IsRepeatRunning ? $"{RepeatCurrent} / {RepeatTotal}" : string.Empty;

        // RepeatCurrent, RepeatTotal 변경 시 텍스트도 갱신
        partial void OnRepeatCurrentChanged(int value) =>
            OnPropertyChanged(nameof(RepeatProgressText));
        partial void OnRepeatTotalChanged(int value) =>
            OnPropertyChanged(nameof(RepeatProgressText));

        // ── D-Table IO ────────────────────────────────────────
        public SequenceServiceVM SequenceServiceVM { get; }

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList
            = new ObservableCollection<SensorIoItemViewModel>();

        private readonly List<string> _dTableNameList = new List<string>
        {
            "DIE 1","DIE 2","DIE 3","DIE 4","DIE 5","DIE 6","DIE 7","DIE 8","DIE 9",
        };

        private readonly List<string> _dIoNameList = new List<string>
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
            var p = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            //hz = p.MotionList.FirstOrDefault(m => m.Name == MotionExtensions.h_z);
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
                             = TopPlaceState = StepState.Idle;
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
        //  DIE LOAD
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task DieLoad()
        {
            ResetCts();
            try
            {
                await _sequenceService.DTableLoading(_cts.Token);

                bool confirmed = false;
                List<int> topList = new List<int>();
                List<int> botList = new List<int>();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var dialog = new VacuumSelector
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    dialog.ShowDialog();
                    confirmed = dialog.DialogResult == true;
                    if (confirmed)
                    {
                        topList = dialog.TopDieVacuums;
                        botList = dialog.BotDieVacuums;
                    }
                });

                if (!confirmed) return;

                if (topList.Count > 0) TopDie = topList[0];
                if (botList.Count > 0) BottomDie = botList[0];

                _logger.Information(
                    "Die Load 선택 완료 — TOP: [{Top}]  BOT: [{Bot}]",
                    string.Join(", ", topList),
                    string.Join(", ", botList));
            }
            catch (Exception e) { _logger.Error(e.Message); }
        }

        // ═════════════════════════════════════════════════════
        //  Info 팝업 커맨드
        // ═════════════════════════════════════════════════════

        [RelayCommand] public void InitInfo() => IsInitInfoOpen = true;
        [RelayCommand] public void CloseInitInfo() => IsInitInfoOpen = false;

        [RelayCommand]
        public void OpenTopHighAlignInfo()
        {
            var (rf, ra, lf, la) = (TopRightFid, TopRightAlign, TopLeftFid, TopLeftAlign);
            _ = RunDialogOnNewThread(() =>
            {
                new TopHighAlignInfoWindow(rf, ra, lf, la)
                {
                    Header = "고배율 보정 정보 (Top Die)",
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                }.ShowDialog();
            });
        }

        [RelayCommand]
        public void BtmHighAlignInfo()
        {
            var (rf, ra, lf, la) = (BtmRightFid, BtmRightAlign, BtmLeftFid, BtmLeftAlign);
            _ = RunDialogOnNewThread(() =>
            {
                new TopHighAlignInfoWindow(rf, ra, lf, la, useWaferY: true)
                {
                    Header = "고배율 보정 정보 (Bottom Die)",
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                }.ShowDialog();
            });
        }

        [RelayCommand]
        public void TopHighAlignInfo()   // Bonding INFO 버튼
        {
            var history = BondingHistory.ToList();
            _ = RunDialogOnNewThread(() =>
            {
                new BondingInfoWindow(_recipeService, history)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                }.ShowDialog();
            });
        }

        // ═════════════════════════════════════════════════════
        //  BOTTOM 시퀀스
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
                await _sequenceService.DTableBTMPickup(BottomDie, VisionBtmLowAlign, _cts.Token);
                await _sequenceHelper.BTMVac(BottomDie, eOnOff.Off, _cts.Token);
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
                await _sequenceService.DTableBTMPickup(BottomDie, VisionBtmLowAlign, ct);
                await _sequenceHelper.BTMVac(BottomDie, eOnOff.Off, ct);
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
        //  TOP 시퀀스 — 개별 커맨드
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task TopLowAlign()
        {
            ResetCts();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                await RunTopLowAlign(_cts.Token);
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
                await RunTopPickup(_cts.Token);
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
                await RunTopHighAlign(_cts.Token);
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
                await RunBtmHighAlign(_cts.Token);
            }
            catch (OperationCanceledException) { BtmHighAlignState = StepState.Idle; }
            catch (Exception e) { BtmHighAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopPlace()
        {
            ResetCts();
            try
            {
                await RunTopPlace(_cts.Token);
                
            }
            catch (OperationCanceledException) { TopBondingState = StepState.Idle; }
            catch (Exception e) { TopBondingState = StepState.Failed; _logger.Warning(e.Message); }
        }

        // ═════════════════════════════════════════════════════
        //  TOP 시퀀스 — Full (1→2→3→4→5)
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task TopRunFullSequence()
        {
            ResetCts();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }

                await RunTopLowAlign(_cts.Token);
                await RunTopPickup(_cts.Token);
                await RunTopHighAlign(_cts.Token);
                await RunBtmHighAlign(_cts.Token);
                await RunTopPlace(_cts.Token);
                
            }
            catch (OperationCanceledException)
            {
                TopLowAlignState = IfInProgress(TopLowAlignState, StepState.Idle);
                TopPickupState = IfInProgress(TopPickupState, StepState.Idle);
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Idle);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Idle);
                TopBondingState = IfInProgress(TopBondingState, StepState.Idle);
            }
            catch (Exception e)
            {
                TopLowAlignState = IfInProgress(TopLowAlignState, StepState.Failed);
                TopPickupState = IfInProgress(TopPickupState, StepState.Failed);
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Failed);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Failed);
                TopBondingState = IfInProgress(TopBondingState, StepState.Failed);
                _logger.Warning(e.Message);
            }
        }


        [RelayCommand]
        public async Task TopRepeatBonding()
        {
            ResetCts();
            var ct = _cts.Token;

            if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }

            RepeatCurrent = 0;
            IsRepeatRunning = true;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    RepeatCurrent++;

                    TopHighAlignState = StepState.Idle;
                    BtmHighAlignState = StepState.Idle;
                    TopBondingState = StepState.Idle;

                    await RunTopHighAlign(ct);
                    await RunBtmHighAlign(ct);
                    await RunTopPlace(ct);
                    _logger.Information("반복 본딩 #{Count} 완료", RepeatCurrent);
                }
            }
            catch (OperationCanceledException)
            {
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Idle);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Idle);
                TopBondingState = IfInProgress(TopBondingState, StepState.Idle);
                _logger.Information("반복 본딩 중단 — {Count}회 완료 후 취소", RepeatCurrent);
            }
            catch (Exception e)
            {
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Failed);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Failed);
                TopBondingState = IfInProgress(TopBondingState, StepState.Failed);
                _logger.Warning("반복 본딩 실패 #{Count}: {Msg}", RepeatCurrent, e.Message);
            }
            finally
            {
                IsRepeatRunning = false;
            }
        }
        // ═════════════════════════════════════════════════════
        //  LowResult (Head 초기화 후 Wafer Center 이동)
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task LowResult()
        {
            ResetCts();
            const string centerLow = "WAFER_CENTER_LOW";
            try
            {
                await _sequenceService.Init_Head(_cts.Token);
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, centerLow, _cts.Token),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, centerLow, _cts.Token)
                );
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, centerLow, _cts.Token);
            }
            catch (Exception e) { _logger.Warning(e.Message); }
        }

        //[RelayCommand]
        //public async Task HighResult()
        //{
        //    _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
        //    try
        //    {

        //        await _sequenceService.TopDiePlace(_cts.Token);


        //        var hc2Param = _ecParamService.FindByName(MotionExtensions.HC2_T);
        //        double hc2Rad = double.Parse(hc2Param.Value);

        //        // ── 1. Btm 우측 Align Mark로 이동 후 측정 (BTM RIGHT) ────────────
        //        var bR = await _sequenceService.VisionResult(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, MotionExtensions.W_Y, _cts.Token);

        //        // ── 2. X: -500um 이동 ─────────────────────────────────────────────
        //        await _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, -0.5, _cts.Token);

        //        // ── 3. Top 우측 Align Mark 측정 (TOP RIGHT) ───────────────────────
        //        var tR = await _sequenceService.VisionResult(CameraType.HC2_HIGH, MarkType.ALIGN_MARK_TOP, DirectType.RIGHT, MotionExtensions.W_Y, _cts.Token);

        //        // ── 4. X: -12mm, Y: +7mm 이동 ────────────────────────────────────
        //        await Task.WhenAll(
        //            _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, -12.0, _cts.Token),
        //            _sequenceService.RelativeMotionsMove(MotionExtensions.W_Y, 7.0, _cts.Token)
        //        );

        //        // ── 5. Left Align Mark 측정 (BTM LEFT) ───────────────────────────
        //        var bL = await _sequenceService.VisionResult(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, MotionExtensions.W_Y, _cts.Token);

        //        // ── 6. X: +500um 이동 ─────────────────────────────────────────────
        //        await _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, 0.5, _cts.Token);

        //        // ── 7. Left Align Mark 측정 (TOP LEFT) ───────────────────────────
        //        var tL = await _sequenceService.VisionResult(CameraType.HC2_HIGH, MarkType.ALIGN_MARK_TOP, DirectType.LEFT, MotionExtensions.W_Y, _cts.Token);

        //        var br = CalibrationMath.ApplyRotation(Point2D.of(bR.DxCamToMark, bR.DyCamToMark), hc2Rad);
        //        var bl = CalibrationMath.ApplyRotation(Point2D.of(bL.DxCamToMark, bL.DyCamToMark), hc2Rad);
        //        var tr = CalibrationMath.ApplyRotation(Point2D.of(tR.DxCamToMark, tR.DyCamToMark), hc2Rad);
        //        var tl = CalibrationMath.ApplyRotation(Point2D.of(tL.DxCamToMark, tL.DyCamToMark), hc2Rad);

        //        HrBlX = bl.X; HrBlY = bl.Y;
        //        HrBrX = br.X; HrBrY = br.Y;
        //        HrTlX = tl.X; HrTlY = tl.Y;
        //        HrTrX = tr.X; HrTrY = tr.Y;


        //        bR.DxCamToMark = br.X; bL.DxCamToMark = bl.X;
        //        bR.DyCamToMark = br.Y; bL.DyCamToMark = bl.Y;
        //        tR.DxCamToMark = tr.X; tL.DxCamToMark = tl.X;
        //        tR.DyCamToMark = tr.Y; tL.DyCamToMark = tl.Y;

        //        double bCX = (bR.CenterX + bL.CenterX) / 2;
        //        double bCY = (bR.CenterWaferY + bL.CenterWaferY) / 2;
        //        double tCX = (tR.CenterX + tL.CenterX) / 2;
        //        double tCY = (tR.CenterWaferY + tL.CenterWaferY) / 2;

        //        double btmTheta = CalcTheta(bL, bR);
        //        double topTheta = CalcTheta(tL, tR);
        //        double deltaTheta = btmTheta - topTheta;

        //        DetailX = tCX - bCX;
        //        DetailY = tCY - bCY;
        //        DetailT = deltaTheta - 2.07;
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.Information("HighResult 작업이 취소되었습니다.");
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.Warning(e.Message);
        //    }
        //}
        [RelayCommand]
        public async Task HighResult()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                await _sequenceService.TopDiePlace(_cts.Token);
                var result = await _sequenceService.GetVernier(_cts.Token);
                VernierResult = result;
                //await _sequenceService.TCheck(_cts.Token);
            }
            catch(Exception e)
            {

            }
        }

        [RelayCommand]
        public async Task RepeatMeasure()
        {
            // ── 1. 반복 횟수 입력 모달 ──
            var dialog = new RepeatMeasureDialog(_repeatCount);
            if (dialog.ShowDialog() != true) return;

            int repeat = dialog.RepeatCount;
            _repeatCount = repeat; // 마지막 입력값 기억

            // ── 2. 진행 상태 초기화 ──
            ResetCts();
            RepeatTotal = repeat;
            RepeatCurrent = 0;
            IsRepeatRunning = true;

            var results = new List<AlignContext>();

            try
            {
                for (int i = 0; i < repeat; i++)
                {
                    RepeatCurrent = i + 1;  // 1-based 표시
                    var result = await _sequenceService.TopHighAlign(
                        new AlignContext(), _cts.Token);
                    results.Add(result);
                }
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"align_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                ExportToCsv(results, path);
            }
            catch (OperationCanceledException)
            {
                // STOP 버튼으로 취소된 경우
            }
            finally
            {
                IsRepeatRunning = false;
            }
        }


        [RelayCommand]
        public async Task FidAFCheck()
        {
            ResetCts();
            IsRepeatRunning = true;
            RepeatTotal = 20;
            RepeatCurrent = 0;
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeasureResults");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, "FidAF.csv");
            try
            {
                bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
                using var writer = new StreamWriter(filePath, true, Encoding.UTF8);
                if (writeHeader)
                    await writer.WriteLineAsync("Time,HC1_X,HC1_Y,HC2_X,HC2_Y");
                for (int i = 0; i < 20; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var hc1 = await _sequenceService.VisionAFResult(
                        CameraType.HC1_HIGH, MarkType.FIDUCIAL, DirectType.LEFT, _cts.Token);
                    var hc2 = await _sequenceService.VisionAFResult(
                        CameraType.HC2_HIGH, MarkType.FIDUCIAL, DirectType.RIGHT, _cts.Token);
                    RepeatCurrent = i + 1;
                    await writer.WriteLineAsync($"{DateTime.Now},{hc1.x:F6},{hc1.y:F6},{hc2.x:F6},{hc2.y:F6}");
                    await writer.FlushAsync();
                }
                _logger.Information("FidAF(AF) CSV 저장 완료: {Path} ({Count}건)", filePath, RepeatCurrent);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("FidAF(AF) 측정 취소 — {Count}회 완료", RepeatCurrent);
            }
            catch (Exception e)
            {
                _logger.Warning("FidAF(AF) 측정 실패: {Msg}", e.Message);
            }
            finally
            {
                IsRepeatRunning = false;
            }
        }

        [RelayCommand]
        public async Task FidNoAFCheck()
        {
            ResetCts();
            IsRepeatRunning = true;
            RepeatTotal = 20;
            RepeatCurrent = 0;
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeasureResults");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, "FidAFNO.csv");
            try
            {
                bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
                using var writer = new StreamWriter(filePath, true, Encoding.UTF8);
                if (writeHeader)
                    await writer.WriteLineAsync("Time,HC1_X,HC1_Y,HC2_X,HC2_Y");
                for (int i = 0; i < 20; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var hc1 = await _sequenceService.VisionAFNoResult(
                        CameraType.HC1_HIGH, MarkType.FIDUCIAL, DirectType.LEFT, _cts.Token);
                    var hc2 = await _sequenceService.VisionAFNoResult(
                        CameraType.HC2_HIGH, MarkType.FIDUCIAL, DirectType.RIGHT, _cts.Token);
                    RepeatCurrent = i + 1;
                    await writer.WriteLineAsync($"{DateTime.Now},{hc1.x:F6},{hc1.y:F6},{hc2.x:F6},{hc2.y:F6}");
                    await writer.FlushAsync();
                    if (i < 19)
                        await Task.Delay(TimeSpan.FromSeconds(20), _cts.Token);
                }
                _logger.Information("FidNoAF CSV 저장 완료: {Path} ({Count}건)", filePath, RepeatCurrent);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("FidNoAF 측정 취소 — {Count}회 완료", RepeatCurrent);
            }
            catch (Exception e)
            {
                _logger.Warning("FidNoAF 측정 실패: {Msg}", e.Message);
            }
            finally
            {
                IsRepeatRunning = false;
            }
        }

        [RelayCommand]
        public async Task AlignAFCheck()
        {
            ResetCts();
            IsRepeatRunning = true;
            RepeatTotal = 20;
            RepeatCurrent = 0;
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeasureResults");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, "AlignAF.csv");
            try
            {
                bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
                using var writer = new StreamWriter(filePath, true, Encoding.UTF8);
                if (writeHeader)
                    await writer.WriteLineAsync("Time, HC1_X,HC1_Y,HC2_X,HC2_Y");
                for (int i = 0; i < 20; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var hc1 = await _sequenceService.VisionAFResult(
                        CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, _cts.Token);
                    var hc2 = await _sequenceService.VisionAFResult(
                        CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, _cts.Token);

                    RepeatCurrent = i + 1;
                    await writer.WriteLineAsync($"{DateTime.Now.ToString()},{hc1.x:F6},{hc1.y:F6},{hc2.x:F6},{hc2.y:F6}");
                    await writer.FlushAsync();
                }
                _logger.Information("AlignAF(AF) CSV 저장 완료: {Path} ({Count}건)", filePath, RepeatCurrent);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("AlignAF(AF) 측정 취소 — {Count}회 완료", RepeatCurrent);
            }
            catch (Exception e)
            {
                _logger.Warning("AlignAF(AF) 측정 실패: {Msg}", e.Message);
            }
            finally
            {
                IsRepeatRunning = false;
            }
        }

        [RelayCommand]
        public async Task AlignNoAFCheck()
        {
            ResetCts();
            IsRepeatRunning = true;
            RepeatTotal = 20;
            RepeatCurrent = 0;
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MeasureResults");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, "AlignAFNO.csv");
            try
            {
                bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
                using var writer = new StreamWriter(filePath, true, Encoding.UTF8);
                if (writeHeader)
                    await writer.WriteLineAsync("Time,HC1_X,HC1_Y,HC2_X,HC2_Y");
                for (int i = 0; i < 20; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var hc1 = await _sequenceService.VisionAFNoResult(
                        CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, _cts.Token);
                    var hc2 = await _sequenceService.VisionAFNoResult(
                        CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, _cts.Token);
                    RepeatCurrent = i + 1;
                    await writer.WriteLineAsync($"{DateTime.Now},{hc1.x:F6},{hc1.y:F6},{hc2.x:F6},{hc2.y:F6}");
                    await writer.FlushAsync();
                    if (i < 19)
                        await Task.Delay(TimeSpan.FromSeconds(20), _cts.Token);
                }
                _logger.Information("AlignNoAF CSV 저장 완료: {Path} ({Count}건)", filePath, RepeatCurrent);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("AlignNoAF 측정 취소 — {Count}회 완료", RepeatCurrent);
            }
            catch (Exception e)
            {
                _logger.Warning("AlignNoAF 측정 실패: {Msg}", e.Message);
            }
            finally
            {
                IsRepeatRunning = false;
            }
        }


        // 마지막 입력값 보존용
        private int _repeatCount = 5;
        // ═════════════════════════════════════════════════════
        //  공개 Reset 메서드 (외부에서 상태 초기화 시 사용)
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
            try
            {
                await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
                TopDie = 0;
                VisionTopLowAlign = null;
                _alignCtx = null;
                TopLowAlignState = TopPickupState = TopHighAlignState
                                  = TopPlaceState = StepState.Idle;
            }
            catch { throw; }
        }

        // ═════════════════════════════════════════════════════
        //  내부 Run* 메서드 — 상태 업데이트 + 서비스 위임
        // ═════════════════════════════════════════════════════

        private async Task RunTopLowAlign(CancellationToken ct)
        {
            TopLowAlignState = StepState.InProgress;
            VisionTopLowAlign = await _sequenceService.TopLowAlign(TopDie, ct);
            TopLowAlignState = StepState.Completed;
        }

        private async Task RunTopPickup(CancellationToken ct)
        {
            TopPickupState = StepState.InProgress;
            await _sequenceService.TopPickup(TopDie, VisionTopLowAlign, ct);
            TopPickupState = StepState.Completed;
        }

        private async Task RunTopHighAlign(CancellationToken ct)
        {
            TopHighAlignState = StepState.InProgress;

            _alignCtx = await _sequenceService.TopHighAlign(new AlignContext(), ct);

            // UI 바인딩 동기화
            TopRightFid = _alignCtx.TopRightFid;
            TopRightAlign = _alignCtx.TopRightAlign;
            TopLeftFid = _alignCtx.TopLeftFid;
            TopLeftAlign = _alignCtx.TopLeftAlign;
            TopOffsetX = _alignCtx.TopOffsetX;
            TopOffsetY = _alignCtx.TopOffsetY;
            TopOffsetT = _alignCtx.TopOffsetT;
            TopAlignRelOffsetX = _alignCtx.TopAlignRelOffsetX;
            TopAlignRelOffsetY = _alignCtx.TopAlignRelOffsetY;
            TopAlignRelOffsetT = _alignCtx.TopAlignRelOffsetT;

            TopHighAlignState = StepState.Completed;
        }


        private async Task RunBtmHighAlign(CancellationToken ct)
        {
            BtmHighAlignState = StepState.InProgress;

            _alignCtx = await _sequenceService.BtmHighAlign(_alignCtx, ct);

            // UI 바인딩 동기화
            BtmRightFid = _alignCtx.BtmRightFid;
            BtmRightAlign = _alignCtx.BtmRightAlign;
            BtmLeftFid = _alignCtx.BtmLeftFid;
            BtmLeftAlign = _alignCtx.BtmLeftAlign;
            BtmOffsetX = _alignCtx.BtmOffsetX;
            BtmOffsetY = _alignCtx.BtmOffsetY;
            BtmOffsetT = _alignCtx.BtmOffsetT;

            BtmHighAlignState = StepState.Completed;
        }

        private async Task RunTopPlace(CancellationToken ct)
        {
            TopBondingState = StepState.InProgress;
            await _sequenceService.TopPlace(_alignCtx, _recipeService, ct);
            await HighResult();
            ExportBondingResult();
            TopBondingState = StepState.Completed;
        }

        // ═════════════════════════════════════════════════════
        //  공통 유틸
        // ═════════════════════════════════════════════════════

        /// <summary>CTS를 교체한다. 이전 CTS가 있으면 먼저 취소 후 Dispose.</summary>
        /// 

        private void ResetCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        /// <summary>현재 상태가 InProgress일 때만 교체한다.</summary>
        private static StepState IfInProgress(StepState current, StepState next)
            => current == StepState.InProgress ? next : current;

        /// <summary>ShowDialog()를 별도 STA 스레드에서 실행해 메인 UI를 블로킹하지 않는다.</summary>
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
        //  본딩 결과 CSV 저장
        // ═════════════════════════════════════════════════════


        public void ExportBondingResult()
        {
            if (_alignCtx == null)
            {
                _logger.Information("저장할 본딩 결과가 없습니다.");
                return;
            }

            // ★ 날짜만 사용 → 같은 날은 같은 파일에 누적
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"bonding_result2_{DateTime.Now:yyyyMMdd}.csv");

            var fileExists = File.Exists(path);
            var sb = new StringBuilder();

            // ── 헤더 (파일이 처음 만들어질 때만) ──
            if (!fileExists)
            {
                sb.AppendLine(string.Join(",",
                    "타임스탬프",
                    "Top다이번호",
                    "Bottom다이번호",

                    "Top우측피듀셜_스테이지X", "Top우측피듀셜_스테이지Y",
                    "Top우측피듀셜_카메라마크편차X", "Top우측피듀셜_카메라마크편차Y",
                    "Top우측피듀셜_마크절대X", "Top우측피듀셜_마크절대Y",

                    "Top우측얼라인_스테이지X", "Top우측얼라인_스테이지Y",
                    "Top우측얼라인_카메라마크편차X", "Top우측얼라인_카메라마크편차Y",
                    "Top우측얼라인_마크절대X", "Top우측얼라인_마크절대Y",

                    "Top좌측피듀셜_스테이지X", "Top좌측피듀셜_스테이지Y",
                    "Top좌측피듀셜_카메라마크편차X", "Top좌측피듀셜_카메라마크편차Y",
                    "Top좌측피듀셜_마크절대X", "Top좌측피듀셜_마크절대Y",

                    "Top좌측얼라인_스테이지X", "Top좌측얼라인_스테이지Y",
                    "Top좌측얼라인_카메라마크편차X", "Top좌측얼라인_카메라마크편차Y",
                    "Top좌측얼라인_마크절대X", "Top좌측얼라인_마크절대Y",

                    "Top절대보정X", "Top절대보정Y", "Top절대보정θ",
                    "Top상대보정X", "Top상대보정Y", "Top상대보정θ",

                    "Btm우측피듀셜_스테이지X", "Btm우측피듀셜_스테이지Y",
                    "Btm우측피듀셜_카메라마크편차X", "Btm우측피듀셜_카메라마크편차Y",
                    "Btm우측피듀셜_마크절대X", "Btm우측피듀셜_마크절대Y",

                    "Btm우측얼라인_스테이지X", "Btm우측얼라인_스테이지Y",
                    "Btm우측얼라인_카메라마크편차X", "Btm우측얼라인_카메라마크편차Y",
                    "Btm우측얼라인_마크절대X", "Btm우측얼라인_마크절대Y",

                    "Btm좌측피듀셜_스테이지X", "Btm좌측피듀셜_스테이지Y",
                    "Btm좌측피듀셜_카메라마크편차X", "Btm좌측피듀셜_카메라마크편차Y",
                    "Btm좌측피듀셜_마크절대X", "Btm좌측피듀셜_마크절대Y",

                    "Btm좌측얼라인_스테이지X", "Btm좌측얼라인_스테이지Y",
                    "Btm좌측얼라인_카메라마크편차X", "Btm좌측얼라인_카메라마크편차Y",
                    "Btm좌측얼라인_마크절대X", "Btm좌측얼라인_마크절대Y",

                    "Btm절대보정X", "Btm절대보정Y", "Btm절대보정θ",

                    "측정얼라인각도_ThetaO(rad)",
                    "최종회전보정_ThetaF(rad)",
                    "최종X이동량(mm)",
                    "최종Y이동량(mm)",
                    "적용레시피오프셋X(mm)",
                    "적용레시피오프셋Y(mm)",
                    "적용레시피오프셋θ(mm)",
                    "결과BL_X",
                    "결과BL_Y",
                    "결과BR_X",
                    "결과BR_Y",
                    "결과TL_X",
                    "결과TL_Y",
                    "결과TR_X",
                    "결과TR_Y",
                    "결과C_X",
                    "결과C_Y",
                    "결과C_T"
                ));
            }

            var ctx = _alignCtx;
            sb.AppendLine(string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TopDie,
                BottomDie,

                MarkFields(ctx.TopRightFid),
                MarkFields(ctx.TopRightAlign),
                MarkFields(ctx.TopLeftFid),
                MarkFields(ctx.TopLeftAlign),

                ctx.TopOffsetX, ctx.TopOffsetY, ctx.TopOffsetT,
                ctx.TopAlignRelOffsetX, ctx.TopAlignRelOffsetY, ctx.TopAlignRelOffsetT,

                MarkFields(ctx.BtmRightFid),
                MarkFields(ctx.BtmRightAlign),
                MarkFields(ctx.BtmLeftFid),
                MarkFields(ctx.BtmLeftAlign),

                ctx.BtmOffsetX, ctx.BtmOffsetY, ctx.BtmOffsetT,

                ctx.FinalThetaO,
                ctx.FinalThetaF,
                ctx.FinalShiftX,
                ctx.FinalShiftY,
                ctx.OffsetXApplied,
                ctx.OffsetYApplied,
                ctx.OffsetTApplied,
                HrBlX, HrBlY,
                HrBrX, HrBrY,
                HrTlX, HrTlY,
                HrTrX, HrTrY,
                DetailX, DetailY, DetailT
            ));

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            _logger.Information("본딩 결과 저장: {Path}", path);
        }
        /// <summary>VisionMarkResult 6개 필드를 콤마로 이어 반환</summary>
        private static string MarkFields(VisionMarkResult m)
        {
            if (m == null) return ",,,,,";
            return string.Join(",",
                m.StageX,        // 측정 시 스테이지 위치
                m.StageY,
                m.DxCamToMark,   // 카메라 중심 → 마크 중심 편차
                m.DyCamToMark,
                m.CenterX,       // 보정 후 마크 절대좌표
                m.CenterY);
        }

        private static void WriteMarkRow(StringBuilder sb, string label, VisionMarkResult mark)
        {
            if (mark == null)
            {
                sb.AppendLine($"{label},(미측정),,,,, ");
                return;
            }

            sb.AppendLine(string.Join(",",
                label,
                mark.StageX,       // 측정 당시 스테이지 X 위치
                mark.StageY,       // 측정 당시 스테이지 Y 위치
                mark.DxCamToMark,  // 카메라 센터 → 마크 센터 X 편차
                mark.DyCamToMark,  // 카메라 센터 → 마크 센터 Y 편차
                mark.CenterX,      // 마크 절대좌표 X (= StageX + Dx 보정)
                mark.CenterY       // 마크 절대좌표 Y (= StageY + Dy 보정)
            ));
        }

        private void ExportToCsv(List<AlignContext> results, string filePath)
        {
            var sb = new StringBuilder();

            // 헤더
            sb.AppendLine(
                "Index," +
                "TopRightFid_StageX,TopRightFid_StageY,TopRightFid_DxCamToMark,TopRightFid_DyCamToMark,TopRightFid_CenterX,TopRightFid_CenterY," +
                "TopRightAlign_StageX,TopRightAlign_StageY,TopRightAlign_DxCamToMark,TopRightAlign_DyCamToMark,TopRightAlign_CenterX,TopRightAlign_CenterY," +
                "TopLeftFid_StageX,TopLeftFid_StageY,TopLeftFid_DxCamToMark,TopLeftFid_DyCamToMark,TopLeftFid_CenterX,TopLeftFid_CenterY," +
                "TopLeftAlign_StageX,TopLeftAlign_StageY,TopLeftAlign_DxCamToMark,TopLeftAlign_DyCamToMark,TopLeftAlign_CenterX,TopLeftAlign_CenterY"
            );

            // 데이터 행
            for (int i = 0; i < results.Count; i++)
            {
                var ctx = results[i];
                sb.AppendLine(string.Join(",",
                    i + 1,
                    // TopRightFid
                    ctx.TopRightFid.StageX, ctx.TopRightFid.StageY,
                    ctx.TopRightFid.DxCamToMark, ctx.TopRightFid.DyCamToMark,
                    ctx.TopRightFid.CenterX, ctx.TopRightFid.CenterY,
                    // TopRightAlign
                    ctx.TopRightAlign.StageX, ctx.TopRightAlign.StageY,
                    ctx.TopRightAlign.DxCamToMark, ctx.TopRightAlign.DyCamToMark,
                    ctx.TopRightAlign.CenterX, ctx.TopRightAlign.CenterY,
                    // TopLeftFid
                    ctx.TopLeftFid.StageX, ctx.TopLeftFid.StageY,
                    ctx.TopLeftFid.DxCamToMark, ctx.TopLeftFid.DyCamToMark,
                    ctx.TopLeftFid.CenterX, ctx.TopLeftFid.CenterY,
                    // TopLeftAlign
                    ctx.TopLeftAlign.StageX, ctx.TopLeftAlign.StageY,
                    ctx.TopLeftAlign.DxCamToMark, ctx.TopLeftAlign.DyCamToMark,
                    ctx.TopLeftAlign.CenterX, ctx.TopLeftAlign.CenterY
                ));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}