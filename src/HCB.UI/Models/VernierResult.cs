using CommunityToolkit.Mvvm.ComponentModel;


namespace HCB.UI
{
    public partial class VernierResult : ObservableObject
    {
        [ObservableProperty] private Point2D rV3 = new Point2D(0, 0);
        [ObservableProperty] private Point2D rV1 = new Point2D(0, 0);
        [ObservableProperty] private Point2D lV3 = new Point2D(0, 0);
        [ObservableProperty] private Point2D lV1 = new Point2D(0, 0);
        [ObservableProperty] private Point2D cV3 = new Point2D(0, 0);
        [ObservableProperty] private Point2D cV1 = new Point2D(0, 0);

    }
}
