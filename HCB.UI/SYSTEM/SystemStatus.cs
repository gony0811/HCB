using HCB.Data.Entity.Type;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public enum Availability
    {
        Down,
        Up
    }

    public enum RunStop
    {
        Ready,
        Run,
        Stop,
        Pause
    }

    public enum OperationMode
    {
        Manual,
        Auto
    }

    public enum AlarmState
    {
        NO_ALARM,
        LIGHT,
        HEAVY
    }

    public class EQStatus
    {
        public Availability Availability { get; set; } = Availability.Up;
        public AlarmState Alarm { get; set; } = AlarmState.NO_ALARM;
        public RunStop Run { get; set; } = RunStop.Ready;
        public OperationMode Operation { get; set; } = OperationMode.Manual;
    }
}
