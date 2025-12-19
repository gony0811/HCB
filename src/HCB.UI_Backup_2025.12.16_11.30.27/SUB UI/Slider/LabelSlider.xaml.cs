

using System.Windows;
using System.Windows.Controls.Primitives;
using UserControl = System.Windows.Controls.UserControl;

namespace HCB.UI
{
    public partial class LabelSlider : UserControl
    {
        public LabelSlider()
        {
            InitializeComponent();
        }

        // ① 라벨 텍스트
        public static readonly DependencyProperty TitleLabelProperty =
            DependencyProperty.Register(nameof(TitleLabel), typeof(string),
                typeof(LabelSlider), new PropertyMetadata("X"));
        public string TitleLabel
        {
            get => (string)GetValue(TitleLabelProperty);
            set => SetValue(TitleLabelProperty, value);
        }

        // ② 값 (TwoWay 바인딩용)
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double),
                typeof(LabelSlider), new PropertyMetadata(0.0));
        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        // ③ 범위/틱/스냅
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double),
                typeof(LabelSlider), new PropertyMetadata(1.0));
        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double),
                typeof(LabelSlider), new PropertyMetadata(100.0));
        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty TickFrequencyProperty =
            DependencyProperty.Register(nameof(TickFrequency), typeof(double),
                typeof(LabelSlider), new PropertyMetadata(1.0));
        public double TickFrequency
        {
            get => (double)GetValue(TickFrequencyProperty);
            set => SetValue(TickFrequencyProperty, value);
        }

        public static readonly DependencyProperty IsSnapToTickEnabledProperty =
            DependencyProperty.Register(nameof(IsSnapToTickEnabled), typeof(bool),
                typeof(LabelSlider), new PropertyMetadata(true));
        public bool IsSnapToTickEnabled
        {
            get => (bool)GetValue(IsSnapToTickEnabledProperty);
            set => SetValue(IsSnapToTickEnabledProperty, value);
        }

        public static readonly DependencyProperty TickPlacementProperty =
            DependencyProperty.Register(nameof(TickPlacement), typeof(TickPlacement),
                typeof(LabelSlider), new PropertyMetadata(TickPlacement.BottomRight));
        public TickPlacement TickPlacement
        {
            get => (TickPlacement)GetValue(TickPlacementProperty);
            set => SetValue(TickPlacementProperty, value);
        }

        // ④ 키보드/마우스 휠 변화량
        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(nameof(SmallChange), typeof(double),
                typeof(LabelSlider), new PropertyMetadata(1.0));
        public double SmallChange
        {
            get => (double)GetValue(SmallChangeProperty);
            set => SetValue(SmallChangeProperty, value);
        }

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(nameof(LargeChange), typeof(double),
                typeof(LabelSlider), new PropertyMetadata(10.0));
        public double LargeChange
        {
            get => (double)GetValue(LargeChangeProperty);
            set => SetValue(LargeChangeProperty, value);
        }

        // ⑤ 하단 TextBox 폭
        public static readonly DependencyProperty TextBoxWidthProperty =
            DependencyProperty.Register(nameof(TextBoxWidth), typeof(double),
                typeof(LabelSlider), new PropertyMetadata(50.0));
        public double TextBoxWidth
        {
            get => (double)GetValue(TextBoxWidthProperty);
            set => SetValue(TextBoxWidthProperty, value);
        }
    }
}
