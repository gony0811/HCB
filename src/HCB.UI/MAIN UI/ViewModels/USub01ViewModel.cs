using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub01ViewModel : ObservableObject
    {
        [ObservableProperty] public UserControl currentTab;
        [ObservableProperty] public string currentDevice = "UNKNOWN DEVICE";
        [ObservableProperty] private string selectedTabKey = "LOADING";

        public USub01ViewModel()
        {
            SetTab(selectedTabKey);
            
        }

        [RelayCommand]
        public void SetTab(string viewName)
        {
            SelectedTabKey = viewName;
            switch (viewName)
            {
                case "LOADING": CurrentTab = App.Container.Resolve<LoadingTab>(); break;
                case "AUTO": CurrentTab = App.Container.Resolve<AutoTab>(); break;
                case "MANUAL": CurrentTab = App.Container.Resolve<ManualTab>(); break;
                //case "STEP": CurrentTab = App.Container.Resolve<StepTab>(); break;
                //case "VISION": CurrentTab = App.Container.Resolve<LoadingTab>(); break;
                //case "CALIBRATION": CurrentTab = App.Container.Resolve<LoadingTab>(); break;
            }
        }

    }
}
