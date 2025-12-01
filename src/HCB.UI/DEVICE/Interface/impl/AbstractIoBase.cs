using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;

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
    }
}