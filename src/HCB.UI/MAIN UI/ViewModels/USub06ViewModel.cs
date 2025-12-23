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
        public void PositionCreate()
        {
            // ... (생략) ...

            
            try
            {
                //var wizard = new MotionWizardWindow
                //{
                //    Owner = DialogService.GetOwnerWindow()
                //};
                //if (Application.Current.Dispatcher.CheckAccess())
                //{
                //    if (wizard.ShowDialog() == true)
                //    {
                //        return;
                //    }
                //}
                //else
                //{
                //    // UI 스레드에서 ShowDialog()를 호출하도록 처리
                //    Application.Current.Dispatcher.Invoke(() =>
                //    {
                //        if (wizard.ShowDialog() == true)
                //        {
                //            // ... (결과 처리) ...
                //        }
                //    });
                //}
            }catch(Exception e)
            {
               
            }
            
        }
        #endregion
    }
}
