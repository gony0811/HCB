using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HCB.UI
{
    public partial class MotionMoveController : UserControl
    {
        public MotionMoveController()
        {
            InitializeComponent();
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (TabPosition == null) return;

            var rb = sender as RadioButton;
            string header = rb?.Content?.ToString() ?? "";

            TabPosition.Visibility = Visibility.Collapsed;
            TabJog.Visibility = Visibility.Collapsed;

            switch (header)
            {
                case "위치 이동": TabPosition.Visibility = Visibility.Visible; break;
                case "조그 이동": TabJog.Visibility = Visibility.Visible; break;
            }
        }
    }
}
