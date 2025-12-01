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
                _value = value;

                if (Device == null || !Device.IsConnected)
                {
                    return;
                }
                else
                {
                    string command = string.Format("{0}{1:D4}", Address, Index);
                    _value = Device.SendCommand<bool>(command).Result;
                }
            }
        }
    }
}
