using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace HCB.UI
{
    public partial class StateCell : UserControl
    {
        public StateCell()
        {
            InitializeComponent();

        }

        public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StateCell));

        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(StateCell));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }
    }
}
