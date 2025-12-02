using HCB.Data.Entity.Type;

namespace HCB.UI
{
    public class DigitalInput : AbstractDigital
    {
        public DigitalInput()
        {
            IoType = IoType.DigitalInput;
        }

        private bool _value = false;

        public bool Value
        {
            get { return _value; }

            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnValueChanged(new ValueChangedEventArgs<bool>(_value, value));
                }
            }
        }
    }
}
