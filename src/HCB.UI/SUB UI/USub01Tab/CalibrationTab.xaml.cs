using HCB.IoC;
using System.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Scoped)]
    public partial class CalibrationTab : UserControl
    {
        public CalibrationTab(CalibrationTabViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
