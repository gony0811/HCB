using HCB.Data.Entity.Type;
using System;

namespace HCB.UI
{
    public interface IOperationService
    {
        EQStatus Status { get; }

        event Action<EQStatus> EQStatusChanged;

        void SetAvailability(Availability availability);
        void SetAlarm(AlarmState state);
        void SetRun(RunStop run);
        void SetOperationMode(OperationMode operation);
    }
}
