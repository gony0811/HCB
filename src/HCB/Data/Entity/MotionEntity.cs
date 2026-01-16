using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HCB.Data.Entity.Type;
using HCB.Data.Interface;

namespace HCB.Data.Entity
{
    public class MotionEntity : IEntity
    {

        [Required]
        public string Name { get; set; } = "";

        public int MotorNo { get; set; }

        //public string ControlType { get; set; }

        public UnitType Unit { get; set; }

        public bool IsEnabled { get; set; } = true;

        public double MinimumSpeed { get; set; } = 1;
        public double MaximumSpeed { get; set; } = 100;
        public double MinimumLocation { get; set; } = 0.0;
        public double MaximumLocation { get; set; } = 0.0;
        public double EncoderCountsPerUnit { get; set; } = 1.0;
        public int HommingProgramNumber { get; set; } = 0;

        public int ParentDeviceId { get; set; }  
        public MotionDeviceDetail? ParentDeviceEntity { get; set; }

        public ICollection<MotionPosition> PositionList { get; set; } = new List<MotionPosition>();
        public ICollection<MotionParameter> ParameterList { get; set; } = new List<MotionParameter>();
    }
}
