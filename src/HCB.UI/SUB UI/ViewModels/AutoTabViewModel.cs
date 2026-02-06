using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class AutoTabViewModel : ObservableObject
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly SequenceService _sequenceService;
        private readonly OperationService _operationService;
        public readonly SequenceServiceVM _sequenceServiceVM;

        public RunInformation RunInformation { get; }
        public RunningStatus RunningStatus { get; }

        public AlarmService AlarmService { get; }

        [ObservableProperty]
        private bool isInitializing;

        [ObservableProperty]
        private bool isRunning;

        [ObservableProperty]
        private bool isStopping;

        public AutoTabViewModel(RunInformation runInformation, RunningStatus runningStatus, OperationService operationService, SequenceService sequenceService, AlarmService alarmService)
        {
            RunInformation = runInformation;
            RunningStatus = runningStatus;
            _sequenceService = sequenceService;
            _operationService = operationService;
            _cancellationTokenSource.TryReset();
            AlarmService = alarmService;
        }

        [RelayCommand]
        public void Loaded()
        {
            _operationService.Status.Operation = OperationMode.Auto;
        }

        [RelayCommand]      
        public void Unloaded() 
        {
            _operationService.Status.Operation = OperationMode.Manual;
        }

        [RelayCommand]
        public void Running()
        {
            RunningStatus.RunningTimeRange.StartTimer();
        }

        [RelayCommand]
        public void Loading()
        {
            RunningStatus.LoadingTimeRange.StartTimer();
        }

        [RelayCommand]
        public async Task MachineInit()
        {
            
            //try
            //{
            //    IsInitializing = true;

            //    // 시스템 체크
            //    _sequenceServiceVM.SystemCheck = StepState.InProgress;
            //    var systemCheckResult = await this._sequenceService.Init_PreCheck(_cancellationTokenSource.Token);
            //    if (systemCheckResult)
            //    {
            //        _sequenceServiceVM.SystemCheck = StepState.Completed;
            //    }else
            //    {
            //        _sequenceServiceVM.SystemCheck = StepState.Failed;
            //        return;
            //    }

            //    // 전체 서보온 
            //    _sequenceServiceVM.ServoOn = StepState.InProgress;
            //    var servoOnResult = await this._sequenceService.Init_ServoAllOn(_cancellationTokenSource.Token);
            //    if (servoOnResult)
            //    {
            //        _sequenceServiceVM.ServoOn = StepState.Completed;
            //    }
            //    else
            //    {
            //        _sequenceServiceVM.ServoOn = StepState.Failed;
            //        return;
            //    }

            //    // H-Z Break Off
            //    _sequenceServiceVM.HZBreakOff = StepState.InProgress;
            //    var breakOnOffResult =  await _sequenceService.SensorOnOff(IoExtensions.DO_ZIMM_SOL_ON, _cancellationTokenSource.Token);
            //    if (breakOnOffResult)
            //    {
            //        _sequenceServiceVM.HZBreakOff = StepState.Completed;
            //    }
            //    else
            //    {
            //        _sequenceServiceVM.HZBreakOff = StepState.Failed;
            //        return;
            //    }

            //    // All Home 
            //    string[] axes = { "H_Z", "h_z", "H_X", "H_T", "D_Y", "W_Y", "W_T", "P_Y" };

            //   _sequenceHelper.



            //}
            //finally
            //{
            //    IsInitializing = false;
            //}
        }

        [RelayCommand]
        public void MachineRun()
        {
            Task.Run(async () => { 
                IsRunning = true;
                await this._sequenceService.MachineStartAsync(_cancellationTokenSource.Token); 
                IsRunning = false;
            });
        }

        [RelayCommand]
        public void MachineStop()
        {
            IsStopping = true;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new();
            IsStopping = false;
        }

        [RelayCommand]
        public void MachineReset()
        {
            Task.Run(async () => await AlarmService.ResetAllAlarms());
        }
    }
}
