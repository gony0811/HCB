using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Matrix = System.Windows.Media.Matrix;
using Point = System.Windows.Point;

namespace HCB.UI
{
    public partial class WaferMapControl : UserControl
    {
        private Point _lastMousePos;
        private Matrix _initialMatrix;

        public static readonly DependencyProperty ItemsSourceProperty =
         DependencyProperty.Register(
             "ItemsSource",
             typeof(List<DieData>),
             typeof(WaferMapControl),
             new PropertyMetadata(null, (d, e) =>
             {
                 var ctrl = d as WaferMapControl;
                 if (ctrl != null)
                 {
                     // 바인딩된 리스트가 교체되거나 업데이트 신호가 오면 다시 그림
                     ctrl.VisualHost.DieList = e.NewValue as List<DieData>;
                     ctrl.UpdateWafer();
                 }
             }));

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WaferMapControl control)
            {
                // 데이터가 새로 바인딩되면 리스트를 업데이트하고 다시 그림
                control.VisualHost.DieList = e.NewValue as List<DieData>;
                control.UpdateWafer();
            }
        }

        public List<DieData> ItemsSource
        {
            get => (List<DieData>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public event EventHandler<DieData> DieClicked;

        private bool _initialCenterDone;
        private DieData _highlightedDie;
        private Brush _highlightBrush;

        public WaferMapControl()
        {
            InitializeComponent();
            Loaded += OnControlLoaded;
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            if (!_initialCenterDone && ItemsSource != null && ItemsSource.Count > 0)
                CenterWafer();
        }

        public void UpdateWafer()
        {
            VisualHost.DieList = ItemsSource;
            VisualHost.RenderWafer();

            if (_highlightedDie != null)
                VisualHost.HighlightDie(_highlightedDie, _highlightBrush);

            if (!_initialCenterDone && IsLoaded && Viewport.ActualWidth > 0)
                CenterWafer();
        }

        public void HighlightDie(DieData die, Brush brush)
        {
            _highlightedDie = die;
            _highlightBrush = brush;
            VisualHost.HighlightDie(die, brush);
        }

        public void CenterWafer()
        {
            if (ItemsSource == null || ItemsSource.Count == 0) return;
            if (Viewport.ActualWidth == 0 || Viewport.ActualHeight == 0) return;

            int maxRow = ItemsSource.Max(d => d.Row);
            int maxCol = ItemsSource.Max(d => d.Col);
            int gridSize = Math.Max(maxRow, maxCol) + 1;
            double totalSize = gridSize * (VisualHost.DieSize + VisualHost.Gap) - VisualHost.Gap;
            double diameter = totalSize + 2 * VisualHost.Gap;

            double scaleX = Viewport.ActualWidth / diameter;
            double scaleY = Viewport.ActualHeight / diameter;
            double scale = Math.Min(scaleX, scaleY) * 0.9;

            double scaledSize = diameter * scale;
            double offsetX = (Viewport.ActualWidth - scaledSize) / 2;
            double offsetY = (Viewport.ActualHeight - scaledSize) / 2;

            var m = new Matrix();
            m.Scale(scale, scale);
            m.Translate(offsetX, offsetY);
            MainTransform.Matrix = m;

            _initialCenterDone = true;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point pos = e.GetPosition(Viewport);
            double scale = e.Delta > 0 ? 1.1 : 1 / 1.1;
            Matrix m = MainTransform.Matrix;
            m.ScaleAt(scale, scale, pos.X, pos.Y);
            MainTransform.Matrix = m;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _lastMousePos = e.GetPosition(Viewport);
                _initialMatrix = MainTransform.Matrix;
                Viewport.CaptureMouse();

                // Hit-Test 수행
                Point canvasPos = e.GetPosition(MainCanvas);
                var selected = VisualHost.GetDieAtPoint(canvasPos);
                if (selected != null) DieClicked?.Invoke(this, selected);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (Viewport.IsMouseCaptured)
            {
                Vector delta = e.GetPosition(Viewport) - _lastMousePos;
                Matrix m = _initialMatrix;
                m.Translate(delta.X, delta.Y);
                MainTransform.Matrix = m;
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e) => Viewport.ReleaseMouseCapture();


    }
}
