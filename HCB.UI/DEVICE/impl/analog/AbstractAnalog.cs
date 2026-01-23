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

        private double _value = 0.0;

        public virtual double Value
        {
            get { return _value; }

            set
            {
                if (_value != value)
                {
                    var old = _value;
                    _value = value;
                }
            }
        }
    }
}
