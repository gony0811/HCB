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
        private USub02ViewModel recipeViewModel;
        #endregion

        [ObservableProperty] public UserControl currentTab;
        [ObservableProperty] public string currentDevice = "UNKNOWN DEVICE";
        [ObservableProperty] private string selectedTabKey = "LOADING";



        public USub01ViewModel(LoadingTab loadingTab, AutoTab autoTab, ManualTab manualTab, USub02ViewModel sub02ViewModel)
        {
            this.loadingTab = loadingTab;
            this.autoTab = autoTab;
            this.manualTab = manualTab;
            this.recipeViewModel = sub02ViewModel;
            SetTab(selectedTabKey);

            //CurrentDevice = this.recipeViewModel.SelectedRecipe.Name;
        }

        [RelayCommand]
        public void SetTab(string viewName)
        {
            SelectedTabKey = viewName;
            switch (viewName)
            {
                case "LOADING": 
                    CurrentTab = loadingTab; break;
                case "AUTO": CurrentTab = autoTab; break;
                case "MANUAL": CurrentTab = manualTab; break;
                //case "STEP": CurrentTab = App.Container.Resolve<StepTab>(); break;
                //case "VISION": CurrentTab = App.Container.Resolve<LoadingTab>(); break;
                //case "CALIBRATION": CurrentTab = App.Container.Resolve<LoadingTab>(); break;
            }
        }

    }
}
