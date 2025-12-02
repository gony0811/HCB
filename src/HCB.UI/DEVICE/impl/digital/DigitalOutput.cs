using HCB.Data.Entity.Type;

namespace HCB.UI
{
    public class DigitalOutput : AbstractDigital
    {
        public DigitalOutput()
        {
            IoType = IoType.DigitalOutput;
        }

        private bool _value = false;

        public bool Value
        {
            get { return _value; }

            set
            {
                var old = _value;
                _value = value;

                if (Device == null || !Device.IsConnected)
                {
                    return;
                }
                else
                {
                    string command = string.Format("{0}{1:D4}={2}", Address, Index, _value? 1:0);
                    Device.SendCommand(command);
                    OnValueChanged(old, value);
                }
            }
        }
    }
}
