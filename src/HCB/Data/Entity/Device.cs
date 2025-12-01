using HCB.Data.Entity.Type;
using HCB.Data.Interface;
using System.ComponentModel.DataAnnotations;

namespace HCB.Data.Entity
{

    public class Device : IEntity
    {

        [Required]
        public string Name { get; set; } = "";

        public DeviceType DeviceType { get; set; }

        public string? FileName { get; set; }

        public string? InstanceName { get; set; } 

        public bool IsEnabled { get; set; } = true;

        public string? Description { get; set; }

        public MotionDeviceDetail? MotionDeviceDetail { get; set; }

        public IoDeviceDetail? IoDeviceDetail { get; set; }
    }
}
