using HCB.Data.Entity.Type;
using System;

namespace HCB.UI
{
    public interface IOperationService
    {
        EQStatus Status { get; }

        event Action<Availability> AvailabilityChanged;
        event Action<AlarmLevel> AlarmChanged;
        event Action<RunStop> RunChanged;
        event Action<OperationMode> OperationModeChanged;

        void SetAvailability(Availability availability);
        void SetAlarm(AlarmLevel alarm);
        void SetRun(RunStop run);
        void SetOperationMode(OperationMode operation);
    }
}
