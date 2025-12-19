using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;

namespace HCB.UI
{
    public partial class DeviceItem : ObservableObject
    {
        [ObservableProperty]
        private int index;

        [ObservableProperty]
        private string name;

        [ObservableProperty] 
        private bool isInUse;
    }
}
