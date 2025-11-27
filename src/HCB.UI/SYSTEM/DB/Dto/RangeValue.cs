using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public partial class RangeValue : ObservableObject
    {
        [ObservableProperty]
        private double currentValue = 0.0;

        [ObservableProperty]
        private double minimum = 0.0;

        [ObservableProperty]
        private double maximum = 0.0;

        public RangeValue(double currentValue, double minimum, double maximum)
        {
            this.currentValue = currentValue;
            this.minimum = minimum;
            this.maximum = maximum;
        }
    }
}
