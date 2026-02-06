using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class SequenceServiceVM : ObservableObject
    {

        [ObservableProperty]
        private int initializeProgress = 0;

        [ObservableProperty]
        private StepState systemCheck = StepState.Idle;

        [ObservableProperty]
        private StepState servoOn = StepState.Idle;

        [ObservableProperty]
        private StepState hZBreakOff= StepState.Idle;

        [ObservableProperty]
        private StepState hZHome= StepState.Idle;

        [ObservableProperty]
        private StepState hzHome= StepState.Idle;

        [ObservableProperty]
        private StepState hXHome= StepState.Idle;

        [ObservableProperty]
        private StepState hTHome= StepState.Idle;

        [ObservableProperty]
        private StepState dYHome= StepState.Idle;

        [ObservableProperty]
        private StepState pYHome= StepState.Idle;

        [ObservableProperty]
        private StepState wYHome= StepState.Idle;

        [ObservableProperty]
        private StepState wTHome= StepState.Idle;
    }
}
