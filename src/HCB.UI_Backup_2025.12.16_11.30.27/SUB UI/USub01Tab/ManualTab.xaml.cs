
using HCB.IoC;
using System.Windows.Controls;

namespace HCB.UI
{

    [View(Lifetime.Scoped)]
    public partial class ManualTab : UserControl
    {
        public ManualTab(ManualTabViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
