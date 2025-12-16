using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using PropertyTools.DataAnnotations;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BrowsableAttribute = System.ComponentModel.BrowsableAttribute;

namespace HCB.UI
{
    public partial class MotionControllerUpdateDto : ObservableObject
    {
        [Browsable(false)]
        public int Id { get; set; }

        [ObservableProperty] private string name;
        [ObservableProperty] private string configFilePath;
        [ObservableProperty] private string description;
        [ObservableProperty] private MotionDeviceType motionDeviceType;
        [ObservableProperty] private string ip;
        [ObservableProperty] private int port;

        public IMotionDevice ToMotionDevice()
        {
            switch(MotionDeviceType)
            {
                case MotionDeviceType.PowerPmac:
                    return new PowerPmacDevice()
                    {
                        Id = this.Id,
                        Name = this.Name,
                        FileName = this.ConfigFilePath,
                        Description = this.Description,
                        MotionDeviceType = this.MotionDeviceType,
                        Ip = this.Ip,
                        Port = this.Port
                    };
            }

            return new PowerPmacDevice()
            {
                Id = this.Id,
                Name = this.Name,
                FileName = this.ConfigFilePath,
                Description = this.Description,
                MotionDeviceType = this.MotionDeviceType,
                Ip = this.Ip,
                Port = this.Port
            };
        }
    }
}
