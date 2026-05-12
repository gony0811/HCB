using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;


namespace HCB.UI
{
    public partial class VernierResult : ObservableObject
    {
        public ObservableCollection<Point2D> v1 = new ObservableCollection<Point2D>();
        public ObservableCollection<Point2D> v3 = new ObservableCollection<Point2D>();
    }

    public class VernierRow
    {
        public string Name { get; set; }
        public double? V1X { get; set; }
        public double? V1Y { get; set; }
        public double? V3X { get; set; }
        public double? V3Y { get; set; }
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