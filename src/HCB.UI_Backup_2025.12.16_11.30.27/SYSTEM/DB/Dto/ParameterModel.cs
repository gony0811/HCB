using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class ParameterModel : ObservableObject
    {
        [ObservableProperty] private string name = "";
        [ObservableProperty] private string value = "";
        [ObservableProperty] private string maximumValue = "";
        [ObservableProperty] private string minimumValue = "";
        [ObservableProperty] private string unit = "";
    }
}
