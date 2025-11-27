using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class DMotionPosition : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private double speed;
        [ObservableProperty] private double location;
        [ObservableProperty] private IMotion parentMotion;
    }
}
