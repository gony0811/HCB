
using HCB.IoC;
using System.Windows;
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
        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (TabDTable == null) return; // 초기화 전 방어

            var rb = sender as RadioButton;
            string header = rb?.Content?.ToString() ?? "";

            TabDTable.Visibility = Visibility.Collapsed;
            TabPTable.Visibility = Visibility.Collapsed;
            TabBHead.Visibility = Visibility.Collapsed;
            TabWTable.Visibility = Visibility.Collapsed;

            switch (header)
            {
                case "D-TABLE": TabDTable.Visibility = Visibility.Visible; break;
                case "P-TABLE": TabPTable.Visibility = Visibility.Visible; break;
                case "B-HEAD": TabBHead.Visibility = Visibility.Visible; break;
                case "W-TABLE": TabWTable.Visibility = Visibility.Visible; break;
            }
        }
    }
}
