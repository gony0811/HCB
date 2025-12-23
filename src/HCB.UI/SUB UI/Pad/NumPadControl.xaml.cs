using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HCB.UI
{
    public partial class NumPadControl : UserControl
    {
        public NumPadControl()
        {
            InitializeComponent();
        }

        // =========================
        // Dependency Properties
        // =========================
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(NumPadControl), new PropertyMetadata("0"));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumPadControl), new PropertyMetadata(0d));

        public static readonly DependencyProperty ApplyCommandProperty =
            DependencyProperty.Register(nameof(ApplyCommand), typeof(ICommand), typeof(NumPadControl));

        public static readonly DependencyProperty CancelCommandProperty =
            DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(NumPadControl));

        public static readonly DependencyProperty MinimumValueProperty =
            DependencyProperty.Register(nameof(MinimumValue), typeof(string), typeof(NumPadControl), new PropertyMetadata(""));

        public static readonly DependencyProperty MaximumValueProperty =
            DependencyProperty.Register(nameof(MaximumValue), typeof(string), typeof(NumPadControl), new PropertyMetadata(""));

        public static readonly DependencyProperty CurrentValueProperty =
            DependencyProperty.Register(nameof(CurrentValue), typeof(string), typeof(NumPadControl), new PropertyMetadata("0"));

        // =========================
        // Properties
        // =========================
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public string MinimumValue { get => (string)GetValue(MinimumValueProperty); set => SetValue(MinimumValueProperty, value); }
        public string MaximumValue { get => (string)GetValue(MaximumValueProperty); set => SetValue(MaximumValueProperty, value); }
        public string CurrentValue { get => (string)GetValue(CurrentValueProperty); set => SetValue(CurrentValueProperty, value); }

        public ICommand ApplyCommand { get => (ICommand)GetValue(ApplyCommandProperty); set => SetValue(ApplyCommandProperty, value); }
        public ICommand CancelCommand { get => (ICommand)GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }

        // =========================
        // Button Logic
        // =========================
        private void OnDigit(object sender, RoutedEventArgs e)
        {
            if (sender is Button b)
                Text = Text == "0" ? b.Content.ToString() : Text + b.Content;
        }

        private void OnDot(object sender, RoutedEventArgs e)
        {
            if (!Text.Contains(".")) Text += ".";
        }

        private void OnBackspace(object sender, RoutedEventArgs e)
        {
            if (Text.Length > 1) Text = Text[..^1];
            else Text = "0";
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            Text = "0";
        }

        private void OnIncrement(object sender, RoutedEventArgs e)
        {
            Value = double.Parse(Text, CultureInfo.InvariantCulture) + 1;
            Text = Value.ToString(CultureInfo.InvariantCulture);
        }

        private void OnDecrement(object sender, RoutedEventArgs e)
        {
            Value = double.Parse(Text, CultureInfo.InvariantCulture) - 1;
            Text = Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
