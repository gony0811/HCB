using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HCB.UI
{
    /// <summary>
    /// 구역별 파라미터 편집 표. DataContext는 <see cref="ParamEditor"/>.
    /// 값 칸에 바로 입력 → Enter로 다음 행 이동, Esc로 되돌리기 → [변경 저장]으로 일괄 저장.
    /// 페이지의 명령(생성/상세 수정/삭제/저장)은 아래 Command 속성으로 받아 버튼 클릭 시 직접 실행한다.
    /// </summary>
    public partial class ParamEditorControl : UserControl
    {
        public ParamEditorControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty SaveCommandProperty =
            DependencyProperty.Register(nameof(SaveCommand), typeof(ICommand), typeof(ParamEditorControl));
        public ICommand SaveCommand
        {
            get => (ICommand)GetValue(SaveCommandProperty);
            set => SetValue(SaveCommandProperty, value);
        }

        public static readonly DependencyProperty CreateCommandProperty =
            DependencyProperty.Register(nameof(CreateCommand), typeof(ICommand), typeof(ParamEditorControl));
        public ICommand CreateCommand
        {
            get => (ICommand)GetValue(CreateCommandProperty);
            set => SetValue(CreateCommandProperty, value);
        }

        /// <summary>선택 행 상세 수정 (행의 ✎ 버튼 / 더블클릭 / 하단 버튼)</summary>
        public static readonly DependencyProperty DetailCommandProperty =
            DependencyProperty.Register(nameof(DetailCommand), typeof(ICommand), typeof(ParamEditorControl));
        public ICommand DetailCommand
        {
            get => (ICommand)GetValue(DetailCommandProperty);
            set => SetValue(DetailCommandProperty, value);
        }

        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(ParamEditorControl));
        public ICommand DeleteCommand
        {
            get => (ICommand)GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }

        /// <summary>누락 경고의 [필수 항목 추가] (지정 시에만 버튼 표시)</summary>
        public static readonly DependencyProperty AddMissingCommandProperty =
            DependencyProperty.Register(nameof(AddMissingCommand), typeof(ICommand), typeof(ParamEditorControl),
                new PropertyMetadata(null, (d, e) =>
                    ((ParamEditorControl)d).AddMissingButton.Visibility = e.NewValue != null ? Visibility.Visible : Visibility.Collapsed));
        public ICommand AddMissingCommand
        {
            get => (ICommand)GetValue(AddMissingCommandProperty);
            set => SetValue(AddMissingCommandProperty, value);
        }

        private ParamEditor Editor => DataContext as ParamEditor;

        private static void Run(ICommand command)
        {
            if (command?.CanExecute(null) == true) command.Execute(null);
        }

        private void OpenDetail(ParamRow row)
        {
            if (row == null || Editor == null) return;
            Editor.SelectedRow = row;
            Run(DetailCommand);
        }

        // ── 하단 버튼 ──
        private void CreateButton_Click(object sender, RoutedEventArgs e) => Run(CreateCommand);
        private void DetailSelectedButton_Click(object sender, RoutedEventArgs e) => Run(DetailCommand);
        private void DeleteButton_Click(object sender, RoutedEventArgs e) => Run(DeleteCommand);
        private void SaveButton_Click(object sender, RoutedEventArgs e) => Run(SaveCommand);
        private void AddMissingButton_Click(object sender, RoutedEventArgs e) => Run(AddMissingCommand);
        private void RevertAllButton_Click(object sender, RoutedEventArgs e) => Run(Editor?.RevertAllCommand);

        // ── 행 버튼 ──
        private void DetailButton_Click(object sender, RoutedEventArgs e)
        {
            OpenDetail((sender as FrameworkElement)?.DataContext as ParamRow);
            e.Handled = true;
        }

        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            ((sender as FrameworkElement)?.DataContext as ParamRow)?.Revert();
            e.Handled = true;
        }

        private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 값 입력칸/버튼 더블클릭은 제외
            for (var d = e.OriginalSource as DependencyObject; d != null && d != sender; d = GetParent(d))
            {
                if (d is TextBox || d is System.Windows.Controls.Primitives.ButtonBase) return;
                if (d is ListBoxItem item) { OpenDetail(item.DataContext as ParamRow); e.Handled = true; return; }
            }
        }

        // ── 목록 마우스 드래그 스크롤 (값 입력칸/버튼/스크롤바 위에서는 동작하지 않음) ──
        private const double DragThreshold = 6;
        private ScrollViewer _dragScroll;
        private Point _dragStart;
        private double _dragStartOffset;
        private bool _dragPending, _dragging;

        private ScrollViewer ListScroll =>
            ParamList.Template?.FindName("PART_ListScroll", ParamList) as ScrollViewer;

        private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            for (var d = e.OriginalSource as DependencyObject; d != null && d != sender; d = GetParent(d))
            {
                if (d is TextBox || d is System.Windows.Controls.Primitives.ButtonBase
                    || d is System.Windows.Controls.Primitives.ScrollBar) return;
            }

            _dragScroll = ListScroll;
            if (_dragScroll == null) return;
            _dragStart = e.GetPosition(ParamList);
            _dragStartOffset = _dragScroll.VerticalOffset;
            _dragPending = true;
            _dragging = false;
        }

        private void List_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragPending || e.LeftButton != MouseButtonState.Pressed) { EndDrag(); return; }

            double dy = e.GetPosition(ParamList).Y - _dragStart.Y;
            if (!_dragging)
            {
                if (System.Math.Abs(dy) < DragThreshold) return;
                _dragging = true;
                ParamList.CaptureMouse();
                Mouse.OverrideCursor = Cursors.ScrollNS;
            }

            _dragScroll.ScrollToVerticalOffset(_dragStartOffset - dy);
            e.Handled = true;
        }

        private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragging) e.Handled = true;   // 드래그 후에는 클릭(선택 변경)으로 처리하지 않음
            EndDrag();
        }

        private void List_LostMouseCapture(object sender, MouseEventArgs e) => EndDrag();

        private void EndDrag()
        {
            bool wasDragging = _dragging;
            _dragPending = false;
            _dragging = false;
            if (wasDragging)
            {
                Mouse.OverrideCursor = null;
                if (ParamList.IsMouseCaptured) ParamList.ReleaseMouseCapture();
            }
        }

        private static DependencyObject GetParent(DependencyObject d)
            => d is Visual || d is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(d)
                : LogicalTreeHelper.GetParent(d);

        // ── 값 입력칸 ──
        private void ValueBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (tb.DataContext is ParamRow row && Editor != null)
                    Editor.SelectedRow = row;
                tb.SelectAll();
            }
        }

        private void ValueBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb) return;

            if (e.Key == Key.Enter)
            {
                tb.MoveFocus(new TraversalRequest(
                    Keyboard.Modifiers == ModifierKeys.Shift ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && tb.DataContext is ParamRow row)
            {
                row.Revert();
                tb.SelectAll();
                e.Handled = true;
            }
        }
    }
}
