using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class AutoTabViewModel : ObservableObject
    {
        public RunInformation RunInformation { get; }
        public RunningStatus RunningStatus { get; }

        public AutoTabViewModel(RunInformation runInformation, RunningStatus runningStatus)
        {
            RunInformation = runInformation;
            RunningStatus = runningStatus;
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
    }
}
