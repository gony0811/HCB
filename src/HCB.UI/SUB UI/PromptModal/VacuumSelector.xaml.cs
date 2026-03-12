using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    /// <summary>
    /// Vacuum 번호(1~9) 를 TOP DIE / BOTTOM DIE 각각 선택하는 모달창.
    /// </summary>
    public partial class VacuumSelector : RadWindow
    {
        // ── 결과 프로퍼티 ──────────────────────────────────────────
        /// <summary>TOP DIE 로 선택된 Vacuum 번호 (1~9). 미선택 시 null.</summary>
        public int? TopDieVacuum { get; private set; }

        /// <summary>BOTTOM DIE 로 선택된 Vacuum 번호 (1~9). 미선택 시 null.</summary>
        public int? BotDieVacuum { get; private set; }

        // ── 내부 상태 ──────────────────────────────────────────────
        private enum ActiveDie { Top, Bottom }
        private ActiveDie _activeDie = ActiveDie.Top;

        // 버튼 번호 → RadButton 맵
        private Dictionary<int, RadButton> _buttons = new();

        // 각 버튼에 어떤 Die 가 할당되어 있는지 추적
        private int? _topAssigned = null;   // TOP DIE 가 선택한 버튼 번호
        private int? _botAssigned = null;   // BOT DIE 가 선택한 버튼 번호

        // 색상 상수
        private static readonly SolidColorBrush TopColor = new(Color.FromRgb(0x1E, 0x88, 0xE5)); // #1E88E5 파랑
        private static readonly SolidColorBrush BotColor = new(Color.FromRgb(0xE9, 0x45, 0x60)); // #E94560 빨강
        private static readonly SolidColorBrush BothColor = new(Color.FromRgb(0x53, 0x34, 0x83)); // #533483 보라 (둘 다)
        private static readonly SolidColorBrush NoneColor = new(Color.FromRgb(0x16, 0x21, 0x3E)); // #16213E 기본
        private static readonly SolidColorBrush NoneBorder = new(Color.FromRgb(0x0F, 0x34, 0x60)); // #0F3460 기본 테두리

        // ── 생성자 ─────────────────────────────────────────────────
        public VacuumSelector()
        {
            InitializeComponent();
            BuildButtonMap();
            SetActiveTab(ActiveDie.Top);
        }

        /// <summary>Name → 버튼 딕셔너리 구성</summary>
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

            // TOP 탭 활성/비활성 스타일
            TopDieTab.Background = die == ActiveDie.Top ? new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E)) : Brushes.Transparent;
            TopDieTab.BorderBrush = die == ActiveDie.Top ? TopColor : Brushes.Transparent;
            TopDieTab.Foreground = die == ActiveDie.Top ? TopColor : new SolidColorBrush(Color.FromRgb(0x55, 0x66, 0x88));

            // BOTTOM 탭 활성/비활성 스타일
            BotDieTab.Background = die == ActiveDie.Bottom ? new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x3E)) : Brushes.Transparent;
            BotDieTab.BorderBrush = die == ActiveDie.Bottom ? BotColor : Brushes.Transparent;
            BotDieTab.Foreground = die == ActiveDie.Bottom ? BotColor : new SolidColorBrush(Color.FromRgb(0x55, 0x66, 0x88));
        }

        // ── Vacuum 버튼 클릭 ───────────────────────────────────────
        private void VacuumButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadButton btn) return;
            if (!int.TryParse(btn.Tag?.ToString(), out int num)) return;

            if (_activeDie == ActiveDie.Top)
            {
                if (_topAssigned.HasValue && _topAssigned.Value != num)
                {
                    int prev = _topAssigned.Value;
                    _topAssigned = null;
                    RefreshButtonVisual(prev);
                }

                if (_botAssigned == num)
                {
                    int prev = _botAssigned.Value;
                    _botAssigned = null;
                    BotDieVacuum = null;
                    BotDieValueText.Text = "—";
                    RefreshButtonVisual(prev);
                }

                _topAssigned = num;  // ← ②
                TopDieVacuum = num;
                TopDieValueText.Text = num.ToString();
            }
            else
            {
                if (_botAssigned.HasValue && _botAssigned.Value != num)
                {
                    int prev = _botAssigned.Value;
                    _botAssigned = null;        // ← ③ 먼저 해제
                    RefreshButtonVisual(prev);
                }

                if (_topAssigned == num)
                {
                    int prev = _topAssigned.Value;
                    _topAssigned = null;
                    TopDieVacuum = null;
                    TopDieValueText.Text = "—";
                    RefreshButtonVisual(prev);
                }

                _botAssigned = num;
                BotDieVacuum = num;
                BotDieValueText.Text = num.ToString();
            }

            RefreshButtonVisual(num);
            UpdateConfirmButton();
        }

        /// <summary>버튼 배경/테두리 색을 현재 할당 상태에 맞게 갱신</summary>
        private void RefreshButtonVisual(int num)
        {
            if (!_buttons.TryGetValue(num, out var btn)) return;

            bool isTop = (_topAssigned == num);
            bool isBot = (_botAssigned == num);

            if (isTop && isBot)
            {
                btn.Background = BothColor;
                btn.BorderBrush = BotColor;
            }
            else if (isTop)
            {
                btn.Background = TopColor;
                btn.BorderBrush = TopColor;
            }
            else if (isBot)
            {
                btn.Background = BotColor;
                btn.BorderBrush = BotColor;
            }
            else
            {
                btn.Background = NoneColor;
                btn.BorderBrush = NoneBorder;
            }
        }

        /// <summary>TOP / BOT 둘 다 선택돼야 확인 버튼 활성화</summary>
        private void UpdateConfirmButton()
        {
            ConfirmButton.IsEnabled = _topAssigned.HasValue && _botAssigned.HasValue;
        }

        // ── 확인 / 취소 ────────────────────────────────────────────
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            TopDieVacuum = null;
            BotDieVacuum = null;
            DialogResult = false;
            Close();
        }
    }
}