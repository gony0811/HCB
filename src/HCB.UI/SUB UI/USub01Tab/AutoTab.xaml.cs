
using HCB.IoC;
using System.Windows;
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AutoTabViewModel vm && vm.LoadedCommand.CanExecute(null))
            {
                vm.LoadedCommand.Execute(null);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AutoTabViewModel vm && vm.UnloadedCommand.CanExecute(null))
            {
                vm.UnloadedCommand.Execute(null);
            }
        }
    }
}
