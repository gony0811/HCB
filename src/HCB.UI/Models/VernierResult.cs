using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;


namespace HCB.UI
{
    public partial class VernierResult : ObservableObject
    {
        public ObservableCollection<Point2D> v1 = new ObservableCollection<Point2D>();
        public ObservableCollection<Point2D> v3 = new ObservableCollection<Point2D>();

        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetT { get; set; }

        public void Preprocess(double distX, double distY)
        {
            if (v1.Count < 2 || v3.Count < 2) return;

            double p1X = PickSmallestNonZero(v1[0].X, v3[0].X);
            double p1Y = PickSmallestNonZero(v1[0].Y, v3[0].Y);
            double p3X = PickSmallestNonZero(v1[1].X, v3[1].X);
            double p3Y = PickSmallestNonZero(v1[1].Y, v3[1].Y);

            OffsetX = (p1X + p3X) / 2.0;
            OffsetY = (p1Y + p3Y) / 2.0;

            double dx = p3X - p1X;
            double dy = p3Y - p1Y;
            double dist = Math.Sqrt(distX * distX + distY * distY);
            OffsetT = dist > 0 ? Math.Atan2(dy, dx) * (180.0 / Math.PI) : 0;
        }

        private static double PickSmallestNonZero(double a, double b)
        {
            bool aZero = a == 0;
            bool bZero = b == 0;
            if (aZero && bZero) return 0;
            if (aZero) return b;
            if (bZero) return a;
            return Math.Abs(a) <= Math.Abs(b) ? a : b;
        }
    }

    public class VernierRow
    {
        public string  Name { get; set; }
        public double? V1X  { get; set; }
        public double? V1Y  { get; set; }
        public double? V3X  { get; set; }
        public double? V3Y  { get; set; }
    }

    public class VernierPoint
    {
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public DirectType Dir1 { get; set; }
        public DirectType Dir2 { get; set; }

        public VernierPoint(string name, double x, double y, DirectType dir1, DirectType dir2)
        {
            Name = name;
            X = x;
            Y = y;
            Dir1 = dir1;
            Dir2 = dir2;
        }
    }
}
