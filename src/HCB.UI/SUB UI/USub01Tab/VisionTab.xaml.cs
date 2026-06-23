using HCB.IoC;
using System.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Scoped)]
    public partial class VisionTab : UserControl
    {
        public VisionTab(VisionTabViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
