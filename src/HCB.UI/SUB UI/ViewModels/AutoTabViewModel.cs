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

        public AutoTabViewModel(RunInformation runInformation, RunningStatus runningStatus, SequenceService sequenceService, AlarmService alarmService)
        {
            RunInformation = runInformation;
            RunningStatus = runningStatus;
            _sequenceService = sequenceService;
            _cancellationTokenSource.TryReset();
            AlarmService = alarmService;
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
        public void MachineInit()
        {
            Task.Run(async () => { 
                IsInitializing = true;
                await this._sequenceService.MachineInitAsync(_cancellationTokenSource.Token); 
                IsInitializing = false;
            });
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
