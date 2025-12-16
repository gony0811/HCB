

using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class MotionPositionCreate : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private double speed;
        [ObservableProperty] private double minimumSpeed;
        [ObservableProperty] private double maximumSpeed;

        [ObservableProperty] private double location;
        [ObservableProperty] private double minimumLocation;
        [ObservableProperty] private double maximumLocation;
    }
}
