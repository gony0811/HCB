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
        #region
        private LoadingTab loadingTab;
        private AutoTab autoTab;
        private ManualTab manualTab;
        #endregion

        [ObservableProperty] public UserControl currentTab;
        [ObservableProperty] public string currentDevice = "UNKNOWN DEVICE";
        [ObservableProperty] private string selectedTabKey = "LOADING";



        public USub01ViewModel(LoadingTab loadingTab, AutoTab autoTab, ManualTab manualTab)
        {
            this.loadingTab = loadingTab;
            this.autoTab = autoTab;
            this.manualTab = manualTab;

            SetTab(selectedTabKey);
            
        }

        [RelayCommand]
        public void SetTab(string viewName)
        {
            SelectedTabKey = viewName;
            switch (viewName)
            {
                case "LOADING": CurrentTab = loadingTab; break;
                case "AUTO": CurrentTab = autoTab; break;
                case "MANUAL": CurrentTab = manualTab; break;
                //case "STEP": CurrentTab = App.Container.Resolve<StepTab>(); break;
                //case "VISION": CurrentTab = App.Container.Resolve<LoadingTab>(); break;
                //case "CALIBRATION": CurrentTab = App.Container.Resolve<LoadingTab>(); break;
            }
        }

    }
}
