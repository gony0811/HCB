using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public abstract partial class AbstractDigital : AbstractIoBase
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private string address;
        [ObservableProperty] private int index;
        [ObservableProperty] private IoType ioType;
        [ObservableProperty] private IDevice device;
        [ObservableProperty] private bool isEnabled;
        [ObservableProperty] private string description;

        public delegate void ValueChangedEventHandler(object sender, ValueChangedEventArgs<bool> e);
        public event ValueChangedEventHandler? ValueChanged;

        protected virtual void OnValueChanged(ValueChangedEventArgs<bool> e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }
}
