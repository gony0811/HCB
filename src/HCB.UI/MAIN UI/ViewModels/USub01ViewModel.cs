using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using Telerik.Windows.Documents.Flow.FormatProviders.Html;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub01ViewModel : ObservableObject
    {
        #region
        private LoadingTab loadingTab;
        private AutoTab autoTab;
        private ManualTab manualTab;
        private StepSeqTab stepSeqTab;
        private WaferSeqTab waferSeqTab;
        private CalibrationTab calibrationTab;
        private VisionTab visionTab;
        private USub02ViewModel recipeViewModel;
        private RecipeService _recipeService;
        #endregion

        [ObservableProperty] public UserControl currentTab;
        [ObservableProperty] public string currentDevice = "UNKNOWN DEVICE";
        [ObservableProperty] private string selectedTabKey = "LOADING";

        public USub01ViewModel(LoadingTab loadingTab, AutoTab autoTab, ManualTab manualTab, StepSeqTab stepSeqTab, WaferSeqTab waferSeqTab, StepSeqTabViewModel stepSeqTabViewModel, CalibrationTab calibrationTab, VisionTab visionTab, USub02ViewModel sub02ViewModel, RecipeService recipeService)
        {
            this.loadingTab = loadingTab;
            this.autoTab = autoTab;
            this.manualTab = manualTab;
            this.stepSeqTab = stepSeqTab;
            this.waferSeqTab = waferSeqTab;
            this.calibrationTab = calibrationTab;
            this.visionTab = visionTab;
            this.recipeViewModel = sub02ViewModel;
            this._recipeService = recipeService;
            SetTab(selectedTabKey);

            stepSeqTabViewModel.RecipeComponentChanged += OnRecipeComponentChanged;

            //CurrentDevice = this.recipeViewModel.SelectedRecipe.Name;
        }

        private void OnRecipeComponentChanged(ComponentType component)
        {
            if (component == ComponentType.WAFER)
                SetTab("WAFER");
            else
                SetTab("STEP");
        }

        [RelayCommand]
        public void SetTab(string viewName)
        {
            if (viewName == "STEP" && _recipeService.UseRecipe?.Component == ComponentType.WAFER)
                viewName = "WAFER";
            else if (viewName == "WAFER" && _recipeService.UseRecipe?.Component != ComponentType.WAFER)
                viewName = "STEP";

            SelectedTabKey = viewName;
            switch (viewName)
            {
                case "LOADING":
                    CurrentTab = loadingTab; break;
                case "AUTO": CurrentTab = autoTab; break;
                case "MANUAL": CurrentTab = manualTab; break;
                case "STEP": CurrentTab = stepSeqTab; break;
                case "WAFER": CurrentTab = waferSeqTab; break;
                case "CALIBRATION": CurrentTab = calibrationTab; break;
                case "VISION": CurrentTab = visionTab; break;
            }
        }

    }
}
