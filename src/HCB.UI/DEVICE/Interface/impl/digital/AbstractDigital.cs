using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class AbstractDigital : ObservableObject, IIoData
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
    }
}
