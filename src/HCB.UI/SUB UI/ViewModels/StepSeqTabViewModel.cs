using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class StepSeqTabViewModel : ObservableObject
    {
        private CancellationTokenSource _cts;
        private readonly ILogger _logger;
        private readonly SequenceHelper _sequenceHelper;
        private readonly SequenceService _sequenceService;
        private readonly DeviceManager _deviceManager;
        private IOManager ioManager;

        [ObservableProperty] private int topDie = 0;
        [ObservableProperty] private int bottomDie = 0;

        [ObservableProperty] private VisionMarkPositionResponse visionBtmLowAlign;
        [ObservableProperty] private VisionMarkPositionResponse visionTopLowAlign;

        [ObservableProperty] private VisionMarkResult topRightAlign;
        [ObservableProperty] private VisionMarkResult topRightFid;
        [ObservableProperty] private VisionMarkResult topLeftAlign;
        [ObservableProperty] private VisionMarkResult topLeftFid;

        [ObservableProperty] private double errorT;

        // ── Step Lamp States ─────────────────────────────────
        [ObservableProperty] private StepState initState        = StepState.Idle;

        [ObservableProperty] private StepState btmLowAlignState = StepState.Idle;
        [ObservableProperty] private StepState btmPickupState   = StepState.Idle;
        [ObservableProperty] private StepState btmPlaceState    = StepState.Idle;

        [ObservableProperty] private StepState topLowAlignState  = StepState.Idle;
        [ObservableProperty] private StepState topPickupState    = StepState.Idle;
        [ObservableProperty] private StepState topHighAlignState = StepState.Idle;
        [ObservableProperty] private StepState topPlaceState     = StepState.Idle;

        [ObservableProperty] private bool isInitInfoOpen;

        public SequenceServiceVM SequenceServiceVM { get; }

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new ObservableCollection<SensorIoItemViewModel>();

        private readonly List<string> dTableNameList = new List<string>
        {
            "DIE 1", "DIE 2", "DIE 3", "DIE 4", "DIE 5", "DIE 6", "DIE 7", "DIE 8", "DIE 9",
        };

        private readonly List<string> dIoNameList = new List<string>
        {
            IoExtensions.DO_DTABLE_VAC_1_ON, IoExtensions.DO_DTABLE_VAC_2_ON, IoExtensions.DO_DTABLE_VAC_3_ON,
            IoExtensions.DO_DTABLE_VAC_4_ON, IoExtensions.DO_DTABLE_VAC_5_ON, IoExtensions.DO_DTABLE_VAC_6_ON,
            IoExtensions.DO_DTABLE_VAC_7_ON, IoExtensions.DO_DTABLE_VAC_8_ON, IoExtensions.DO_DTABLE_VAC_9_ON,
        };

        public StepSeqTabViewModel(
            SequenceServiceVM sequenceServiceVM,
            SequenceService sequenceService,
            DeviceManager deviceManager,
            IOManager ioManager,
            ILogger logger)
        {
            _logger = logger.ForContext<StepSeqTabViewModel>();
            this.SequenceServiceVM = sequenceServiceVM;
            this._sequenceService = sequenceService;
            this._deviceManager = deviceManager;
            this.ioManager = ioManager;

            var ioDevice = this._deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            if (ioDevice != null)
            {
                for (var i = 0; i < dTableNameList.Count; i++)
                {
                    var result = ioManager.CreateIoVM(dTableNameList[i], dIoNameList[i], dTableNameList[i]);
                    if (result != null) DTableList.Add(result);
                }
            }
        }

        // ── STOP ─────────────────────────────────────────────
        [RelayCommand]
        public async Task Stop()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                await _sequenceService.StopAsync(_cts.Token);
                InitState = StepState.Idle;
                BtmLowAlignState = BtmPickupState = BtmPlaceState = StepState.Idle;
                TopLowAlignState = TopPickupState = TopHighAlignState = TopPlaceState = StepState.Idle;
            }
        }

        // ── INIT ─────────────────────────────────────────────
        [RelayCommand]
        public async Task Init()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
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

        // Die Load
        [RelayCommand]
        public async Task DieLoad()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            await _sequenceService.DTableLoading(_cts.Token);
            var tcs    = new TaskCompletionSource<bool>();
            var dialog = new VacuumSelector();
            dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;

            dialog.Closed += (s, e) => tcs.SetResult(dialog.DialogResult == true);
            dialog.ShowDialog();

            bool confirmed = await tcs.Task;
            if (!confirmed) return;

            var topList = dialog.TopDieVacuums;  // List
            var botList = dialog.BotDieVacuums;  // List

            if (topList.Count > 0) TopDie    = topList[0];
            if (botList.Count  > 0) BottomDie = botList[0];
            
            _logger.Information(
                "Die Load 선택 완료 — TOP: [{Top}]  BOT: [{Bot}]",
                string.Join(", ", topList),
                string.Join(", ", botList));

            List<int> vacs = new List<int> { TopDie, BottomDie };
            await _sequenceService.DTableLoadComplete(vacs, _cts.Token);
        }

        [RelayCommand]
        public void InitInfo() => IsInitInfoOpen = true;

        [RelayCommand]
        public void CloseInitInfo() => IsInitInfoOpen = false;

        // BOTTOM ALIGN 
        [RelayCommand]
        public async Task BtmLowAlign()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }
                BtmLowAlignState = StepState.InProgress;
                VisionBtmLowAlign = await _sequenceService.DTableCarrierAlign(BottomDie, MarkType.DIE_CENTER_BOTTOM, _cts.Token);
                BtmLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { BtmLowAlignState = StepState.Idle; }
            catch (Exception e)
            {
                BtmLowAlignState =  StepState.Failed;
                _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmPickup()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (BottomDie == 0) { _logger.Information("Bottom Die를 Load해주세요"); return; }
                if (VisionBtmLowAlign == null) { _logger.Information("Bottom Die Align 해주세요"); return; }
                BtmPickupState = StepState.InProgress;
                await _sequenceService.DTableBTMPickup(BottomDie, VisionBtmLowAlign, _cts.Token);
                DTableList[BottomDie - 1].Off();
                BtmPickupState = StepState.Completed;
                
            }
            catch (OperationCanceledException) { BtmPickupState = StepState.Idle; }
            catch (Exception e) { BtmPickupState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task BtmPlace()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                BtmPlaceState = StepState.InProgress;
                await _sequenceService.BtmDieDrop(1, _cts.Token);
                BtmPlaceState = StepState.Completed;
                
            }
            catch (OperationCanceledException) { BtmPlaceState = StepState.Idle; }
            catch (Exception e) { BtmPlaceState = StepState.Failed; _logger.Warning(e.Message); }
        }

        // ── TOP ALIGN ────────────────────────────────────────
        [RelayCommand]
        public async Task TopLowAlign()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                TopLowAlignState = StepState.InProgress;
                VisionTopLowAlign = await _sequenceService.DTableCarrierAlign(TopDie, MarkType.DIE_CENTER_TOP, _cts.Token);
                TopLowAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopLowAlignState = StepState.Idle; }
            catch (Exception e) { TopLowAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopPickup()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                if (TopDie == 0) { _logger.Information("Top Die를 Load해주세요"); return; }
                if (VisionTopLowAlign == null) { _logger.Information("Top Die Align 해주세요"); return; }
                TopPickupState = StepState.InProgress;
                await _sequenceService.DTableTOPPickup(TopDie, VisionTopLowAlign, _cts.Token);
                DTableList[TopDie - 1].Off();
                TopPickupState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopPickupState = StepState.Idle; }
            catch (Exception e) { TopPickupState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopHighAlign()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                TopHighAlignState = StepState.InProgress;
                TopRightFid   = await _sequenceService.TopDieVisionRightFid(_cts.Token);
                TopRightAlign = await _sequenceService.TopDieVisionRightAlign(_cts.Token);
                TopLeftFid    = await _sequenceService.TopDieVisionLeftFid(_cts.Token);
                TopLeftAlign  = await _sequenceService.TopDieVisionLeftAlign(_cts.Token);
                TopHighAlignState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopHighAlignState = StepState.Idle; }
            catch (Exception e) { TopHighAlignState = StepState.Failed; _logger.Warning(e.Message); }
        }

        [RelayCommand]
        public async Task TopPlace()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                TopPlaceState = StepState.InProgress;
                await _sequenceService.TopDieDrop(_cts.Token);
                TopPlaceState = StepState.Completed;
            }
            catch (OperationCanceledException) { TopPlaceState = StepState.Idle; }
            catch (Exception e) { TopPlaceState = StepState.Failed; _logger.Warning(e.Message); }
        }


        public async Task BtmInit()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
            BottomDie = 0;
            BtmLowAlignState = StepState.Idle;
            BtmPickupState = StepState.Idle;
            BtmPlaceState = StepState.Idle;
            VisionBtmLowAlign = null;
        }

        public async Task TopInit()
        {
            
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, _cts.Token);
                TopDie = 0;
                TopLowAlignState = StepState.Idle;
                TopPickupState = StepState.Idle;
                TopHighAlignState = StepState.Idle;
                TopPlaceState = StepState.Idle;
                VisionTopLowAlign = null;
            }
            catch(Exception e)
            {
                throw;
            }
            

            
        }
    }
}
