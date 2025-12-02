using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;

namespace HCB.UI
{
    public abstract partial class AbstractIoBase : ObservableObject, IIoData
    {
        [ObservableProperty]
        private int _id;

        [ObservableProperty]
        private string _name = "";

        [ObservableProperty]
        private string _address = "";

        [ObservableProperty]
        private int _index;

        [ObservableProperty]
        private IoType _ioType;

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private string _description = "";

        [ObservableProperty]
        private IDevice? _device;

        // Centralized value-changed event using object as the value type
        public event EventHandler<ValueChangedEventArgs<object>>? ValueChanged;

        protected virtual void OnValueChanged(object? oldValue, object newValue)
        {
            ValueChanged?.Invoke(this, new ValueChangedEventArgs<object>(oldValue, newValue));
        }
    }
}