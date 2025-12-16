using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.IoC;
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
        [ObservableProperty]
        private ObservableCollection<IAxis> motionList = new();
        [ObservableProperty] private IAxis selectedMotion;

        public USub06ViewModel(DeviceManager deviceManager, DialogService dialogService)
        {
            this.deviceManager = deviceManager;
            this.dialogService = dialogService;

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
            if(SelectedMotion != null) dialogService.ShowMessage("경고", "모션을 선택해주세요");

            var name = new NameViewModel();
            bool? step1 = await dialogService.ShowEditDialog(name);

            if (step1 == false) return;

            double? result = dialogService.ShowEditNumDialog(0, SelectedMotion.LimitMinPosition, SelectedMotion.LimitMaxPosition);

            if (result == null) return;

            new MotionPosition
            {
                Name = name.Name,
                Position = result.Value,
                Speed = 
            };

            SelectedMotion.PositionList.Add();
        }
        #endregion
    }
}
