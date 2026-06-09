using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class StepRecipeCreateDto : ObservableObject
    {
        [ObservableProperty] private string name = "";
        [ObservableProperty] private int stepNumber;
        [ObservableProperty] private int accTime;
        [ObservableProperty] private int accTime2;
        [ObservableProperty] private int contTime;
        [ObservableProperty] private int decTime;
        [ObservableProperty] private double loadCell;
        [ObservableProperty] private double current;
        [ObservableProperty] private double current2;
        [ObservableProperty] private int vacOffTime;
        [ObservableProperty] private string description = "";
    }
}
