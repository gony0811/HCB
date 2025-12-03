using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using PropertyTools.DataAnnotations;

namespace HCB.UI
{
    public partial class DeviceCreateVM : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private DeviceType deviceType;

        [ObservableProperty]
        [property: InputFilePath]
        private string configFilePath;

        [ObservableProperty]
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
