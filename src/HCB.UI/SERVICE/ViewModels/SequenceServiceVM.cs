using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class SequenceServiceVM : ObservableObject
    {
        [ObservableProperty]
        private string statusMessage;
    }
}
