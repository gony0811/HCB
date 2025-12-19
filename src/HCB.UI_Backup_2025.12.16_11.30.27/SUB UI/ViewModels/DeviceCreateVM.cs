using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using PropertyTools.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Telerik.Windows.Controls.DataVisualization.Map.BingRest;

namespace HCB.UI
{
    public partial class DeviceCreateVM : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty]
        [Display(Order = 1)]
        private string name;

        [ObservableProperty]
        [Display(Order = 2)]
        private DeviceType deviceType;

        [ObservableProperty]
        [Display(Order = 3)]
        private string configFilePath;

        [ObservableProperty]
        [Display(Order = 4)]
        private string description;

        [ObservableProperty]
        private DeviceDetailBase extraSetting;

        public DeviceCreateVM()
        {
            DeviceType = DeviceType.MotionController;
            ExtraSetting = new MotionDeviceDetailCreateVM();
        }
        partial void OnDeviceTypeChanged(DeviceType value)
        {
            switch (value)
            {
                case DeviceType.MotionController:
                    ExtraSetting = new MotionDeviceDetailCreateVM();
                    break;
                case DeviceType.IODevice:
                    ExtraSetting = new IoDeviceDetailCreateVM();
                    break;
                default:
                    ExtraSetting = null;
                    break;
            }
        }
        public DeviceCreateVM ToCreateVM(IDevice device)
        {

            switch(device.DeviceType)
            {
                case DeviceType.MotionController:

                    IMotionDevice motionDevice = device as IMotionDevice;

                    var detail = new MotionDeviceDetailCreateVM
                    {
                        MotionDeviceType = motionDevice.MotionDeviceType,
                        Ip = motionDevice.Ip,
                        Port = motionDevice.Port
                    };
                    return new DeviceCreateVM
                    {
                        Id = motionDevice.Id,
                        Name = motionDevice.Name,
                        ConfigFilePath = motionDevice.FileName,
                        DeviceType = motionDevice.DeviceType,
                        Description = motionDevice.Description,
                        ExtraSetting = detail
                    };
                case DeviceType.IODevice:
                    IIoDevice ioDevice = device as IIoDevice;

                    var ioDetail = new IoDeviceDetailCreateVM
                    {
                        IoDeviceType = ioDevice.IoDeviceType,
                        Ip = ioDevice.Ip,
                        Port = ioDevice.Port
                    };

                    return new DeviceCreateVM
                    {
                        Id = ioDevice.Id,
                        Name = ioDevice.Name,
                        ConfigFilePath = ioDevice.FileName,
                        DeviceType = ioDevice.DeviceType,
                        Description = ioDevice.Description,
                        ExtraSetting = ioDetail
                    };

            }

            return new DeviceCreateVM();
        }

        public IDevice ToDto()
        {
            switch(DeviceType)
            {
                case DeviceType.MotionController:
                    var detail = ExtraSetting as MotionDeviceDetailCreateVM;
                    var motionDevice = new PowerPmacDevice
                    {
                        Id = this.Id,
                        Name = this.Name,
                        DeviceType = this.DeviceType,
                        FileName = Path.GetFileName(this.ConfigFilePath),
                        InstanceName = this.ConfigFilePath,
                        Description = this.Description,
                        IsConnected = false,
                        IsEnabled = true,
                        Ip = detail.Ip,
                        Port = detail.Port,
                        MotionDeviceType = detail?.MotionDeviceType ?? MotionDeviceType.PowerPmac
                    };
                    return motionDevice;

                case DeviceType.IODevice:
                    var ioDetail = ExtraSetting as IoDeviceDetailCreateVM;
                    var ioDevice = new PmacIoDevice
                    {
                        Id = this.Id,
                        Name = this.Name,
                        DeviceType = this.DeviceType,
                        FileName = Path.GetFileName(this.ConfigFilePath),
                        InstanceName = this.ConfigFilePath,
                        Description = this.Description,
                        IsConnected = false,
                        IsEnabled = true,
                        Ip = ioDetail.Ip,
                        Port = ioDetail.Port,
                        IoDeviceType = ioDetail?.IoDeviceType ?? IoDeviceType.PowerPmac
                    };
                    return ioDevice;
            }

            return null;
        }
    }
    public abstract class DeviceDetailBase : ObservableObject
    {
    }

    public partial class MotionDeviceDetailCreateVM : DeviceDetailBase
    {
        [ObservableProperty]
        private string ip;

        [ObservableProperty]
        private int port;

        [ObservableProperty]
        private MotionDeviceType motionDeviceType;
    }

    public partial class IoDeviceDetailCreateVM : DeviceDetailBase
    {
        [ObservableProperty]
        private string ip;
        [ObservableProperty]
        private int port;
        [ObservableProperty]
        private IoDeviceType ioDeviceType;
    }

}
