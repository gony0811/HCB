
using System.Windows.Controls;
using HCB.IoC;

namespace HCB.UI
{
    [View(Lifetime.Singleton)]
    public partial class USub05 : Page
    {
        public USub05(USub05ViewModel vm)
        {
            this.DataContext = vm;
            InitializeComponent();
        }

    }
}
