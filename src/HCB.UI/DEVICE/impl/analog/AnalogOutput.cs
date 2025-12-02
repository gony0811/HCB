using HCB.Data.Entity.Type;

namespace HCB.UI
{ 
    public partial class AnalogOutput : AbstractAnalog
    {
        public AnalogOutput()   
        {
            IoType = IoType.AnalogOutput;
        }

        private double _value = 0.0;

        public double Value
        {
            get { return _value; }

            set
            {
                _value = value;

                if (Device == null || !Device.IsConnected)
                {
                    return;
                }
                else
                {
                    string command = string.Format("{0}{1:D4}={2}", Address, Index, value);
                    Device.SendCommand(command);
                    OnValueChanged(new ValueChangedEventArgs<double>(_value, value));
                }
            }
        }
    }
}
