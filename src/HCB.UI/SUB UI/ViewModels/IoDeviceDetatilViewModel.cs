using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Repository;
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
        private readonly DialogService dialogService;
        [ObservableProperty] private IIoDevice device;

        [ObservableProperty] private IIoData selectedIoData;

        public IoDeviceDetatilViewModel(IIoDevice device, DialogService dialogService, IoDataRepository ioDataRepository)
        {
            this.ioDataRepository = ioDataRepository;
            this.dialogService = dialogService;
            Device = device;
            SelectedIoData = Device.IoDataList.FirstOrDefault();
        }

        [RelayCommand]
        public async Task IoDataCreate()
        {
            if (Device == null || Device.Id == 0)
            {
                dialogService.ShowMessage("에러", "디바이스를 먼저 선택하세요");
                return;
            }

            var vm = new IoDataCreateVM();
            bool? result = await dialogService.ShowEditDialog(vm);
            
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
                    dialogService.ShowMessage("파라미터 생성 실패", e.Message);
                }
            }
            else
            {
                dialogService.ShowMessage("취소", "파라미터 생성 취소");
            }

        }

        [RelayCommand]
        public async Task IoDataUpdate()
        {
            if (SelectedIoData == null || SelectedIoData.Id == 0)
            {
                dialogService.ShowMessage("선택 오류", "수정할 IO 데이터를 선택하세요");
                return;
            }

            var originalEntity = IoDataFactory.ToEntity(SelectedIoData, 0);

            var vm = new IoDataCreateVM
            {
                Name = originalEntity.Name,
                Address = originalEntity.Address,
                Index = originalEntity.Index,
                IoType = originalEntity.IoDataType,
                Description = originalEntity.Description,
                Unit = originalEntity.Unit
            };

            bool? result = await dialogService.ShowEditDialog(vm);

            if (result != true)
                return;

            try
            {
                var updatedEntity = vm.ToEntity();

                updatedEntity.Id = originalEntity.Id;
                updatedEntity.ParentDeviceId = Device.Id;

                await ioDataRepository.Update(updatedEntity);

                SelectedIoData.Name = updatedEntity.Name;
                SelectedIoData.Address = updatedEntity.Address;
                SelectedIoData.Index = updatedEntity.Index;
                SelectedIoData.Description = updatedEntity.Description;
                SelectedIoData.Unit = updatedEntity.Unit;
                SelectedIoData.IoType = updatedEntity.IoDataType;
                SelectedIoData.IsEnabled = updatedEntity.IsEnabled;

                dialogService.ShowMessage("완료", "IO 데이터가 수정되었습니다");
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage("수정 실패", ex.Message);
            }
        }

        [RelayCommand]
        public async Task IoDataDelete()
        {
            if (SelectedIoData == null || SelectedIoData.Id == 0)
            {
                dialogService.ShowMessage("선택 오류", "삭제할 IO 데이터를 선택하세요");
                return;
            }

            var confirm = MessageBox.Show(
                $"'{SelectedIoData.Name}' IO 데이터를 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await ioDataRepository.Remove(SelectedIoData.Id);

                Device.IoDataList.Remove(SelectedIoData);
                SelectedIoData = Device.IoDataList.FirstOrDefault();

                dialogService.ShowMessage("삭제 완료", "IO 데이터가 삭제되었습니다");
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage("삭제 실패", ex.Message);
            }
        }
    }
}
