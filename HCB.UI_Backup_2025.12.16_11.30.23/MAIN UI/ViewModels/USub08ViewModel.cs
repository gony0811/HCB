using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using SharpDX.Direct3D9;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView;
using DeviceType = HCB.Data.Entity.Type.DeviceType;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub08ViewModel : ObservableObject
    {
        private readonly DialogService _dialogService;

        private ILogger logger;
        private readonly DeviceManager deviceManager;
        private readonly DeviceDetailViewModelFactory deviceDetailViewModelFactory;
        [ObservableProperty] public ObservableCollection<IDevice> deviceList;

        [ObservableProperty] private IDevice selectedDevice;
        [ObservableProperty] private IDeviceDetailViewModel selectedDetailViewModel;

        public USub08ViewModel(ILogger logger, DeviceManager deviceManager, DeviceDetailViewModelFactory deviceDetailViewModelFactory, DialogService dialogService)
        {
            this._dialogService = dialogService;
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
            bool? result = await _dialogService.ShowDetailEditModal(vm);

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
        public async Task DeviceUpdate()
        {
            if (SelectedDevice == null)
            {
                _dialogService.ShowMessage("경고", "디바이스를 선택해주세요");
                return;
            }

            switch (SelectedDevice.DeviceType)
            {
                case DeviceType.MotionController:

                    if (SelectedDevice is not IMotionDevice motion)
                    {
                        _dialogService.ShowMessage("에러", "선택된 디바이스 형식이 MotionDevice가 아닙니다.");
                        return;
                    }

                    var vm = new MotionControllerUpdateDto
                    {
                        Id = motion.Id,
                        Name = motion.Name,
                        ConfigFilePath = motion.FileName,
                        Description = motion.Description,
                        Ip = motion.Ip,
                        Port = motion.Port,
                        MotionDeviceType = motion.MotionDeviceType,
                    };

                    bool? result = await _dialogService.ShowEditDialog(vm);

                    if (result == true)
                    {
                        try
                        {
                            await deviceManager.UpdateDevice(vm.ToMotionDevice());
                            _dialogService.ShowMessage("완료", "디바이스가 업데이트되었습니다.");
                        }
                        catch (Exception ex)
                        {
                            _dialogService.ShowMessage("에러", "디바이스 업데이트 중 오류가 발생했습니다.");
                        }
                    }
                    break;

                case DeviceType.IODevice:
                    if (SelectedDevice is not IIoDevice ioDevice)
                    {
                        _dialogService.ShowMessage("에러", "선택된 디바이스 형식이 IO디바이스가 아닙니다.");
                        return;
                    }

                    var ioVM = new IODeviceUpdateDto().of(ioDevice);
                    bool? ioResult = await _dialogService.ShowEditDialog(ioVM);
                    if (ioResult == true)
                    {
                        try
                        {
                            await deviceManager.UpdateDevice(ioVM.ToIoDevice());
                            _dialogService.ShowMessage("완료", "디바이스가 업데이트되었습니다.");
                        }
                        catch (Exception ex)
                        {
                            _dialogService.ShowMessage("에러", "디바이스 업데이트 중 오류가 발생했습니다.");
                        }
                    }
                    break;
            }
        }

        [RelayCommand]
        public async Task DeviceDelete()
        {
            if (SelectedDevice == null) return;

            bool ask = _dialogService.ShowConfirm("디바이스 삭제", $"{SelectedDevice.Name}를 삭제하시겠습니까?");

            if (!ask) return;


            // 디바이스 삭제 ( 메모리, 디비 ) 
            bool result = false;
            try
            {
                result = await deviceManager.RemoveDevice(SelectedDevice.Id);
                _dialogService.ShowMessage("삭제 완료", "삭제가 완료되었습니다");
            }catch(Exception ex)
            {
                _dialogService.ShowMessage("삭제 에러", "삭제중 에러가 발생했습니다.");
            }

            
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
