using System.Windows.Media;
using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using System.Globalization;
using System.Windows.Data;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using System.Windows.Input;
using System;

namespace HCB.UI
{
    /// <summary>
    /// Interaction logic for IoPillToggle.xaml
    /// </summary>

    public partial class IoPillToggle : UserControl
    {
        public IoPillToggle() => InitializeComponent();

        // Command 
        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand),
            typeof(IoPillToggle), new PropertyMetadata(null));
        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object),
            typeof(IoPillToggle), new PropertyMetadata(null));
        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        // 상태
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool),
                typeof(IoPillToggle), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

        // 입력신호 표기(조작 불가)
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(IoPillToggle), new PropertyMetadata(false));
        public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

        // 라벨
        public static readonly DependencyProperty OnTextProperty =
            DependencyProperty.Register(nameof(OnText), typeof(string), typeof(IoPillToggle), new PropertyMetadata("ON"));
        public string OnText { get => (string)GetValue(OnTextProperty); set => SetValue(OnTextProperty, value); }

        public static readonly DependencyProperty OffTextProperty =
            DependencyProperty.Register(nameof(OffText), typeof(string), typeof(IoPillToggle), new PropertyMetadata("OFF"));
        public string OffText { get => (string)GetValue(OffTextProperty); set => SetValue(OffTextProperty, value); }

        public static readonly DependencyProperty LabelFontSizeProperty =
            DependencyProperty.Register(nameof(LabelFontSize), typeof(double), typeof(IoPillToggle), new PropertyMetadata(12.0));
        public double LabelFontSize { get => (double)GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }

        // 인디케이터 크기/색
        public static readonly DependencyProperty IndicatorSizeProperty =
            DependencyProperty.Register(nameof(IndicatorSize), typeof(double), typeof(IoPillToggle), new PropertyMetadata(18.0));
        public double IndicatorSize { get => (double)GetValue(IndicatorSizeProperty); set => SetValue(IndicatorSizeProperty, value); }

        public static readonly DependencyProperty IndicatorOnFillProperty =
            DependencyProperty.Register(nameof(IndicatorOnFill), typeof(Brush), typeof(IoPillToggle),
                new PropertyMetadata((Brush)new BrushConverter().ConvertFromString("#17C3B2"))); // 청록
        public Brush IndicatorOnFill { get => (Brush)GetValue(IndicatorOnFillProperty); set => SetValue(IndicatorOnFillProperty, value); }

        public static readonly DependencyProperty IndicatorOffStrokeProperty =
            DependencyProperty.Register(nameof(IndicatorOffStroke), typeof(Brush), typeof(IoPillToggle),
                new PropertyMetadata((Brush)new BrushConverter().ConvertFromString("#E74C3C"))); // 빨강 링
        public Brush IndicatorOffStroke { get => (Brush)GetValue(IndicatorOffStrokeProperty); set => SetValue(IndicatorOffStrokeProperty, value); }

        // pill 색/라벨 색
        public static readonly DependencyProperty PillOnBrushProperty =
            DependencyProperty.Register(nameof(PillOnBrush), typeof(Brush), typeof(IoPillToggle),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public Brush PillOnBrush { get => (Brush)GetValue(PillOnBrushProperty); set => SetValue(PillOnBrushProperty, value); }

        public static readonly DependencyProperty PillOffBrushProperty =
            DependencyProperty.Register(nameof(PillOffBrush), typeof(Brush), typeof(IoPillToggle),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public Brush PillOffBrush { get => (Brush)GetValue(PillOffBrushProperty); set => SetValue(PillOffBrushProperty, value); }

        public static readonly DependencyProperty LabelActiveBrushProperty =
            DependencyProperty.Register(nameof(LabelActiveBrush), typeof(Brush), typeof(IoPillToggle),
                new PropertyMetadata(Brushes.White));
        public Brush LabelActiveBrush { get => (Brush)GetValue(LabelActiveBrushProperty); set => SetValue(LabelActiveBrushProperty, value); }

        public static readonly DependencyProperty LabelInactiveBrushProperty =
            DependencyProperty.Register(nameof(LabelInactiveBrush), typeof(Brush), typeof(IoPillToggle),
                new PropertyMetadata((Brush)new BrushConverter().ConvertFromString("#B3FFFFFF")));
        public Brush LabelInactiveBrush { get => (Brush)GetValue(LabelInactiveBrushProperty); set => SetValue(LabelInactiveBrushProperty, value); }

    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b ? !b : v;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is bool b ? !b : v;
    }


}
