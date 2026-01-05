using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;

namespace HCB.UI
{
    [ViewModel(Lifetime.Transient)]
    public partial class MotorStatusTableViewModel : ObservableObject
    {
        public string TableName { get; set; }

        [ObservableProperty]
        private State homeState = State.Normal;

        [ObservableProperty] 
        private State rdyState;

        [ObservableProperty]
        private State almState;

        [ObservableProperty]
        private State inpoState;

        [ObservableProperty]
        private State nlmtState;

        [ObservableProperty]
        private State orgState;

        [ObservableProperty]
        private State plmtState;

        [ObservableProperty]
        private string encState;

        [ObservableProperty]
        private string posState;

        public MotorStatusTableViewModel(string tableName) 
        {
            TableName = tableName;
        }

        [RelayCommand]
        public void ServoOn()
        {

        }

        [RelayCommand]
        public void HomeStart()
        {

        }

    }
}
