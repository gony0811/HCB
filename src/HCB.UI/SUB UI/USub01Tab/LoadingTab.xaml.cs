using HCB.IoC;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Scoped)]
    public partial class LoadingTab : UserControl
    {
        private static readonly SolidColorBrush TopColor = new(Color.FromRgb(0x1E, 0x88, 0xE5));
        private static readonly SolidColorBrush BtmColor = new(Color.FromRgb(0xE9, 0x45, 0x60));
        private static readonly SolidColorBrush NoneColor = new(Color.FromRgb(0x16, 0x21, 0x3E));
        private static readonly SolidColorBrush NoneBorder = new(Color.FromRgb(0x0F, 0x34, 0x60));

        // D-Table 토글 상태
        private readonly HashSet<int> _dTopActive = new();
        private readonly HashSet<int> _dBtmActive = new();

        // W-Table 토글 상태
        private readonly HashSet<int> _wTopActive = new();
        private readonly HashSet<int> _wBtmActive = new();

        public LoadingTab(TableManagerViewModel tableManagerViewModel)
        {
            InitializeComponent();
            DataContext = tableManagerViewModel;
        }

        private TableManagerViewModel VM => (TableManagerViewModel)DataContext;

        // ── 탭 전환 ──
        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (TabDTable == null) return;

            var rb = sender as RadioButton;
            string header = rb?.Content?.ToString() ?? "";

            TabDTable.Visibility = Visibility.Collapsed;
            TabWTable.Visibility = Visibility.Collapsed;

            switch (header)
            {
                case "D-TABLE": TabDTable.Visibility = Visibility.Visible; break;
                case "W-TABLE": TabWTable.Visibility = Visibility.Visible; break;
            }
        }

        // ── D-Table Top 버튼 ──
        private void DTopButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            Toggle(_dTopActive, num, btn, TopColor);
            var onOff = _dTopActive.Contains(num) ? eOnOff.On : eOnOff.Off;
            _ = VM.TopDieVacToggle(num, onOff);
        }

        // ── D-Table Btm 버튼 ──
        private void DBtmButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            Toggle(_dBtmActive, num, btn, BtmColor);
            var onOff = _dBtmActive.Contains(num) ? eOnOff.On : eOnOff.Off;
            _ = VM.BtmDieVacToggle(num, onOff);
        }

        // ── W-Table Top 버튼 ──
        private void WTopButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            Toggle(_wTopActive, num, btn, TopColor);
            var onOff = _wTopActive.Contains(num) ? eOnOff.On : eOnOff.Off;
            // TODO: W-Table Top vacuum command
        }

        // ── W-Table Btm 버튼 ──
        private void WBtmButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            Toggle(_wBtmActive, num, btn, BtmColor);
            var onOff = _wBtmActive.Contains(num) ? eOnOff.On : eOnOff.Off;
            // TODO: W-Table Btm vacuum command
        }

        // ── 공통: 토글 상태 + 색상 반영 (VacuumSelector 방식) ──
        private static void Toggle(HashSet<int> activeSet, int num, RadButton btn, SolidColorBrush activeColor)
        {
            if (activeSet.Contains(num))
            {
                activeSet.Remove(num);
                btn.Background = NoneColor;
                btn.BorderBrush = NoneBorder;
            }
            else
            {
                activeSet.Add(num);
                btn.Background = activeColor;
                btn.BorderBrush = activeColor;
            }
        }
    }
}
