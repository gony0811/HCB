using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace HCB.UI
{
    public partial class MotorStateControlVM : ObservableObject
    {
        [ObservableProperty] private string title = "";

        [ObservableProperty] ObservableCollection<StateCellVM> motorStates = new ObservableCollection<StateCellVM>();
        
        [ObservableProperty] string ep = "0.0";

        [ObservableProperty] string cp = "0.0";

        public MotorStateControlVM() { }

        public MotorStateControlVM(ObservableCollection<StateCellVM> motorStates, string ep, string cp)
        {
            MotorStates = motorStates;
            Ep = ep;
            Cp = cp;
        }

        /** 
         * Command 명령 
        **/
        [RelayCommand]
        private void Servo()
        {
            // TODO: 서보 동작
        }

        [RelayCommand]
        private void Home()
        {
            // TODO: HOME 동작 
        }



    }
}
