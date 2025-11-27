using System.Windows;
using System.Windows.Controls;

namespace HCB.UI
{
    public partial class MotorStatusTable : UserControl
    {
        public MotorStatusTable()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double),
                typeof(MotorStatusTable), new PropertyMetadata(14.0));
        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        
        

    }

}
