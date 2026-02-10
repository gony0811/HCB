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
        public List<DieData> DieList { get; set; }
        public double DieSize { get; set; } = 10;
        public double Gap { get; set; } = 0.5;

        public WaferVisualHost() => _children = new VisualCollection(this);

        public void RenderWafer()
        {
            _children.Clear();
            if (DieList == null || DieList.Count == 0) return;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                foreach (var die in DieList)
                {
                    // Gap을 고려한 사각형 영역 계산
                    Rect rect = new Rect(
                        die.Col * (DieSize + Gap),
                        die.Row * (DieSize + Gap),
                        DieSize,
                        DieSize);

                    dc.DrawRectangle(die.DieBrush, null, rect);
                }
            }
            _children.Add(visual);
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
