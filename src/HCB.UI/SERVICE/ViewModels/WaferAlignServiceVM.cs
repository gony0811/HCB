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
        private bool isWaferAlign;

        [ObservableProperty]
        private StepState waferCenterMeasure;

        [ObservableProperty]
        private StepState waferLeftMeasure;

        [ObservableProperty]
        private StepState waferRightMeasure;

        [ObservableProperty]
        private StepState waferAlign;

    }
}
