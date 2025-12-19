
using HCB.IoC;
using System.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Scoped)]
    public partial class AutoTab : UserControl
    {
        public AutoTab(AutoTabViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
