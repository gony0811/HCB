using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace HCB.UI
{
    public class WaferVisualHost : FrameworkElement
    {
        private readonly VisualCollection _children;
        private DrawingVisual _selectionVisual;

        public List<DieData> DieList { get; set; }
        public double DieSize { get; set; } = 10;
        public double Gap { get; set; } = 0.5;

        private static readonly SolidColorBrush WaferBg;
        private static readonly Pen WaferOutline;

        static WaferVisualHost()
        {
            WaferBg = new SolidColorBrush(Color.FromRgb(0x0A, 0x16, 0x28));
            WaferBg.Freeze();
            var outlineBrush = new SolidColorBrush(Color.FromRgb(0x1C, 0x3A, 0x52));
            outlineBrush.Freeze();
            WaferOutline = new Pen(outlineBrush, 1.5);
            WaferOutline.Freeze();
        }

        public WaferVisualHost() => _children = new VisualCollection(this);

        public void RenderWafer()
        {
            _children.Clear();
            _selectionVisual = null;
            if (DieList == null || DieList.Count == 0) return;

            var baseVisual = new DrawingVisual();
            using (DrawingContext dc = baseVisual.RenderOpen())
            {
                int maxRow = DieList.Max(d => d.Row);
                int maxCol = DieList.Max(d => d.Col);
                int gridSize = Math.Max(maxRow, maxCol) + 1;

                double totalSize = gridSize * (DieSize + Gap) - Gap;
                var center = new Point(totalSize / 2, totalSize / 2);
                double radius = totalSize / 2 + Gap;

                dc.DrawEllipse(WaferBg, WaferOutline, center, radius, radius);

                foreach (var die in DieList)
                {
                    Rect rect = new Rect(
                        die.Col * (DieSize + Gap),
                        die.Row * (DieSize + Gap),
                        DieSize,
                        DieSize);

                    dc.DrawRectangle(die.DieBrush, null, rect);
                }
            }
            _children.Add(baseVisual);

            _selectionVisual = new DrawingVisual();
            _children.Add(_selectionVisual);
        }

        public void HighlightDie(DieData die, Brush brush)
        {
            if (_selectionVisual == null) return;

            _children.Remove(_selectionVisual);
            _selectionVisual = new DrawingVisual();

            if (die != null)
            {
                using (DrawingContext dc = _selectionVisual.RenderOpen())
                {
                    Rect rect = new Rect(
                        die.Col * (DieSize + Gap),
                        die.Row * (DieSize + Gap),
                        DieSize,
                        DieSize);
                    dc.DrawRectangle(brush, null, rect);
                }
            }

            _children.Add(_selectionVisual);
        }

        public DieData GetDieAtPoint(Point p)
        {
            if (DieList == null) return null;
            int col = (int)(p.X / (DieSize + Gap));
            int row = (int)(p.Y / (DieSize + Gap));
            return DieList.Find(d => d.Row == row && d.Col == col);
        }

        protected override int VisualChildrenCount => _children.Count;
        protected override Visual GetVisualChild(int index) => _children[index];
    }
}
