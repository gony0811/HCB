using UserControl = System.Windows.Controls.UserControl;
using System.Windows;
using System.Windows.Input;

namespace HCB.UI
{
    public partial class MotorStateControl : UserControl
    {
        public MotorStateControl()
        {
            InitializeComponent();
        }

        // 외부에서 주입할 VM
        public MotorStateControlVM ViewModel
        {
            get => (MotorStateControlVM)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(MotorStateControlVM),
                typeof(MotorStateControl),
                new PropertyMetadata(null));

        // h_z 전용 홈 시퀀스 커맨드 (외부 주입)
        public ICommand HzHomeCommand
        {
            get => (ICommand)GetValue(HzHomeCommandProperty);
            set => SetValue(HzHomeCommandProperty, value);
        }

        public static readonly DependencyProperty HzHomeCommandProperty =
            DependencyProperty.Register(
                nameof(HzHomeCommand),
                typeof(ICommand),
                typeof(MotorStateControl),
                new PropertyMetadata(null));
    }
}
