using HCB.IoC;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Scoped)]
    public partial class JogWindow : RadWindow
    {
        private JogWindowVM VM => (JogWindowVM) DataContext;

        // 모드별 색상
        private static readonly SolidColorBrush ContinueFg = new(Color.FromRgb(0xFF, 0x6B, 0x35)); // NeonOrange
        private static readonly SolidColorBrush ContinueBg = new(Color.FromRgb(0x2A, 0x1A, 0x0A));
        private static readonly SolidColorBrush PitchFg = new(Color.FromRgb(0x39, 0xFF, 0x14)); // NeonGreen
        private static readonly SolidColorBrush PitchBg = new(Color.FromRgb(0x0A, 0x2A, 0x0A));

        public JogWindow(JogWindowVM vm)
        {
            InitializeComponent();
            DataContext = vm;
            Closed += (_, _) => vm.Dispose();

            // 모드 변경 → 버튼 색 갱신
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(vm.CurrentMode))
                    ApplyModeStyle();
            };

            ApplyModeStyle(); // 초기 적용
        }

        // ── 모드에 따라 버튼 색 일괄 변경 ──────────────────────
        private void ApplyModeStyle()
        {
            bool isPitch = VM.IsPitchMode;

            var fg = isPitch ? PitchFg : ContinueFg;
            var bg = isPitch ? PitchBg : ContinueBg;

            // 모드 표시 점 — 선택된 쪽만 불투명
            DotContinue.Opacity = isPitch ? 0.25 : 1.0;
            DotPitch.Opacity = isPitch ? 1.0 : 0.25;

            // 수직 조그 버튼 전체 색 변경
            foreach (var btn in GetAllJogButtons())
            {
                btn.Foreground = fg;
                btn.Background = bg;
                btn.BorderBrush = fg;
            }

            // HX 버튼은 cyan 고정 (수평이라 색 구분 불필요)
            // — 필요 시 아래 주석 해제하여 HX도 모드색 적용 가능
            // BtnHxBack.Foreground  = fg; ...
        }

        private IEnumerable<Button> GetAllJogButtons()
        {
            // Tag가 있는 버튼 = 조그 버튼
            return FindVisualChildren<Button>(this)
                   .Where(b => b.Tag is string t && t.Contains('|'));
        }

        // VisualTree 재귀 탐색 헬퍼
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child))
                    yield return sub;
            }
        }

        // ════════════════════════════════════════════════════
        //  이벤트 핸들러 — 모드에 따라 분기
        // ════════════════════════════════════════════════════

        // Continue 모드: 누를 때 Jog 시작
        private void JogBtn_Down(object sender, MouseButtonEventArgs e)
        {
            if (!VM.IsContinueMode) return;
            if (sender is Button btn && btn.Tag is string tag)
            {
                var p = tag.Split('|');
                _ = VM.JogStart(p[0], p[1]);
            }
        }

        // Continue 모드: 뗄 때 Stop
        private void JogBtn_Up(object sender, MouseButtonEventArgs e)
        {
            if (!VM.IsContinueMode) return;
            if (sender is Button btn && btn.Tag is string tag)
                _ = VM.JogStop(tag.Split('|')[0]);
        }

        // Continue 모드: 버튼 밖으로 나갈 때 Stop
        private void JogBtn_Leave(object sender, MouseEventArgs e)
        {
            if (!VM.IsContinueMode) return;
            if (e.LeftButton == MouseButtonState.Pressed &&
                sender is Button btn && btn.Tag is string tag)
                _ = VM.JogStop(tag.Split('|')[0]);
        }

        // Pitch 모드: Click 시 Pitch 이동
        // Tag "DY|Up" → param "DY+"  /  Tag "DY|Down" → param "DY-"
        private void JogBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!VM.IsPitchMode) return;
            if (sender is Button btn && btn.Tag is string tag)
            {
                var p = tag.Split('|');
                var axis = p[0];
                var sign = p[1] is "Up" or "Front" ? "+" : "-";
                _ = VM.PitchMoveCommand.ExecuteAsync(axis + sign);
            }
        }
    }
}