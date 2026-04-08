using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    public partial class VacuumSelector : RadWindow
    {
        public List<int> TopDieVacuums { get; private set; } = new();
        public List<int> BotDieVacuums { get; private set; } = new();

        private readonly HashSet<int> _topAssigned = new();
        private readonly HashSet<int> _botAssigned = new();

        private readonly Dictionary<int, RadButton> _topButtons = new();
        private readonly Dictionary<int, RadButton> _botButtons = new();

        private static readonly SolidColorBrush TopColor = new(Color.FromRgb(0x1E, 0x88, 0xE5));
        private static readonly SolidColorBrush BotColor = new(Color.FromRgb(0xE9, 0x45, 0x60));
        private static readonly SolidColorBrush NoneColor = new(Color.FromRgb(0x16, 0x21, 0x3E));
        private static readonly SolidColorBrush NoneBorder = new(Color.FromRgb(0x0F, 0x34, 0x60));

        public VacuumSelector()
        {
            InitializeComponent();
            BuildButtonMap();
        }

        private void BuildButtonMap()
        {
            _topButtons[1] = TopBtn1; _topButtons[2] = TopBtn2;
            _topButtons[3] = TopBtn3; _topButtons[4] = TopBtn4;

            _botButtons[1] = BotBtn1; _botButtons[2] = BotBtn2;
            _botButtons[3] = BotBtn3; _botButtons[4] = BotBtn4;
        }

        private void TopButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            if (_topAssigned.Contains(num))
                _topAssigned.Remove(num);
            else
                _topAssigned.Add(num);

            RefreshButtonVisual(_topButtons, num, _topAssigned, TopColor);
            UpdateConfirmButton();
        }

        private void BotButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            if (_botAssigned.Contains(num))
                _botAssigned.Remove(num);
            else
                _botAssigned.Add(num);

            RefreshButtonVisual(_botButtons, num, _botAssigned, BotColor);
            UpdateConfirmButton();
        }

        private static void RefreshButtonVisual(
            Dictionary<int, RadButton> map,
            int num,
            HashSet<int> assigned,
            SolidColorBrush activeColor)
        {
            if (!map.TryGetValue(num, out var btn)) return;

            if (assigned.Contains(num))
            {
                btn.Background = activeColor;
                btn.BorderBrush = activeColor;
            }
            else
            {
                btn.Background = NoneColor;
                btn.BorderBrush = NoneBorder;
            }
        }

        private void UpdateConfirmButton()
        {
            ConfirmButton.IsEnabled = _topAssigned.Count > 0 || _botAssigned.Count > 0;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            TopDieVacuums = _topAssigned.OrderBy(x => x).ToList();
            BotDieVacuums = _botAssigned.OrderBy(x => x).ToList();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            TopDieVacuums = new List<int>();
            BotDieVacuums = new List<int>();
            DialogResult = false;
            Close();
        }
    }
}