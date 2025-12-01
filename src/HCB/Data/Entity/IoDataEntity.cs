using HCB.Data.Entity.Type;
using HCB.Data.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.Data.Entity
{
    public class IoDataEntity : IEntity
    {
        [Required]
        public string Name { get; set; } = "";

        public string Address { get; set; } = "";
        public int Index { get; set; } = 0;

        public IoType IoDataType { get; set; }

        public string Unit { get; set; } = "";

        public string Description { get; set; } = "";

        public bool IsEnabled { get; set; } = true;

        public int ParentDeviceId { get; set; }
        public IoDeviceDetail? ParentDeviceEntity { get; set; }
    }}
