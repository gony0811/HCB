

using System.Windows.Media;

namespace HCB.UI
{
    public class DieData
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public Brush DieBrush { get; set; }
        public string Information { get; set; } // 추가 정보 (Bin No, Yield 등)
    }
}
