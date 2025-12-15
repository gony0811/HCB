using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;

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
        private UnitType _unit;

        // removed type-specific event - use base.ValueChanged
    }
}
