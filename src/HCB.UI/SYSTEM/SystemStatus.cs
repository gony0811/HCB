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



    public class EQStatus
    {
        public Availability Availability { get; internal set; } = Availability.Up;
        public AlarmLevel Alarm { get; internal set; } = AlarmLevel.Normal;
        public RunStop Run { get; internal set; } = RunStop.Ready;
        public OperationMode Operation { get; internal set; } = OperationMode.Manual;
    }
}
