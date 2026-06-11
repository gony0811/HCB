using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;


namespace HCB.UI
{
    public partial class VernierResult : ObservableObject
    {
        public ObservableCollection<Point2D> v1 = new ObservableCollection<Point2D>();
        public ObservableCollection<Point2D> v3 = new ObservableCollection<Point2D>();

        public double? OffsetX { get; set; }
        public double? OffsetY { get; set; }
        public double? OffsetT { get; set; }

        public void Preprocess(double distX, double distY)
        {
            if (v1.Count < 2 || v3.Count < 2) return;

            double p1X = PickSmallestNonZero(v1[0].X / 1000, v3[0].X / 1000);
            double p1Y = PickSmallestNonZero(v1[0].Y / 1000, v3[0].Y / 1000);
            double p3X = PickSmallestNonZero(v1[1].X / 1000, v3[1].X / 1000);
            double p3Y = PickSmallestNonZero(v1[1].Y / 1000, v3[1].Y / 1000);

            // 실제 위치
            double a1X = p1X, a1Y = p1Y;
            double a3X = distX + p3X, a3Y = distY + p3Y;

            // 회전 보정량
            double idealAngle = Math.Atan2(distY, distX);
            double actualAngle = Math.Atan2(a3Y - a1Y, a3X - a1X);
            double offsetT = (actualAngle - idealAngle) * (180.0 / Math.PI);

            // 회전 보정 적용 후 남는 평행이동 오차
            double rad = -offsetT * Math.PI / 180.0;
            double cosR = Math.Cos(rad);
            double sinR = Math.Sin(rad);

            double r1X = a1X * cosR - a1Y * sinR;
            double r1Y = a1X * sinR + a1Y * cosR;

            double r3X = a3X * cosR - a3Y * sinR;
            double r3Y = a3X * sinR + a3Y * cosR;

            OffsetX = ((r1X - 0.0) + (r3X - distX)) / 2.0;
            OffsetY = ((r1Y - 0.0) + (r3Y - distY)) / 2.0;
            OffsetT = offsetT;
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
