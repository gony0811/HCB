using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HCB.Data.Entity.Type;

namespace HCB.Data.Entity
{
    public class MotionDeviceDetail
    {
        [Key]
        public int DeviceId { get; set; }  // ✅ Device.Id와 1:1 매핑 (FK)

        [MaxLength(50)]
        public string? Ip { get; set; } = "";

        public int Port { get; set; }
        public MotionDeviceType MotionDeviceType { get; set; }
        public Device? Device { get; set; }

        // ✅ Motion 관계
        public ICollection<MotionEntity> MotionList { get; set; } = new List<MotionEntity>();
    }
}
