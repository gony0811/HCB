using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Spreadsheet.Model.ConditionalFormattings;

namespace HCB.UI
{
    public partial class IODeviceUpdateDto : ObservableObject
    {
        [Browsable(false)]
        public int Id { get; set; }

        [ObservableProperty] private string name;
        [ObservableProperty] private string configFilePath;
        [ObservableProperty] private string description;
        [ObservableProperty] private IoDeviceType ioDeviceType;
        [ObservableProperty] private string ip;
        [ObservableProperty] private int port;

        public IODeviceUpdateDto of(IIoDevice device)
        {
            return new IODeviceUpdateDto()
            {
                Id = device.Id,
                Name = device.Name,
                ConfigFilePath = device.FileName,
                Description = device.Description,
                IoDeviceType = device.IoDeviceType,
                Ip = device.Ip,
                Port = device.Port
            };
        }

        public IIoDevice ToIoDevice()
        {
            switch (IoDeviceType)
            {
                case IoDeviceType.PowerPmac:
                    return new PmacIoDevice()
                    {
                        Id = this.Id,
                        Name = this.Name,
                        FileName = this.ConfigFilePath,
                        Description = this.Description,
                        IoDeviceType = this.IoDeviceType,
                        Ip = this.Ip,
                        Port = this.Port
                    };
            }

            return new PmacIoDevice()
            {
                Id = this.Id,
                Name = this.Name,
                FileName = this.ConfigFilePath,
                Description = this.Description,
                IoDeviceType = this.IoDeviceType,
                Ip = this.Ip,
                Port = this.Port
            };
        }
    }
}
