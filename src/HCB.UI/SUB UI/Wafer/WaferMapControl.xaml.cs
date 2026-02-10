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

        public WaferMapControl() => InitializeComponent();

        public void UpdateWafer()
        {
            VisualHost.DieList = ItemsSource;
            VisualHost.RenderWafer();
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
