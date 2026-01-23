using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.Data.Entity
{
    public class IoDeviceDetail
    {
        [Key]
        public int DeviceId { get; set; }  // ✅ Device.Id와 1:1 매핑 (FK)

        [MaxLength(50)]
        public string? Ip { get; set; } = "";

        public int Port { get; set; }
        public IoDeviceType IoDeviceType { get; set; }
        public Device? Device { get; set; }

        // ✅ Motion 관계
        public ICollection<IoDataEntity> IoDataList { get; set; } = new List<IoDataEntity>();
    }
}
