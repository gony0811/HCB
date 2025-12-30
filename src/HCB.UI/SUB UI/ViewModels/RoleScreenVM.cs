using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class RoleScreenVM : ObservableObject
    {
        [ObservableProperty] private int roleId;
        [ObservableProperty] private int screenId;
        [ObservableProperty] private string screenName;
        [ObservableProperty] private bool granted;
            
    }
}
