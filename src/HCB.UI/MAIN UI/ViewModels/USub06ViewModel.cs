using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore.Migrations;
using SharpDX;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Documents.Fixed.Model.Data;
using Telerik.Windows.Documents.Spreadsheet.Expressions.Functions;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub06ViewModel : ObservableObject
    {
        private readonly DeviceManager deviceManager;
        private readonly DialogService dialogService;
        private readonly MotionPositionRepository positionRepository;
        [ObservableProperty] private ObservableCollection<IAxis> motionList = new();
        [ObservableProperty] private IAxis selectedMotion;
        [ObservableProperty] private DMotionPosition selectedPosition;
        [ObservableProperty] private MotionMoveVM motionMoveVM = new MotionMoveVM();

        [ObservableProperty] MotorStateControlVM motionStatus = new MotorStateControlVM();
        private IDisposable statusSubscription;

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

            SelectedMotion = MotionList.FirstOrDefault();
            MotionMoveVM.Axis = SelectedMotion;

        }

        public void Dispose()
        {
            statusSubscription?.Dispose();
        }
        #region Position CRUD
        [RelayCommand]
        public async Task PositionCreate()
        {
            try
            {
                if (SelectedMotion == null || SelectedMotion.Id == 0) dialogService.ShowMessage("선택", "모션을 선택해주세요");
                
                PositionCreateVM position = new PositionCreateVM();
                var result = await dialogService.ShowEditDialog(position);
                if (result == false) return;

                var entity = position.ToEntity(SelectedMotion.Id);
                entity = await positionRepository.AddAsync(entity);
                DMotionPosition dto = new DMotionPosition
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Position = entity.Position,
                    Speed = entity.Speed,
                    ParentMotion = SelectedMotion
                };
                SelectedMotion.PositionList.Add(dto);

                dialogService.ShowMessage("저장", "저장되었습니다");
            }
            catch(Exception e)
            {
                dialogService.ShowMessage("에러", $"{e.Message}");
            }
        }

        [RelayCommand]
        public async Task PositionUpdate(DMotionPosition position)
        {
            if (position == null) return;

            try
            {
                var vm = new PositionCreateVM
                {
                    Name = position.Name,
                    Position = position.Position,
                    Speed = position.Speed,
                };

                var result = await dialogService.ShowEditDialog(vm);
                if (result == false || result == null) return;

                position.Name = vm.Name;
                position.Speed = vm.Speed;
                position.Position = vm.Position;

                await positionRepository.Update(position.ToEntity());
            }catch(Exception e)
            {
                dialogService.ShowMessage("에러", $"{e.Message}");
            }
        }

        [RelayCommand]
        public async Task PositionDelete(DMotionPosition position)
        {
            if (position == null) return;
            try
            {
                var result = dialogService.ShowConfirm("삭제",$"{position.Name}을 삭제하시겠습니까?");
                if (result == true)
                {
                    await positionRepository.Remove(position.Id);
                    SelectedMotion.PositionList.Remove(position);
                    dialogService.ShowMessage("삭제", "삭제되었습니다");
                }


            }catch(Exception e)
            {
                dialogService.ShowMessage("에러", $"{e.Message}");
            }
        }
        #endregion
    }
}
