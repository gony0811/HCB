using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB;
using HCB.IoC;
using HCB.Data.Entity.Type;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telerik.Windows.Controls.GridView;
using Telerik.Windows.Controls;
using Serilog;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub08ViewModel : ObservableObject
    {
        private ILogger logger;
        private readonly DeviceManager deviceManager;
        private readonly DeviceDetailViewModelFactory deviceDetailViewModelFactory;
        [ObservableProperty] public ObservableCollection<IDevice> deviceList;

        [ObservableProperty] private IDevice selectedDevice;
        [ObservableProperty] private IDeviceDetailViewModel selectedDetailViewModel;

        public USub08ViewModel(ILogger logger, DeviceManager deviceManager, DeviceDetailViewModelFactory deviceDetailViewModelFactory)
        {
            this.logger = logger.ForContext<USub08ViewModel>();
            this.deviceManager = deviceManager;
            this.deviceDetailViewModelFactory = deviceDetailViewModelFactory;
            DeviceList = deviceManager.Devices;
            SelectedDevice = DeviceList.FirstOrDefault();
        }

        partial void OnSelectedDeviceChanged(IDevice value)
        {
            SelectedDetailViewModel = deviceDetailViewModelFactory.Create(value);
        }

        [RelayCommand]
        public async Task DeviceCreate()
        {
            var vm = new DeviceCreateVM();
            var modal = new CreateModal
            {              
                Header = "Device Create",
                DataContext = vm,
                Width = 400,
                Height = 800,
            };

            modal.Owner = App.Current.MainWindow;
            bool? result = modal.ShowDialog();

            if (result == true)
            {
                // 상세 설정 매핑
                switch (vm.DeviceType)
                {
                    case DeviceType.MotionController:
                        var detail = vm.ExtraSetting as MotionDeviceDetailCreateVM;
                        var motionDevice = new PowerPmacDevice(logger)
                        {
                            Name = vm.Name,
                            DeviceType = vm.DeviceType,
                            FileName = Path.GetFileName(vm.ConfigFilePath),
                            InstanceName = vm.ConfigFilePath,
                            Description = vm.Description,
                            IsConnected = false,
                            IsEnabled = true,
                            Ip = detail?.Ip,
                            Port = detail.Port,
                            MotionDeviceType = detail?.MotionDeviceType ?? MotionDeviceType.PowerPmac
                        };
                        await deviceManager.RegisterDevice(motionDevice);
                        break;
                    case DeviceType.IODevice:
                        var ioDetail = vm.ExtraSetting as IoDeviceDetailCreateVM;
                        var ioDevice = new PmacIoDevice(logger)
                        {
                            Name = vm.Name,
                            DeviceType = vm.DeviceType,
                            FileName = Path.GetFileName(vm.ConfigFilePath),
                            InstanceName = vm.ConfigFilePath,
                            Description = vm.Description,
                            IsConnected = false,
                            IsEnabled = true,
                            Ip = ioDetail?.Ip,
                            Port = ioDetail.Port,
                            IoDeviceType = ioDetail?.IoDeviceType ?? IoDeviceType.PowerPmac
                        };
                        await deviceManager.RegisterDevice(ioDevice);
                        break;

                    default:
                        break;
                }

            }
        }

        [RelayCommand]
        public void MotionCreate()
        {

        }

        [RelayCommand]
        public async Task MotionSave()
        {

        }

        [RelayCommand]
        public void ConnectPowerPMac()
        {
            //bool result = SelectedPMac.Connect();
            //if (!result)
            //{
            //    MessageBox.Show($"연결 실패");
            //}else
            //{
            //    MessageBox.Show("연결 성공");
            //}
        }
    }
}
