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

        public override bool Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    var old = _value;
                    _value = value;
                    OnValueChanged(old, value);
                }
            }
        }
    }
}
