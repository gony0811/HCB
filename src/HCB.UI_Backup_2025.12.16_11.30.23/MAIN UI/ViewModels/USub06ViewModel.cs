using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub06ViewModel : ObservableObject
    {
        private readonly DialogService dialogService;
        private readonly DeviceManager deviceManager;
        private readonly MotionPositionRepository positionRepository;
        [ObservableProperty] private ObservableCollection<IAxis> motionList = new ObservableCollection<IAxis>();
        [ObservableProperty] private IAxis selectedMotion;
        [ObservableProperty] private DMotionPosition selectedPosition;

        public USub06ViewModel(DeviceManager deviceManager, DialogService dialogService, MotionPositionRepository positionRepository)
        {
            this.positionRepository = positionRepository;
            this.deviceManager = deviceManager;
            this.dialogService = dialogService;
            _ = Initialize();
        }

        public async Task Initialize()
        {
            MotionList.Clear();
            var motionDeviceList = deviceManager.Devices
                .OfType<IMotionDevice>()
                .Where(x => x.DeviceType == DeviceType.MotionController)
                .ToList();
            foreach (var md in motionDeviceList)
            {
                foreach(var m in md.MotionList)
                {
                    MotionList.Add(m);
                }
            }
        }

        

        #region Position CRUD
        [RelayCommand]
        public async void PositionCreate()
        {
            if (SelectedMotion == null || SelectedMotion.Id == 0)
            {
                dialogService.ShowMessage("에러", "모션을 선택해주세요");
                return;
            }

            var newPosition = new MotionPositionCreate();
            bool? result = await dialogService.ShowEditDialog(newPosition);

            if (result == true)
            {
                var createPosition = new DMotionPosition
                {
                    Name = newPosition.Name,
                    Speed = newPosition.Speed,
                    Position = newPosition.Location,
                    ParentMotion = SelectedMotion
                };

                try
                {
                    var entity = new MotionPosition
                    {
                        Name = newPosition.Name,
                        Speed = newPosition.Speed,
                        Position = newPosition.Location,
                        MotionId = SelectedMotion.Id
                    };
                    entity = await positionRepository.AddAsync(entity);
                    createPosition.Id = entity.Id;
                    SelectedMotion.PositionList.Add(createPosition);
                    dialogService.ShowMessage("저장", "저장 되었습니다");

                } catch (Exception e)
                {
                    dialogService.ShowMessage("에러", "저장 실패");
                }
            }
        }

        [RelayCommand]
        public async Task PositionUpdate(DMotionPosition pos)
        {
            if (pos == null) return;


        }

        [RelayCommand]
        public async Task PositionDelete(DMotionPosition pos)
        {
            if (pos == null) return;

            var confirm = MessageBox.Show(
                $"'{pos.Name}' 삭제하시겠습니까?",
                "삭제 확인",
                MessageBoxButton.YesNo);

            if (confirm != MessageBoxResult.Yes)
                return;

            SelectedMotion.PositionList.Remove(pos);
        }
        #endregion
    }
}
