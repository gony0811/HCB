using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Repository;
using HCB.UI.DEVICE.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    public partial class IoDeviceDetatilViewModel : ObservableObject, IDeviceDetailViewModel
    {
        private readonly IoDataRepository ioDataRepository;

        [ObservableProperty] private IIoDevice device;

        [ObservableProperty] private IIoData selectedIoData;

        public IoDeviceDetatilViewModel(IIoDevice device, IoDataRepository ioDataRepository)
        {
            this.ioDataRepository = ioDataRepository;

            Device = device;
            SelectedIoData = Device.IoDataList.FirstOrDefault();
        }

        [RelayCommand]
        public async Task IoDataCreate()
        {
            if (Device == null || Device.Id == 0)
            {
                MessageBox.Show("디바이스를 먼저 저장하세요.");
            }

            var vm = new IoDataCreateVM();
            var modal = new CreateModal
            {
                Header = "Device Create",
                DataContext = vm,
                Width = 400,
                Height = 800,
            };

            bool? result = modal.ShowDialog();

            if (result == true)
            {
                var entity = vm.ToEntity();
                entity.ParentDeviceId = Device.Id;
                try
                {
                    var resultEntity = await ioDataRepository.AddAsync(entity);
                    Device.IoDataList.Add(IoDataFactory.ToRuntime(resultEntity, Device));

                }
                catch (Exception e)
                {
                    throw new Exception("모션 생성에 실패했습니다.", e);
                }
            }
            else
            {
                MessageBox.Show("모션 생성이 취소되었습니다.");
            }

        }
    }
}
