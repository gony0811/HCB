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

        [ObservableProperty]
        private ObservableCollection<VernierRow> vernierRows = new();

        [ObservableProperty] private bool avgMode = false;

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
        public async Task HighResult()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
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
                        V1X  = result.v1[i].X,
                        V1Y  = result.v1[i].Y,
                        V3X  = result.v3.Count > i ? result.v3[i].X : (double?)null,
                        V3Y  = result.v3.Count > i ? result.v3[i].Y : (double?)null,
                    });
                }
                _logger.Information("Vernier 측정 완료 — {Count}포인트", result.v1.Count);
            }
            catch (Exception e) { _logger.Warning("Vernier 측정 실패: {Msg}", e.Message); }
        }

        [RelayCommand]
        public void ExportHighResult()
        {
            if (VernierRows.Count == 0)
            {
                _logger.Information("저장할 Vernier 결과가 없습니다. 먼저 결과 측정을 실행하세요.");
                return;
            }

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"vernier_{DateTime.Now:yyyyMMdd}.csv");

            bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            var sb = new StringBuilder();
            if (writeHeader)
                sb.AppendLine("Time,Pos,V1_X,V1_Y,V3_X,V3_Y");

            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            foreach (var row in VernierRows)
                sb.AppendLine($"{ts},{row.Name},{Fn(row.V1X)},{Fn(row.V1Y)},{Fn(row.V3X)},{Fn(row.V3Y)}");

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            _logger.Information("Vernier CSV 저장: {Path}", path);
        }

        

        [RelayCommand]
        public void ChangeAvgMode()
        {
            AvgMode = !AvgMode;
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
            var data = new AlignData();
            data.AvgMove = AvgMode;
            hcbData = await _sequenceService.TopHighAlign(data, ct);

            TopRightFid = hcbData.TopRightFidRaw;
            TopRightAlign = hcbData.TopRightAlignRaw;
            TopLeftFid = hcbData.TopLeftFidRaw;
            TopLeftAlign = hcbData.TopLeftAlignRaw;

            TopHighAlignState = StepState.Completed;
        }

        private async Task RunBtmHighAlign(CancellationToken ct)
        {
            BtmHighAlignState = StepState.InProgress;

            hcbData = await _sequenceService.BtmHighAlign(hcbData, ct);

            BtmRightFid = hcbData.BtmRightFidRaw;
            BtmRightAlign = hcbData.BtmRightAlignRaw;
            BtmLeftFid = hcbData.BtmLeftFidRaw;
            BtmLeftAlign = hcbData.BtmLeftAlignRaw;

            BtmHighAlignState = StepState.Completed;
        }

        private async Task RunTopPlace(CancellationToken ct)
        {
            TopBondingState = StepState.InProgress;
            BondingHistory = new ObservableCollection<BondingDataPoint>();
            await _sequenceService.TopPlace(hcbData, ct);
            await _sequenceService.Bonding(BondingHistory, ct);
            TopBondingState = StepState.Completed;
            ExportHcbData();
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

        /// <summary>VisionMarkResult 6개 필드를 콤마로 이어 반환</summary>
        private void ExportHcbData()
        {
            if (hcbData == null)
            {
                _logger.Information("저장할 본딩 데이터가 없습니다.");
                return;
            }

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"bonding_hcb_{DateTime.Now:yyyyMMdd}.csv");

            bool writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
            var sb = new StringBuilder();

            if (writeHeader)
            {
                sb.AppendLine(string.Join(",",
                    "Time", "AvgMode",
                    // Top 비전
                    "TopRF_StageX", "TopRF_StageY", "TopRF_DxCam", "TopRF_DyCam", "TopRF_CenterX", "TopRF_CenterY",
                    "TopRA_StageX", "TopRA_StageY", "TopRA_DxCam", "TopRA_DyCam", "TopRA_CenterX", "TopRA_CenterY",
                    "TopLF_StageX", "TopLF_StageY", "TopLF_DxCam", "TopLF_DyCam", "TopLF_CenterX", "TopLF_CenterY",
                    "TopLA_StageX", "TopLA_StageY", "TopLA_DxCam", "TopLA_DyCam", "TopLA_CenterX", "TopLA_CenterY",
                    // Btm 비전
                    "BtmRF_StageX", "BtmRF_StageY", "BtmRF_DxCam", "BtmRF_DyCam", "BtmRF_CenterX", "BtmRF_CenterY",
                    "BtmRA_StageX", "BtmRA_StageY", "BtmRA_DxCam", "BtmRA_DyCam", "BtmRA_CenterX", "BtmRA_CenterY",
                    "BtmLF_StageX", "BtmLF_StageY", "BtmLF_DxCam", "BtmLF_DyCam", "BtmLF_CenterX", "BtmLF_CenterY",
                    "BtmLA_StageX", "BtmLA_StageY", "BtmLA_DxCam", "BtmLA_DyCam", "BtmLA_CenterX", "BtmLA_CenterY",
                    // 캘리브레이션 파라미터
                    "PcTRad", "Hc1Rad", "Hc2Rad",
                    "Hcro_X", "Hcro_Y",
                    "Hc2Offset_X", "Hc2Offset_Y",
                    // 레시피 오프셋
                    "OffsetX", "OffsetY", "OffsetT"
                ));
            }

            sb.AppendLine(string.Join(",",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                hcbData.AvgMove,
                // Top 비전
                MarkFields(hcbData.TopRightFidRaw),
                MarkFields(hcbData.TopRightAlignRaw),
                MarkFields(hcbData.TopLeftFidRaw),
                MarkFields(hcbData.TopLeftAlignRaw),
                // Btm 비전
                MarkFields(hcbData.BtmRightFidRaw),
                MarkFields(hcbData.BtmRightAlignRaw),
                MarkFields(hcbData.BtmLeftFidRaw),
                MarkFields(hcbData.BtmLeftAlignRaw),
                // 캘리브레이션 파라미터
                F(hcbData.PcTRad), F(hcbData.Hc1Rad), F(hcbData.Hc2Rad),
                F(hcbData.Hcro.X), F(hcbData.Hcro.Y),
                F(hcbData.Hc2Offset.X), F(hcbData.Hc2Offset.Y),
                // 레시피 오프셋
                F(hcbData.OffsetXY.X), F(hcbData.OffsetXY.Y), F(hcbData.OffsetT)
            ));

            File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            _logger.Information("본딩 데이터 저장: {Path}", path);
        }

        private static string MarkFields(VisionMarkResult m)
        {
            if (m == null) return ",,,,,";
            return string.Join(",",
                F(m.StageX), F(m.StageY),
                F(m.DxCamToMark), F(m.DyCamToMark),
                F(m.CenterX), F(m.CenterY));
        }

      

        /// <summary>double → 소수점 6자리 문자열</summary>
        private static string F(double v) => v.ToString("F6");

        /// <summary>nullable double → 소수점 6자리 문자열 (null이면 빈 문자열)</summary>
        private static string Fn(double? v) => v.HasValue ? v.Value.ToString("F6") : string.Empty;

       
    }
}