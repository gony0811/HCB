using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;


namespace HCB.UI
{
    public class ValueChangedEventArgs<T> : EventArgs
    {
        public T Value { get; private set; }
        public ValueChangedEventArgs(T value)
        {
            Value = value;
        }
    }

    public partial class AbstractAnalog : ObservableObject, IIoData
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private string address;
        [ObservableProperty] private int index;
        [ObservableProperty] private int length;
        [ObservableProperty] private double maxValue;
        [ObservableProperty] private double minValue;
        [ObservableProperty] private string unit;
        [ObservableProperty] private bool isEnabled;
        [ObservableProperty] private string description;

        [ObservableProperty] private IoType ioType;

        [ObservableProperty] private IDevice device;

        public delegate void ValueChangedEventHandler(object sender, ValueChangedEventArgs<double> e);
    }
}
