using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HCB.UI
{
    public partial class HeaderBar : UserControl
    {
        public HeaderBar()
        {
            InitializeComponent();
        }

        // ─────────────────────────────────────────────────────────────
        // Header Background
        // ─────────────────────────────────────────────────────────────
        public static readonly DependencyProperty HeaderBackgroundProperty =
            DependencyProperty.Register(nameof(HeaderBackground), typeof(Brush), typeof(HeaderBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x0D, 0x1B, 0x2A))));

        public Brush HeaderBackground
        {
            get => (Brush)GetValue(HeaderBackgroundProperty);
            set => SetValue(HeaderBackgroundProperty, value);
        }

        // ─────────────────────────────────────────────────────────────
        // Accent Bar
        // ─────────────────────────────────────────────────────────────
        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register(nameof(AccentColor), typeof(Brush), typeof(HeaderBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB))));

        public Brush AccentColor
        {
            get => (Brush)GetValue(AccentColorProperty);
            set => SetValue(AccentColorProperty, value);
        }

        public static readonly DependencyProperty AccentBarWidthProperty =
            DependencyProperty.Register(nameof(AccentBarWidth), typeof(double), typeof(HeaderBar),
                new PropertyMetadata(3.0));

        public double AccentBarWidth
        {
            get => (double)GetValue(AccentBarWidthProperty);
            set => SetValue(AccentBarWidthProperty, value);
        }

        public static readonly DependencyProperty AccentBarHeightProperty =
            DependencyProperty.Register(nameof(AccentBarHeight), typeof(double), typeof(HeaderBar),
                new PropertyMetadata(18.0));

        public double AccentBarHeight
        {
            get => (double)GetValue(AccentBarHeightProperty);
            set => SetValue(AccentBarHeightProperty, value);
        }

        // ─────────────────────────────────────────────────────────────
        // Title
        // ─────────────────────────────────────────────────────────────
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(HeaderBar),
                new PropertyMetadata("Title"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleFontSizeProperty =
            DependencyProperty.Register(nameof(TitleFontSize), typeof(double), typeof(HeaderBar),
                new PropertyMetadata(16.0));

        public double TitleFontSize
        {
            get => (double)GetValue(TitleFontSizeProperty);
            set => SetValue(TitleFontSizeProperty, value);
        }

        public static readonly DependencyProperty TitleForegroundProperty =
            DependencyProperty.Register(nameof(TitleForeground), typeof(Brush), typeof(HeaderBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xEC, 0xF0, 0xF1))));

        public Brush TitleForeground
        {
            get => (Brush)GetValue(TitleForegroundProperty);
            set => SetValue(TitleForegroundProperty, value);
        }

        // ─────────────────────────────────────────────────────────────
        // SubTitle Badge
        // ─────────────────────────────────────────────────────────────
        public static readonly DependencyProperty SubTitleProperty =
            DependencyProperty.Register(nameof(SubTitle), typeof(string), typeof(HeaderBar),
                new PropertyMetadata(string.Empty, OnSubTitleChanged));

        public string SubTitle
        {
            get => (string)GetValue(SubTitleProperty);
            set => SetValue(SubTitleProperty, value);
        }

        private static void OnSubTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeaderBar hb)
                hb.BadgeVisibility = string.IsNullOrEmpty(e.NewValue as string)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        public static readonly DependencyProperty SubTitleFontSizeProperty =
            DependencyProperty.Register(nameof(SubTitleFontSize), typeof(double), typeof(HeaderBar),
                new PropertyMetadata(10.0));

        public double SubTitleFontSize
        {
            get => (double)GetValue(SubTitleFontSizeProperty);
            set => SetValue(SubTitleFontSizeProperty, value);
        }

        public static readonly DependencyProperty SubTitleForegroundProperty =
            DependencyProperty.Register(nameof(SubTitleForeground), typeof(Brush), typeof(HeaderBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB))));

        public Brush SubTitleForeground
        {
            get => (Brush)GetValue(SubTitleForegroundProperty);
            set => SetValue(SubTitleForegroundProperty, value);
        }

        public static readonly DependencyProperty BadgeBackgroundProperty =
            DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(HeaderBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x5C))));

        public Brush BadgeBackground
        {
            get => (Brush)GetValue(BadgeBackgroundProperty);
            set => SetValue(BadgeBackgroundProperty, value);
        }

        // BadgeVisibility는 SubTitle 유무에 따라 자동 계산 (내부용)
        public static readonly DependencyProperty BadgeVisibilityProperty =
            DependencyProperty.Register(nameof(BadgeVisibility), typeof(Visibility), typeof(HeaderBar),
                new PropertyMetadata(Visibility.Collapsed));

        public Visibility BadgeVisibility
        {
            get => (Visibility)GetValue(BadgeVisibilityProperty);
            private set => SetValue(BadgeVisibilityProperty, value);
        }

        // ─────────────────────────────────────────────────────────────
        // Status (우측 점 + 텍스트)
        // ─────────────────────────────────────────────────────────────
        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(HeaderBar),
                new PropertyMetadata(string.Empty, OnStatusTextChanged));

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }

        private static void OnStatusTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeaderBar hb)
                hb.StatusVisibility = string.IsNullOrEmpty(e.NewValue as string)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        public static readonly DependencyProperty StatusColorProperty =
            DependencyProperty.Register(nameof(StatusColor), typeof(Brush), typeof(HeaderBar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))));

        public Brush StatusColor
        {
            get => (Brush)GetValue(StatusColorProperty);
            set => SetValue(StatusColorProperty, value);
        }

        public static readonly DependencyProperty StatusFontSizeProperty =
            DependencyProperty.Register(nameof(StatusFontSize), typeof(double), typeof(HeaderBar),
                new PropertyMetadata(10.0));

        public double StatusFontSize
        {
            get => (double)GetValue(StatusFontSizeProperty);
            set => SetValue(StatusFontSizeProperty, value);
        }

        public static readonly DependencyProperty StatusDotSizeProperty =
            DependencyProperty.Register(nameof(StatusDotSize), typeof(double), typeof(HeaderBar),
                new PropertyMetadata(7.0));

        public double StatusDotSize
        {
            get => (double)GetValue(StatusDotSizeProperty);
            set => SetValue(StatusDotSizeProperty, value);
        }

        // StatusVisibility는 StatusText 유무에 따라 자동 계산 (내부용)
        public static readonly DependencyProperty StatusVisibilityProperty =
            DependencyProperty.Register(nameof(StatusVisibility), typeof(Visibility), typeof(HeaderBar),
                new PropertyMetadata(Visibility.Collapsed));

        public Visibility StatusVisibility
        {
            get => (Visibility)GetValue(StatusVisibilityProperty);
            private set => SetValue(StatusVisibilityProperty, value);
        }
    }
}
