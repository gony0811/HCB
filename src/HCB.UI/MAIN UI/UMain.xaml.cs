using HCB.IoC;
using System.Windows;

namespace HCB.UI
{
    [View(Lifetime.Singleton)]
    public partial class UMain : Window  
    {
        public UMain()
        {
            InitializeComponent();
        }

        public UMain(UMainViewModel vm) : this()
        {
            this.DataContext = vm;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            App.Current.Shutdown();
        }
    }
}