using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public abstract partial class AbstractAnalog : AbstractIoBase
    {
        [ObservableProperty]
        private int _length;

        [ObservableProperty]
        private double _maxValue;

        [ObservableProperty]
        private double _minValue;

        [ObservableProperty]
        private string _unit = "";

        // removed type-specific event - use base.ValueChanged
    }
}
