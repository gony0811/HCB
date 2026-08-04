

using System.Windows.Media;

namespace HCB.UI
{
    public class DieData
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public Brush DieBrush { get; set; }
        public string Information { get; set; }
        // 저배율 카메라 센터 기준 Die 위치
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        // 고배율 카메라 센터 기준 Die 위치 (= 저배 위치 + ShankLowOffset + HcCenterError)
        public double HighPositionX { get; set; }
        public double HighPositionY { get; set; }
    }
}
