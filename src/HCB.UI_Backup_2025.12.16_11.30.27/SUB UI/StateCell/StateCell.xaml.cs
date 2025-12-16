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

            Loaded += (_, __) =>
            {
                if (AutoUpdateLabel && string.IsNullOrEmpty(Label))
                    Label = DefaultLabel; // 초기 1회 자동 채움
            };
        }

        // ● 점 크기
        public static readonly DependencyProperty DotSizeProperty =
            DependencyProperty.Register(nameof(DotSize), typeof(double),
                typeof(StateCell), new PropertyMetadata(14.0));
        public double DotSize
        {
            get => (double)GetValue(DotSizeProperty);
            set => SetValue(DotSizeProperty, value);
        }

        // ● 폰트 크기 (Label)
        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double),
                typeof(StateCell), new PropertyMetadata(14.0));
        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        // ● 상태
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(State),
                typeof(StateCell),
                new PropertyMetadata(State.Unknown));
        public State State
        {
            get => (State)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        // ● 상단 라벨
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string),
                typeof(StateCell), new PropertyMetadata(null));
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        // ● 텍스트 색상
        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(nameof(TextBrush), typeof(Brush),
                typeof(StateCell),
                new PropertyMetadata(Brushes.White));
        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        // ● 자동 라벨 업데이트
        public static readonly DependencyProperty AutoUpdateLabelProperty =
           DependencyProperty.Register(nameof(AutoUpdateLabel), typeof(bool),
               typeof(StateCell), new PropertyMetadata(true));
        public bool AutoUpdateLabel
        {
            get => (bool)GetValue(AutoUpdateLabelProperty);
            set => SetValue(AutoUpdateLabelProperty, value);
        }

        // ● 기본 라벨
        public static readonly DependencyProperty DefaultLabelProperty =
            DependencyProperty.Register(nameof(DefaultLabel), typeof(string),
                typeof(StateCell), new PropertyMetadata("STATUS"));
        public string DefaultLabel
        {
            get => (string)GetValue(DefaultLabelProperty);
            set => SetValue(DefaultLabelProperty, value);
        }
    }
}
