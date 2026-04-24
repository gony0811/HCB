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

        public string RepeatProgressText =>
            IsRepeatRunning ? $"{RepeatCurrent} / {RepeatTotal}" : string.Empty;

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
        public void TopHighAlignInfo()
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
        //  NoAlgorithmRepeat
        //  ★ Init_Head 추가로 헤드 원점 복귀 → 누적 오차 방지
        // ═════════════════════════════════════════════════════

        [RelayCommand]
        public async Task NoAlgorithmRepeat()
        {
            ResetCts();
            var ct = _cts.Token;

            RepeatCurrent = 0;
            IsRepeatRunning = true;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    RepeatCurrent++;

                    TopHighAlignState = StepState.Idle;
                    BtmHighAlignState = StepState.Idle;

                    await RunTopHighAlign(ct);
                    await RunBtmHighAlign(ct);
                    ExportBondingResult();

                    await _sequenceService.Init_Head(ct);

                    _logger.Information("NoAlgorithm 반복 #{Count} 완료", RepeatCurrent);
                }
            }
            catch (OperationCanceledException)
            {
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Idle);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Idle);
                _logger.Information("NoAlgorithm 반복 중단 — {Count}회 완료 후 취소", RepeatCurrent);
            }
            catch (Exception e)
            {
                TopHighAlignState = IfInProgress(TopHighAlignState, StepState.Failed);
                BtmHighAlignState = IfInProgress(BtmHighAlignState, StepState.Failed);
                _logger.Warning("NoAlgorithm 반복 실패 #{Count}: {Msg}", RepeatCurrent, e.Message);
            }
            finally
            {
                IsRepeatRunning = false;
            }
        }

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

        [RelayCommand]
        public async Task HighResult()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                await _sequenceService.TopDiePlace(_cts.Token);
                var result = await _sequenceService.GetVernier(_cts.Token);
                VernierResult = result;
            }
            catch (Exception e)
            {
            }
        }

        [RelayCommand]
        public async Task RepeatMeasure()
        {
            var dialog = new RepeatMeasureDialog(_repeatCount);
            if (dialog.ShowDialog() != true) return;

            int repeat = dialog.RepeatCount;
            _repeatCount = repeat;

            ResetCts();
            RepeatTotal = repeat;
            RepeatCurrent = 0;
            IsRepeatRunning = true;

            var results = new List<AlignContext>();

            try
            {
                for (int i = 0; i < repeat; i++)
                {
                    RepeatCurrent = i + 1;
                    var result = await _sequenceService.TopHighAlign(
                        new AlignContext(), _cts.Token);
                    results.Add(result);
                }
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"align_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                ExportToCsv(results, path);
            }
            catch (OperationCanceledException) { }
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

        private int _repeatCount = 5;

        // ═════════════════════════════════════════════════════
        //  공개 Reset 메서드
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
        //  내부 Run* 메서드
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

            // UI 바인딩 — 하위 호환 프로퍼티 (Corrected ?? Raw) 자동 반환
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

            // UI 바인딩 — 하위 호환 프로퍼티 (Corrected ?? Raw) 자동 반환
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
            //await HighResult();
            ExportBondingResult();
            TopBondingState = StepState.Completed;
        }

        // ═════════════════════════════════════════════════════
        //  공통 유틸
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
        //  CSV 내보내기 — Raw + Corrected + 보정 파라미터 + 결과
        // ═════════════════════════════════════════════════════

        /// <summary>
        /// 본딩 결과 CSV 저장
        /// 구조: [기본정보] [Top Raw] [Top Corrected] [Top Offset]
        ///       [Btm Raw] [Btm Corrected] [Btm Offset]
        ///       [보정 파라미터] [HcRO 좌표] [최종 결과] [Vernier]
        /// </summary>
        public void ExportBondingResult()
        {
            if (_alignCtx == null)
            {
                _logger.Information("저장할 본딩 결과가 없습니다.");
                return;
            }

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"bonding_result3_{DateTime.Now:yyyyMMdd}.csv");

            var fileExists = File.Exists(path);
            var sb = new StringBuilder();

            // ── 헤더 ──────────────────────────────────────────
            if (!fileExists)
            {
                var headers = new List<string>
                {
                    // ── 기본 정보 ──
                    "타임스탬프", "Top다이번호", "Bottom다이번호",

                    // ── Top Raw (비전 원본) ──
                    "TopRF_Raw_StageX", "TopRF_Raw_StageY",
                    "TopRF_Raw_DxCam",  "TopRF_Raw_DyCam",
                    "TopRF_Raw_CenterX","TopRF_Raw_CenterY",

                    "TopRA_Raw_StageX", "TopRA_Raw_StageY",
                    "TopRA_Raw_DxCam",  "TopRA_Raw_DyCam",
                    "TopRA_Raw_CenterX","TopRA_Raw_CenterY",

                    "TopLF_Raw_StageX", "TopLF_Raw_StageY",
                    "TopLF_Raw_DxCam",  "TopLF_Raw_DyCam",
                    "TopLF_Raw_CenterX","TopLF_Raw_CenterY",

                    "TopLA_Raw_StageX", "TopLA_Raw_StageY",
                    "TopLA_Raw_DxCam",  "TopLA_Raw_DyCam",
                    "TopLA_Raw_CenterX","TopLA_Raw_CenterY",

                    // ── Top Corrected (보정 후) ──
                    "TopRF_Cor_StageX", "TopRF_Cor_StageY",
                    "TopRF_Cor_DxCam",  "TopRF_Cor_DyCam",
                    "TopRF_Cor_CenterX","TopRF_Cor_CenterY",

                    "TopRA_Cor_StageX", "TopRA_Cor_StageY",
                    "TopRA_Cor_DxCam",  "TopRA_Cor_DyCam",
                    "TopRA_Cor_CenterX","TopRA_Cor_CenterY",

                    "TopLF_Cor_StageX", "TopLF_Cor_StageY",
                    "TopLF_Cor_DxCam",  "TopLF_Cor_DyCam",
                    "TopLF_Cor_CenterX","TopLF_Cor_CenterY",

                    "TopLA_Cor_StageX", "TopLA_Cor_StageY",
                    "TopLA_Cor_DxCam",  "TopLA_Cor_DyCam",
                    "TopLA_Cor_CenterX","TopLA_Cor_CenterY",

                    // ── Top Offset ──
                    "TopOffsetX", "TopOffsetY", "TopOffsetT",
                    "TopRelOffsetX", "TopRelOffsetY", "TopRelOffsetT",

                    // ── Btm Raw (비전 원본) ──
                    "BtmRF_Raw_StageX", "BtmRF_Raw_StageY",
                    "BtmRF_Raw_DxCam",  "BtmRF_Raw_DyCam",
                    "BtmRF_Raw_CenterX","BtmRF_Raw_CenterY",

                    "BtmRA_Raw_StageX", "BtmRA_Raw_StageY",
                    "BtmRA_Raw_DxCam",  "BtmRA_Raw_DyCam",
                    "BtmRA_Raw_CenterX","BtmRA_Raw_CenterY",

                    "BtmLF_Raw_StageX", "BtmLF_Raw_StageY",
                    "BtmLF_Raw_DxCam",  "BtmLF_Raw_DyCam",
                    "BtmLF_Raw_CenterX","BtmLF_Raw_CenterY",

                    "BtmLA_Raw_StageX", "BtmLA_Raw_StageY",
                    "BtmLA_Raw_DxCam",  "BtmLA_Raw_DyCam",
                    "BtmLA_Raw_CenterX","BtmLA_Raw_CenterY",

                    // ── Btm Corrected (보정 후) ──
                    "BtmRF_Cor_StageX", "BtmRF_Cor_StageY",
                    "BtmRF_Cor_DxCam",  "BtmRF_Cor_DyCam",
                    "BtmRF_Cor_CenterX","BtmRF_Cor_CenterY",

                    "BtmRA_Cor_StageX", "BtmRA_Cor_StageY",
                    "BtmRA_Cor_DxCam",  "BtmRA_Cor_DyCam",
                    "BtmRA_Cor_CenterX","BtmRA_Cor_CenterY",

                    "BtmLF_Cor_StageX", "BtmLF_Cor_StageY",
                    "BtmLF_Cor_DxCam",  "BtmLF_Cor_DyCam",
                    "BtmLF_Cor_CenterX","BtmLF_Cor_CenterY",

                    "BtmLA_Cor_StageX", "BtmLA_Cor_StageY",
                    "BtmLA_Cor_DxCam",  "BtmLA_Cor_DyCam",
                    "BtmLA_Cor_CenterX","BtmLA_Cor_CenterY",

                    // ── Btm Offset ──
                    "BtmOffsetX", "BtmOffsetY", "BtmOffsetT",

                    // ── 보정 파라미터 ──
                    "HasPcT", "PcTRad",
                    "HasHcRO", "Hc1Rad", "Hc2Rad",
                    "HcRO_X", "HcRO_Y",
                    "Hc1Offset_X", "Hc1Offset_Y",
                    "Hc2Offset_X", "Hc2Offset_Y",
                    "PcHcroScaleX", "PcHcroScaleY", "ScaleFallback",

                    // ── HcRO 좌표 (B-Die) ──
                    "HcroLF_X", "HcroLF_Y",
                    "HcroLA_X", "HcroLA_Y",
                    "HcroRF_X", "HcroRF_Y",
                    "HcroRA_X", "HcroRA_Y",

                    // ── HcRO 좌표 (T-Die) ──
                    "HcroTopLF_X", "HcroTopLF_Y",
                    "HcroTopLA_X", "HcroTopLA_Y",
                    "HcroTopRF_X", "HcroTopRF_Y",
                    "HcroTopRA_X", "HcroTopRA_Y",

                    // ── 최종 결과 ──
                    "ThetaO(rad)", "ThetaF(rad)",
                    "ShiftX(mm)", "ShiftY(mm)",
                    "OffsetX_Recipe", "OffsetY_Recipe", "OffsetT_Recipe",

                    // ── Vernier / HighResult ──
                    "결과BL_X", "결과BL_Y",
                    "결과BR_X", "결과BR_Y",
                    "결과TL_X", "결과TL_Y",
                    "결과TR_X", "결과TR_Y",
                    "결과C_X", "결과C_Y", "결과C_T"
                };

                sb.AppendLine(string.Join(",", headers));
            }

            // ── 데이터 행 ─────────────────────────────────────
            var ctx = _alignCtx;

            var values = new List<string>
            {
                // ── 기본 정보 ──
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TopDie.ToString(),
                BottomDie.ToString(),

                // ── Top Raw ──
                MarkFields(ctx.TopRightFidRaw),
                MarkFields(ctx.TopRightAlignRaw),
                MarkFields(ctx.TopLeftFidRaw),
                MarkFields(ctx.TopLeftAlignRaw),

                // ── Top Corrected ──
                MarkFields(ctx.TopRightFidCorrected),
                MarkFields(ctx.TopRightAlignCorrected),
                MarkFields(ctx.TopLeftFidCorrected),
                MarkFields(ctx.TopLeftAlignCorrected),

                // ── Top Offset ──
                F(ctx.TopOffsetX), F(ctx.TopOffsetY), F(ctx.TopOffsetT),
                F(ctx.TopAlignRelOffsetX), F(ctx.TopAlignRelOffsetY), F(ctx.TopAlignRelOffsetT),

                // ── Btm Raw ──
                MarkFields(ctx.BtmRightFidRaw),
                MarkFields(ctx.BtmRightAlignRaw),
                MarkFields(ctx.BtmLeftFidRaw),
                MarkFields(ctx.BtmLeftAlignRaw),

                // ── Btm Corrected ──
                MarkFields(ctx.BtmRightFidCorrected),
                MarkFields(ctx.BtmRightAlignCorrected),
                MarkFields(ctx.BtmLeftFidCorrected),
                MarkFields(ctx.BtmLeftAlignCorrected),

                // ── Btm Offset ──
                F(ctx.BtmOffsetX), F(ctx.BtmOffsetY), F(ctx.BtmOffsetT),

                // ── 보정 파라미터 ──
                ctx.HasPcT.ToString(), F(ctx.PcTRad),
                ctx.HasHcRO.ToString(), F(ctx.Hc1Rad), F(ctx.Hc2Rad),
                Pt(ctx.Hcro),
                Pt(ctx.Hc1Offset),
                Pt(ctx.Hc2Offset),
                F(ctx.PcHcroScaleX), F(ctx.PcHcroScaleY), ctx.ScaleFallbackApplied.ToString(),

                // ── HcRO 좌표 (B-Die) ──
                Pt(ctx.HcroLF), Pt(ctx.HcroLA),
                Pt(ctx.HcroRF), Pt(ctx.HcroRA),

                // ── HcRO 좌표 (T-Die) ──
                Pt(ctx.HcroTopLF), Pt(ctx.HcroTopLA),
                Pt(ctx.HcroTopRF), Pt(ctx.HcroTopRA),

                // ── 최종 결과 ──
                F(ctx.FinalThetaO), F(ctx.FinalThetaF),
                F(ctx.FinalShiftX), F(ctx.FinalShiftY),
                F(ctx.OffsetXApplied), F(ctx.OffsetYApplied), F(ctx.OffsetTApplied),

                // ── Vernier / HighResult ──
                F(HrBlX), F(HrBlY),
                F(HrBrX), F(HrBrY),
                F(HrTlX), F(HrTlY),
                F(HrTrX), F(HrTrY),
                F(DetailX), F(DetailY), F(DetailT)
            };

            sb.AppendLine(string.Join(",", values));

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            _logger.Information("본딩 결과 저장: {Path}", path);
        }

        // ═════════════════════════════════════════════════════
        //  RepeatMeasure 전용 CSV (Raw + Corrected 비교)
        // ═════════════════════════════════════════════════════

        private void ExportToCsv(List<AlignContext> results, string filePath)
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Join(",",
                "Index",
                // Raw
                "TopRF_Raw_StageX", "TopRF_Raw_StageY", "TopRF_Raw_DxCam", "TopRF_Raw_DyCam", "TopRF_Raw_CenterX", "TopRF_Raw_CenterY",
                "TopRA_Raw_StageX", "TopRA_Raw_StageY", "TopRA_Raw_DxCam", "TopRA_Raw_DyCam", "TopRA_Raw_CenterX", "TopRA_Raw_CenterY",
                "TopLF_Raw_StageX", "TopLF_Raw_StageY", "TopLF_Raw_DxCam", "TopLF_Raw_DyCam", "TopLF_Raw_CenterX", "TopLF_Raw_CenterY",
                "TopLA_Raw_StageX", "TopLA_Raw_StageY", "TopLA_Raw_DxCam", "TopLA_Raw_DyCam", "TopLA_Raw_CenterX", "TopLA_Raw_CenterY",
                // Corrected
                "TopRF_Cor_StageX", "TopRF_Cor_StageY", "TopRF_Cor_DxCam", "TopRF_Cor_DyCam", "TopRF_Cor_CenterX", "TopRF_Cor_CenterY",
                "TopRA_Cor_StageX", "TopRA_Cor_StageY", "TopRA_Cor_DxCam", "TopRA_Cor_DyCam", "TopRA_Cor_CenterX", "TopRA_Cor_CenterY",
                "TopLF_Cor_StageX", "TopLF_Cor_StageY", "TopLF_Cor_DxCam", "TopLF_Cor_DyCam", "TopLF_Cor_CenterX", "TopLF_Cor_CenterY",
                "TopLA_Cor_StageX", "TopLA_Cor_StageY", "TopLA_Cor_DxCam", "TopLA_Cor_DyCam", "TopLA_Cor_CenterX", "TopLA_Cor_CenterY",
                // Offset
                "TopOffsetX", "TopOffsetY", "TopOffsetT",
                "TopRelOffsetX", "TopRelOffsetY", "TopRelOffsetT",
                // 보정 파라미터
                "HasPcT", "PcTRad"
            ));

            for (int i = 0; i < results.Count; i++)
            {
                var ctx = results[i];
                sb.AppendLine(string.Join(",",
                    i + 1,
                    // Raw
                    MarkFields(ctx.TopRightFidRaw),
                    MarkFields(ctx.TopRightAlignRaw),
                    MarkFields(ctx.TopLeftFidRaw),
                    MarkFields(ctx.TopLeftAlignRaw),
                    // Corrected
                    MarkFields(ctx.TopRightFidCorrected),
                    MarkFields(ctx.TopRightAlignCorrected),
                    MarkFields(ctx.TopLeftFidCorrected),
                    MarkFields(ctx.TopLeftAlignCorrected),
                    // Offset
                    F(ctx.TopOffsetX), F(ctx.TopOffsetY), F(ctx.TopOffsetT),
                    F(ctx.TopAlignRelOffsetX), F(ctx.TopAlignRelOffsetY), F(ctx.TopAlignRelOffsetT),
                    // 보정 파라미터
                    ctx.HasPcT.ToString(), F(ctx.PcTRad)
                ));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        // ═════════════════════════════════════════════════════
        //  CSV 유틸
        // ═════════════════════════════════════════════════════

        /// <summary>VisionMarkResult 6개 필드를 콤마로 이어 반환</summary>
        private static string MarkFields(VisionMarkResult m)
        {
            if (m == null) return ",,,,,";
            return string.Join(",",
                F(m.StageX), F(m.StageY),
                F(m.DxCamToMark), F(m.DyCamToMark),
                F(m.CenterX), F(m.CenterY));
        }

        /// <summary>Point2D → "X,Y"</summary>
        private static string Pt(Point2D p)
        {
            if (p == null) return ",";
            return $"{F(p.X)},{F(p.Y)}";
        }

        /// <summary>double → 소수점 6자리 문자열</summary>
        private static string F(double v) => v.ToString("F6");

        private static void WriteMarkRow(StringBuilder sb, string label, VisionMarkResult mark)
        {
            if (mark == null)
            {
                sb.AppendLine($"{label},(미측정),,,,, ");
                return;
            }

            sb.AppendLine(string.Join(",",
                label,
                mark.StageX, mark.StageY,
                mark.DxCamToMark, mark.DyCamToMark,
                mark.CenterX, mark.CenterY));
        }
    }
}