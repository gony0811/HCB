using HCB.IoC;
using System.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Scoped)]
    public partial class USub04 : Page
    {
        public USub04(USub04ViewModel vm)
        {
            InitializeComponent();
            this.DataContext = vm;
        }
    }
}
