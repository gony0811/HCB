using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class StepRecipeCreateDto : ObservableObject
    {
        [ObservableProperty] private int stepNumber;
        [ObservableProperty] private double force;
        [ObservableProperty] private double durationTime;
        [ObservableProperty] private string description = "";
    }
}
