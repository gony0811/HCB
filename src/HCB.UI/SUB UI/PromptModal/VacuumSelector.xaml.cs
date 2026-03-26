using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    /// <summary>
    /// Vacuum 번호(1~9) 를 TOP DIE / BOTTOM DIE 각각 다중 선택하는 모달창.
    /// </summary>
    public partial class VacuumSelector : RadWindow
    {
        // ── 결과 프로퍼티 ──────────────────────────────────────────
        /// <summary>TOP DIE 로 선택된 Vacuum 번호 목록 (1~9).</summary>
        public List<int> TopDieVacuums { get; private set; } = new();

        /// <summary>BOTTOM DIE 로 선택된 Vacuum 번호 목록 (1~9).</summary>
        public List<int> BotDieVacuums { get; private set; } = new();

        // ── 내부 상태 ──────────────────────────────────────────────
        private enum ActiveDie { Top, Bottom }
        private ActiveDie _activeDie = ActiveDie.Top;

        private readonly HashSet<int> _topAssigned = new();
        private readonly HashSet<int> _botAssigned = new();

        // 버튼 번호 → RadButton 맵
        private readonly Dictionary<int, RadButton> _buttons = new();

        // 색상 상수
        private static readonly SolidColorBrush TopColor    = new(Color.FromRgb(0x1E, 0x88, 0xE5)); // #1E88E5 파랑
        private static readonly SolidColorBrush BotColor    = new(Color.FromRgb(0xE9, 0x45, 0x60)); // #E94560 빨강
        private static readonly SolidColorBrush BothColor   = new(Color.FromRgb(0x53, 0x34, 0x83)); // #533483 보라
        private static readonly SolidColorBrush NoneColor   = new(Color.FromRgb(0x16, 0x21, 0x3E)); // #16213E 기본
        private static readonly SolidColorBrush NoneBorder  = new(Color.FromRgb(0x0F, 0x34, 0x60)); // #0F3460 기본 테두리

        // 그리드 모드
        private enum GridMode { Mode3x3, Mode2x4 }
        private GridMode _gridMode = GridMode.Mode3x3;

        // ── 생성자 ─────────────────────────────────────────────────
        public VacuumSelector()
        {
            InitializeComponent();
            BuildButtonMap();
            SetActiveTab(ActiveDie.Top);
        }

        private void BuildButtonMap()
        {
            _buttons[1] = Btn1; _buttons[2] = Btn2; _buttons[3] = Btn3;
            _buttons[4] = Btn4; _buttons[5] = Btn5; _buttons[6] = Btn6;
            _buttons[7] = Btn7; _buttons[8] = Btn8; _buttons[9] = Btn9;
        }

        // ── 탭 전환 ────────────────────────────────────────────────
        private void TopDieTab_Click(object sender, RoutedEventArgs e) => SetActiveTab(ActiveDie.Top);
        private void BotDieTab_Click(object sender, RoutedEventArgs e) => SetActiveTab(ActiveDie.Bottom);

        private void SetActiveTab(ActiveDie die)
        {
            _activeDie = die;

            TopDieTab.Background  = die == ActiveDie.Top    ? new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E)) : Brushes.Transparent;
            TopDieTab.BorderBrush = die == ActiveDie.Top    ? TopColor : Brushes.Transparent;
            TopDieTab.Foreground  = die == ActiveDie.Top    ? TopColor : new SolidColorBrush(Color.FromRgb(0x55, 0x66, 0x88));

            BotDieTab.Background  = die == ActiveDie.Bottom ? new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E)) : Brushes.Transparent;
            BotDieTab.BorderBrush = die == ActiveDie.Bottom ? BotColor : Brushes.Transparent;
            BotDieTab.Foreground  = die == ActiveDie.Bottom ? BotColor : new SolidColorBrush(Color.FromRgb(0x55, 0x66, 0x88));
        }

        // ── 그리드 모드 전환 ───────────────────────────────────────
        private void Mode3x3_Click(object sender, RoutedEventArgs e) => SetGridMode(GridMode.Mode3x3);
        private void Mode4x2_Click(object sender, RoutedEventArgs e) => SetGridMode(GridMode.Mode2x4);

        private void SetGridMode(GridMode mode)
        {
            _gridMode = mode;

            if (mode == GridMode.Mode3x3)
            {
                VacuumGrid.Rows    = 3;
                VacuumGrid.Columns = 3;
                Btn9.Visibility    = Visibility.Visible;

                Mode3x3Btn.Background  = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
                Mode3x3Btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
                Mode3x3Btn.Foreground  = Brushes.White;

                Mode4x2Btn.Background  = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E));
                Mode4x2Btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x44, 0x66));
                Mode4x2Btn.Foreground  = new SolidColorBrush(Color.FromRgb(0x55, 0x66, 0x88));
            }
            else // 2×4 (2행 4열)
            {
                VacuumGrid.Rows    = 4;
                VacuumGrid.Columns = 2;
                Btn9.Visibility    = Visibility.Collapsed;

                // 9번이 선택되어 있었다면 해제
                if (_topAssigned.Remove(9) | _botAssigned.Remove(9))
                {
                    RefreshSelectionDisplay();
                    UpdateConfirmButton();
                }

                Mode4x2Btn.Background  = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
                Mode4x2Btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
                Mode4x2Btn.Foreground  = Brushes.White;

                Mode3x3Btn.Background  = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E));
                Mode3x3Btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x44, 0x66));
                Mode3x3Btn.Foreground  = new SolidColorBrush(Color.FromRgb(0x55, 0x66, 0x88));
            }
        }

        // ── Vacuum 버튼 클릭 (다중 선택 토글) ─────────────────────
        private void VacuumButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            if (_activeDie == ActiveDie.Top)
            {
                if (_topAssigned.Contains(num))
                {
                    // 이미 TOP에 선택됨 → 해제
                    _topAssigned.Remove(num);
                }
                else
                {
                    // BOT에 있으면 제거 후 TOP에 추가
                    _botAssigned.Remove(num);
                    _topAssigned.Add(num);
                }
            }
            else
            {
                if (_botAssigned.Contains(num))
                {
                    // 이미 BOT에 선택됨 → 해제
                    _botAssigned.Remove(num);
                }
                else
                {
                    // TOP에 있으면 제거 후 BOT에 추가
                    _topAssigned.Remove(num);
                    _botAssigned.Add(num);
                }
            }

            RefreshButtonVisual(num);
            RefreshSelectionDisplay();
            UpdateConfirmButton();
        }

        /// <summary>버튼 배경/테두리 색 및 배지를 현재 할당 상태에 맞게 갱신</summary>
        private void RefreshButtonVisual(int num)
        {
            if (!_buttons.TryGetValue(num, out var btn)) return;

            bool isTop = _topAssigned.Contains(num);
            bool isBot = _botAssigned.Contains(num);

            if (isTop && isBot)
            {
                btn.Background  = BothColor;
                btn.BorderBrush = BotColor;
            }
            else if (isTop)
            {
                btn.Background  = TopColor;
                btn.BorderBrush = TopColor;
            }
            else if (isBot)
            {
                btn.Background  = BotColor;
                btn.BorderBrush = BotColor;
            }
            else
            {
                btn.Background  = NoneColor;
                btn.BorderBrush = NoneBorder;
            }

            // 템플릿 파트 배지 표시
            btn.ApplyTemplate();
            if (btn.Template.FindName("BadgePanel", btn) is StackPanel badgePanel)
                badgePanel.Visibility = (isTop || isBot) ? Visibility.Visible : Visibility.Collapsed;
            if (btn.Template.FindName("TopBadge", btn) is Border topBadge)
                topBadge.Visibility = isTop ? Visibility.Visible : Visibility.Collapsed;
            if (btn.Template.FindName("BotBadge", btn) is Border botBadge)
                botBadge.Visibility = isBot ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>선택 현황 WrapPanel(뱃지 목록)을 갱신</summary>
        private void RefreshSelectionDisplay()
        {
            TopSelectionPanel.Children.Clear();
            foreach (var n in _topAssigned.OrderBy(x => x))
                TopSelectionPanel.Children.Add(CreateBadge(n, TopColor));

            BotSelectionPanel.Children.Clear();
            foreach (var n in _botAssigned.OrderBy(x => x))
                BotSelectionPanel.Children.Add(CreateBadge(n, BotColor));
        }

        private static Border CreateBadge(int num, SolidColorBrush color)
        {
            return new Border
            {
                Background    = color,
                CornerRadius  = new CornerRadius(6),
                Padding       = new Thickness(8, 3, 8, 3),
                Margin        = new Thickness(3, 2, 3, 2),
                Child         = new TextBlock
                {
                    Text       = num.ToString(),
                    FontSize   = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };
        }

        /// <summary>TOP 또는 BOT 중 하나라도 선택되면 확인 버튼 활성화</summary>
        private void UpdateConfirmButton()
        {
            ConfirmButton.IsEnabled = _topAssigned.Count > 0 || _botAssigned.Count > 0;
        }

        // ── 확인 / 취소 ────────────────────────────────────────────
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            TopDieVacuums = _topAssigned.OrderBy(x => x).ToList();
            BotDieVacuums = _botAssigned.OrderBy(x => x).ToList();
            DialogResult  = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            TopDieVacuums = new List<int>();
            BotDieVacuums = new List<int>();
            DialogResult  = false;
            Close();
        }
    }
}
