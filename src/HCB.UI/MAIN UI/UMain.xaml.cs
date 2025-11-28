
using HCB.IoC;
using System.Linq;
using System.Windows;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Singleton)]
    public partial class UMain : Window
    {
        public UMain(UMainViewModel vm)
        {        
            InitializeComponent();
            this.DataContext = vm;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            App.Current.Shutdown();
        }
    }
}
