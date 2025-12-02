using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;

namespace HCB.UI
{ 
    public partial class AnalogInput : AbstractAnalog
    {
        public AnalogInput() 
        {
            IoType = IoType.AnalogInput;
        }

        private double _value = 0.0;

        public double Value
        {
            get { return _value; }

            set
            {
                if (_value != value)
                {                 
                    _value = value;
                    OnValueChanged(new ValueChangedEventArgs<double>(_value, value));
                }
            }
        }
    }
}
