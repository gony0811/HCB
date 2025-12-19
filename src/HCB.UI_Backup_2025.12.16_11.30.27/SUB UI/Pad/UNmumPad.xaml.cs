using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    public partial class UNmumPad : RadWindow, INotifyPropertyChanged
    {
        public UNmumPad()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(Text))
                    Text = CurrentValue;
                if (string.IsNullOrWhiteSpace(Text))
                    Text = "0";

                // 키보드 포커스 입력박스로
                if (InputBox != null)
                {
                    InputBox.Focus();
                    InputBox.SelectAll();
                }
            };
        }

        // ===== 바인딩 프로퍼티 =====
        private string _currentValue = "0";
        public string CurrentValue { get => _currentValue; set => Set(ref _currentValue, value); }

        private string _maximumValue = "";
        public string MaximumValue { get => _maximumValue; set => Set(ref _maximumValue, value); }

        private string _minimumValue = "";
        public string MinimumValue { get => _minimumValue; set => Set(ref _minimumValue, value); }

        private string _text = "0";
        public string Text { get => _text; set => Set(ref _text, value); }

        private double _step = 1.0;
        public double Step { get => _step; set => Set(ref _step, value); }

        private bool _allowDecimal = true;
        public bool AllowDecimal { get => _allowDecimal; set => Set(ref _allowDecimal, value); }

        // ====== 유틸: 입력 조립 ======
        private void AppendDigit(string d)
        {
            if (string.IsNullOrEmpty(d)) return;
            if (Text == "0" || Text == "-0")
                Text = Text.StartsWith("-") ? "-" + d : d;
            else
                Text += d;
        }

        private void AppendDot()
        {
            if (!AllowDecimal) return;
            if (string.IsNullOrEmpty(Text)) { Text = "0."; return; }
            if (Text.IndexOf('.') < 0) Text += ".";
            // 점 입력 직후는 중간 상태라 스냅 생략
        }

        // ===== 버튼 핸들러 =====
        private void OnDigit(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn == null) return;

            string s =
                btn.CommandParameter as string ??
                btn.Tag as string ??
                btn.Content as string ??
                ((btn.Content as TextBlock) != null ? (btn.Content as TextBlock).Text : null);

            if (string.IsNullOrWhiteSpace(s))
                return;

            if (s.Length == 1 && char.IsDigit(s[0]))
                AppendDigit(s);
        }

        private void OnDot(object sender, RoutedEventArgs e) => AppendDot();

        private void OnBackspace(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Text) || Text == "0") return;

            // C# 7.3: 범위 연산자 대신 Substring
            if (Text.Length == 1 || (Text.Length == 2 && Text.StartsWith("-")))
                Text = "0";
            else
                Text = Text.Substring(0, Text.Length - 1);
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            Text = "0";
        }

        private void OnIncrement(object sender, RoutedEventArgs e)
        {
            var v = ParseOrZero(Text) + Step;
            Text = FormatNumber(ClampToRange(v));
        }

        private void OnDecrement(object sender, RoutedEventArgs e)
        {
            var v = ParseOrZero(Text) - Step;
            Text = FormatNumber(ClampToRange(v));
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            var v = ClampToRange(ParseOrZero(Text));
            Text = FormatNumber(v);
            CurrentValue = Text;
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ===== 키보드 입력 처리 =====
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 수정키 조합은 무시
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
                return;

            char ch;
            if (TryMapDigitKey(e.Key, out ch))
            {
                AppendDigit(ch.ToString());
                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                case Key.OemPeriod:
                case Key.Decimal:
                    AppendDot(); e.Handled = true; break;

                case Key.Add:
                case Key.OemPlus:
                case Key.Up:
                    OnIncrement(this, new RoutedEventArgs()); e.Handled = true; break;

                case Key.Subtract:
                case Key.OemMinus:
                case Key.Down:
                    OnDecrement(this, new RoutedEventArgs()); e.Handled = true; break;

                case Key.Back:
                    OnBackspace(this, new RoutedEventArgs()); e.Handled = true; break;

                case Key.Delete:
                    OnClearAll(this, new RoutedEventArgs()); e.Handled = true; break;

                case Key.Return:
                    OnApply(this, new RoutedEventArgs()); e.Handled = true; break;

                case Key.Escape:
                    OnCancel(this, new RoutedEventArgs()); e.Handled = true; break;
            }
        }

        // 숫자/소수점 외 문자 입력 차단
        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            char c = e.Text[0];
            var dec = '.';
            var cultureDot = Convert.ToChar(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            if (char.IsDigit(c))
            {
                AppendDigit(c.ToString());
                e.Handled = true;
            }
            else if (AllowDecimal && (c == '.' || c == dec || c == cultureDot))
            {
                AppendDot();
                e.Handled = true;
            }
            else
            {
                e.Handled = true;
            }
        }

        private static bool TryMapDigitKey(Key key, out char digit)
        {
            digit = '\0';
            if (key >= Key.D0 && key <= Key.D9 && Keyboard.Modifiers == ModifierKeys.None)
            {
                digit = (char)('0' + (key - Key.D0));
                return true;
            }
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                digit = (char)('0' + (key - Key.NumPad0));
                return true;
            }
            return false;
        }

        // ===== 유틸 =====
        private double ClampToRange(double value)
        {
            double min, max;
            var hasMin = TryParseInv(MinimumValue, out min);
            var hasMax = TryParseInv(MaximumValue, out max);
            if (hasMin && value < min) value = min;
            if (hasMax && value > max) value = max;
            return value;
        }

        private static double ParseOrZero(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static bool TryParseInv(string s, out double v)
        {
            if (string.IsNullOrWhiteSpace(s)) { v = 0; return false; }
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        private static string FormatNumber(double d)
        {
            var s = d.ToString("G15", CultureInfo.InvariantCulture);
            if (s.IndexOf('.') >= 0) s = s.TrimEnd('0').TrimEnd('.');
            return s;
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!object.Equals(field, value))
            {
                field = value;
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs(name));
            }
        }

        // ===== 범위 강제 유틸 =====
        private bool IsIntermediateTyping(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            if (s == "-" || s.EndsWith(".")) return true;
            return false;
        }

        private void MoveCaretToEnd()
        {
            if (InputBox != null)
                InputBox.CaretIndex = (InputBox.Text ?? string.Empty).Length;
        }

        private void EnforceRangeIfStable()
        {
            var s = Text ?? "";
            if (IsIntermediateTyping(s)) return;

            double v;
            if (!TryParseInv(s, out v)) return;

            double min, max;
            var hasMin = TryParseInv(MinimumValue, out min);
            var hasMax = TryParseInv(MaximumValue, out max);

            if (hasMin && hasMax && min > max)
            {
                var t = min; min = max; max = t;
            }

            var snapped = v;
            if (hasMin && v < min) snapped = min;
            if (hasMax && v > max) snapped = max;

            if (!snapped.Equals(v))
            {
                Text = FormatNumber(snapped);
                MoveCaretToEnd();
            }
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnforceRangeIfStable();
        }

        private void InputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            double v;
            if (TryParseInv(Text, out v))
                Text = FormatNumber(ClampToRange(v));
        }
    }
}
