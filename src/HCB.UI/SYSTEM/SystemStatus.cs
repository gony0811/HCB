using HCB.Data.Entity.Type;
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
        public static Availability Availability { get; set; } = Availability.Up;
        public static AlarmLevel Alarm { get; set; } = AlarmLevel.NO_ALARM;
        public static RunStop Run { get; set; } = RunStop.Ready;
        public static OperationMode Operation { get; set; } = OperationMode.Manual;
    }
}
