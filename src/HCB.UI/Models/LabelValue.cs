using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class LabelValue : ObservableObject
    {
        [ObservableProperty] private string label;
        [ObservableProperty] private string value;

        public LabelValue(string label, string value)
        {
            this.Label = label;
            this.Value = value;
        }
    }
}
