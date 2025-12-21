using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.IoC;

namespace HCB.UI
{


    [ViewModel(Lifetime.Singleton)]
    public partial class SequenceServiceVM : ObservableObject
    {
        [ObservableProperty]
        private bool isMachineInitialized;

        [ObservableProperty]
        private RunStop runStop;

        [ObservableProperty]
        private AlarmLevel alarmLevel;

        [ObservableProperty]
        private Availability availability;

        [ObservableProperty]
        private OperationMode operationMode;

        public SequenceServiceVM(SequenceService sequenceService)
        {
            IsMachineInitialized = false;
            RunStop = RunStop.Stop;
            AlarmLevel = AlarmLevel.Normal;
            Availability = Availability.Up;
            OperationMode = OperationMode.Manual;

            sequenceService.StatusChanged += (s, e) =>
            {
                RunStop = e.RunStop;
                AlarmLevel = e.AlarmLevel;
                Availability = e.Availability;
                OperationMode = e.Mode;
            };
        }
    }
}
