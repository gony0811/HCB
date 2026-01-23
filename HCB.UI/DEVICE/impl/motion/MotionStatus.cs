
using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public class MotionStatus 
    {
        public string Name { get; init; }

        public double CurrentPosition { get; init; }
        public double CommandPosition { get; init; }
        public double CurrentSpeed { get; init; }

        public bool IsEnabled { get; init; }
        public bool IsBusy { get; init; }
        public bool IsError { get; init; }
        public bool IsPlusLimit { get; init; }
        public bool IsMinusLimit { get; init; }
        public bool IsHome { get; set; }

        public bool IsHomeDone { get; init; }        
        
        public bool IsMotionDone { get; init; }
        public bool InPosition { get; init; }
    }
}
