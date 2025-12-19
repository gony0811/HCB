using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using SharpDX;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub06ViewModel : ObservableObject
    {
        private readonly DeviceManager deviceManager;
        private readonly DialogService dialogService;
        private readonly MotionPositionRepository positionRepository;

        [ObservableProperty]
        private ObservableCollection<IAxis> motionList = new();
        [ObservableProperty] private IAxis selectedMotion;

        public USub06ViewModel(DeviceManager deviceManager, DialogService dialogService, MotionPositionRepository positionRepository)
        {
            this.deviceManager = deviceManager;
            this.dialogService = dialogService;
            this.positionRepository = positionRepository;

            var motionDevices = deviceManager.Devices
                .OfType<IMotionDevice>()
                .Where(d => d.DeviceType == DeviceType.MotionController);

            foreach (var device in motionDevices)
            {
                foreach (var axis in device.MotionList)
                {
                    MotionList.Add(axis); 
                }
            }
        }

        #region Position CRUD
        [RelayCommand]
        public async Task PositionCreate()
        {
            if(SelectedMotion == null) dialogService.ShowMessage("경고", "모션을 선택해주세요");

            var position = new DMotionPosition();

            bool? result = await dialogService.ShowEditDialog(position);
            if (result == false) return;

            
            try
            {
                var entity = new MotionPosition()
                {
                    MotionId = SelectedMotion.Id,
                    Name = position.Name,
                    Position = position.Position,
                    Speed = position.Speed
                };

                entity = await positionRepository.AddAsync(entity);
                position.Id = entity.Id;
                SelectedMotion.PositionList.Add(position);
            }
            catch(Exception e)
            {
                dialogService.ShowMessage("오류", $"저장중 에러 발생:\n {e}");
            }
            


            //bool? step1 = await dialogService.ShowEditDialog(name);

            //if (step1 == false) return;

            //double? result = dialogService.ShowEditNumDialog(0, SelectedMotion.LimitMinPosition, SelectedMotion.LimitMaxPosition);
            //double? result = dialogService.ShowEditNumDialog(0, SelectedMotion.LimitMinPosition, SelectedMotion.LimitMaxSpeed);
            //if (result == null) return;

            //new DMotionPosition
            //{
            //    Name = name.Name,
            //    Position = result.Value,
            //    Speed = 
            //};

            //SelectedMotion.PositionList.Add();
        }
        #endregion
    }
}
