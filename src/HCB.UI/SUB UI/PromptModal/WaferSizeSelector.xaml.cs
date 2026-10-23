using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    /// <summary>
    /// Wafer 사이즈(1/4/6/8/12 inch) 선택 창. 확인 시 SelectedInch에 선택값을 담는다.
    /// </summary>
    public partial class WaferSizeSelector : RadWindow
    {
        public int SelectedInch { get; private set; }

        private readonly Dictionary<int, RadButton> _buttons = new();

        private static readonly SolidColorBrush SelectedColor = new(Color.FromRgb(0x0F, 0x6E, 0x56));
        private static readonly SolidColorBrush SelectedBorder = new(Color.FromRgb(0x2E, 0xCC, 0x71));
        private static readonly SolidColorBrush NoneColor = new(Color.FromRgb(0x16, 0x21, 0x3E));
        private static readonly SolidColorBrush NoneBorder = new(Color.FromRgb(0x0F, 0x34, 0x60));

        /// <param name="initialInch">처음 선택되어 있을 사이즈 (0이면 미선택)</param>
        public WaferSizeSelector(int initialInch = 0)
        {
            InitializeComponent();

            _buttons[1] = Size1Btn;
            _buttons[4] = Size4Btn;
            _buttons[6] = Size6Btn;
            _buttons[8] = Size8Btn;
            _buttons[12] = Size12Btn;

            if (_buttons.ContainsKey(initialInch))
                Select(initialInch);
        }

        private void SizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int inch)) return;
            Select(inch);
        }

        private void Select(int inch)
        {
            SelectedInch = inch;
            foreach (var (size, btn) in _buttons)
            {
                bool selected = size == inch;
                btn.Background = selected ? SelectedColor : NoneColor;
                btn.BorderBrush = selected ? SelectedBorder : NoneBorder;
            }
            ConfirmButton.IsEnabled = true;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedInch = 0;
            DialogResult = false;
            Close();
        }
    }
}
