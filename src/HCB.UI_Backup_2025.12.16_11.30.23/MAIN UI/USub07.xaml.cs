using HCB.IoC;
using System.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Singleton)]
    public partial class USub07 : Page
    {
        public USub07(USub07ViewModel vm)
        {
            this.DataContext = vm;
            InitializeComponent();
        }
    }
}
