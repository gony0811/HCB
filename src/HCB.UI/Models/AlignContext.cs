// AlignContext.cs
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    /// <summary>
    /// TopHighAlign ~ BtmHighAlign ~ TopPlace 전 구간에서
    /// 공유되는 중간 계산 결과를 하나로 묶은 DTO.
    /// </summary>
    public class AlignContext
    {
        // ── Top (Pc) Vision 결과 ──────────────────────────────
        public VisionMarkResult TopRightFid { get; set; }
        public VisionMarkResult TopRightAlign { get; set; }
        public VisionMarkResult TopLeftFid { get; set; }
        public VisionMarkResult TopLeftAlign { get; set; }

        // ── Btm (Hc) Vision 결과 ─────────────────────────────
        public VisionMarkResult BtmRightFid { get; set; }
        public VisionMarkResult BtmRightAlign { get; set; }
        public VisionMarkResult BtmLeftFid { get; set; }
        public VisionMarkResult BtmLeftAlign { get; set; }

        // ── Offset 계산 결과 ──────────────────────────────────
        public double TopOffsetX { get; set; }
        public double TopOffsetY { get; set; }
        public double TopOffsetT { get; set; }

        public double TopAlignRelOffsetX { get; set; }
        public double TopAlignRelOffsetY { get; set; }
        public double TopAlignRelOffsetT { get; set; }

        public double BtmOffsetX { get; set; }
        public double BtmOffsetY { get; set; }
        public double BtmOffsetT { get; set; }

        // ── HcRO 좌표계 변환 결과 ─────────────────────────────
        public Point2D HcroLF { get; set; }
        public Point2D HcroLA { get; set; }
        public Point2D HcroRF { get; set; }
        public Point2D HcroRA { get; set; }

        public Point2D HcroTopLF { get; set; }
        public Point2D HcroTopRF { get; set; }
        public Point2D HcroTopLA { get; set; }
        public Point2D HcroTopRA { get; set; }

        // ── 캘리브레이션 파라미터 캐시 ───────────────────────
        public bool HasHcRO { get; set; }
        public bool HasPcT { get; set; }
        public double Hc1Rad { get; set; }
        public double Hc2Rad { get; set; }
        public double PcTRad { get; set; }
        public Point2D Hcro { get; set; }
        public Point2D Hc1Offset { get; set; }
        public Point2D Hc2Offset { get; set; }
    }
}