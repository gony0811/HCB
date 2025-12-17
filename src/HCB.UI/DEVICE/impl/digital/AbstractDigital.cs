using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;

namespace HCB.UI
{
    public abstract partial class AbstractDigital : AbstractIoBase
    {
        private bool _value = false;

        public virtual bool Value
        {
            get { return _value; }

            set
            {                  
                _value = value;
            }
        }
    }
}
