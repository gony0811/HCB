using CommunityToolkit.Mvvm.ComponentModel;

namespace HCB.UI
{
    public abstract partial class AbstractAnalog : AbstractIoBase
    {
        [ObservableProperty]
        private int _length;

        [ObservableProperty]
        private double _maxValue;

        [ObservableProperty]
        private double _minValue;

        [ObservableProperty]
        private string _unit = "";

        public delegate void ValueChangedEventHandler(object sender, ValueChangedEventArgs<double> e);
        public event ValueChangedEventHandler? ValueChanged;

        protected virtual void OnValueChanged(ValueChangedEventArgs<double> e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }
}
