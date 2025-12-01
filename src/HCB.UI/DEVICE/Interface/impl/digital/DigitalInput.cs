using HCB.Data.Entity.Type;

namespace HCB.UI
{
    public class DigitalInput : AbstractDigital
    {
        public DigitalInput()
        {
            IoType = IoType.DigitalInput;
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
                }
            }
        }
    }
}
