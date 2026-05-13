// AlignContext.cs
using Telerik.Windows.Persistence.Core;
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
        // ── Raw (비전 원본, 절대 수정 금지) ──────────────
        public VisionMarkResult TopRightFidRaw { get; set; }
        public VisionMarkResult TopRightAlignRaw { get; set; }
        public VisionMarkResult TopLeftFidRaw { get; set; }
        public VisionMarkResult TopLeftAlignRaw { get; set; }

        public VisionMarkResult BtmRightFidRaw { get; set; }
        public VisionMarkResult BtmRightAlignRaw { get; set; }
        public VisionMarkResult BtmLeftFidRaw { get; set; }
        public VisionMarkResult BtmLeftAlignRaw { get; set; }

        // ── Corrected (보정 후) ──────────────────────────
        public VisionMarkResult TopRightFidCorrected { get; set; }
        public VisionMarkResult TopRightAlignCorrected { get; set; }
        public VisionMarkResult TopLeftFidCorrected { get; set; }
        public VisionMarkResult TopLeftAlignCorrected { get; set; }

        public VisionMarkResult BtmRightFidCorrected { get; set; }
        public VisionMarkResult BtmRightAlignCorrected { get; set; }
        public VisionMarkResult BtmLeftFidCorrected { get; set; }
        public VisionMarkResult BtmLeftAlignCorrected { get; set; }

        // ── 하위 호환 (기존 UI 바인딩·CSV 코드 변경 불필요) ──
        public VisionMarkResult TopRightFid => TopRightFidCorrected ?? TopRightFidRaw;
        public VisionMarkResult TopRightAlign => TopRightAlignCorrected ?? TopRightAlignRaw;
        public VisionMarkResult TopLeftFid => TopLeftFidCorrected ?? TopLeftFidRaw;
        public VisionMarkResult TopLeftAlign => TopLeftAlignCorrected ?? TopLeftAlignRaw;

        public VisionMarkResult BtmRightFid => BtmRightFidCorrected ?? BtmRightFidRaw;
        public VisionMarkResult BtmRightAlign => BtmRightAlignCorrected ?? BtmRightAlignRaw;
        public VisionMarkResult BtmLeftFid => BtmLeftFidCorrected ?? BtmLeftFidRaw;
        public VisionMarkResult BtmLeftAlign => BtmLeftAlignCorrected ?? BtmLeftAlignRaw;

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
        public double PcHcroScale { get; set; }
        public double PcHcroThetaPlus { get; set; }
        public double PcHcroScaleX { get; set; } = 1.0;
        public double PcHcroScaleY { get; set; } = 1.0;
        public bool ScaleFallbackApplied { get; set; }  // 범위 벗어나 1.0 폴백 시 true

        // ── 캘리브레이션 파라미터 캐시 ───────────────────────
        public bool HasHcRO { get; set; }
        public bool HasPcT { get; set; }
        public double Hc1Rad { get; set; }
        public double Hc2Rad { get; set; }
        public double PcTRad { get; set; }
        public Point2D Hcro { get; set; }
        public Point2D Hc1Offset { get; set; }
        public Point2D Hc2Offset { get; set; }


        // 최종 보정치
        public double FinalShiftX { get; set; }   // 최종 X 이동량 (mm)
        public double FinalShiftY { get; set; }   // 최종 Y 이동량 (mm)
        public double FinalThetaF { get; set; }   // 최종 θ 회전량 (rad)
        public double FinalThetaO { get; set; }   // 측정된 얼라인 각도 (rad)
        public double OffsetXApplied { get; set; } // 레시피 X_ALIGN_OFFSET
        public double OffsetYApplied { get; set; } // 레시피 Y_ALIGN_OFFSET
        public double OffsetTApplied { get; set; } // 레시피 T_ALIGN_OFFSET
    }


    public class AlignData
    {
        public VisionMarkResult TopRightFidRaw { get; set; }
        public VisionMarkResult TopRightAlignRaw { get; set; }
        public VisionMarkResult TopLeftFidRaw { get; set; }
        public VisionMarkResult TopLeftAlignRaw { get; set; }

        public VisionMarkResult BtmRightFidRaw { get; set; }
        public VisionMarkResult BtmRightAlignRaw { get; set; }
        public VisionMarkResult BtmLeftFidRaw { get; set; }
        public VisionMarkResult BtmLeftAlignRaw { get; set; }

        public double PcTRad { get; set; }
        public double Hc1Rad { get; set; }
        public double Hc2Rad { get; set; }
        public Point2D Hcro { get; set; }
        public Point2D Hc2Offset { get; set; }

        public Point2D OffsetXY { get; set; }
        public double OffsetT { get; set; }

        // ── TopPlace 중간 계산값 ──
        public Point2D LDist { get; set; }       // Top Left: Align - Fid (cam)
        public Point2D RDist { get; set; }       // Top Right: Align - Fid (cam)

        public Point2D BL { get; set; }          // Btm Left Align (HcRO 기준)
        public Point2D BR { get; set; }          // Btm Right Align (HcRO 기준)
        public Point2D TL { get; set; }          // Top Left (회전 후, HcRO 기준)
        public Point2D TR { get; set; }          // Top Right (회전 후, HcRO 기준)
        public Point2D BFL { get; set; }         // Btm Left Fid (raw)
        public Point2D BFR { get; set; }         // Btm Right Fid (raw)

        public double BTheta { get; set; }       // atan2(br-bl) rad
        public double TTheta { get; set; }       // atan2(tr-tl) rad
        public double ThetaF { get; set; }       // 최종 보정 θ (deg)
        public double ThetaFRad { get; set; }    // thetaF in rad
        public double SpecTheta { get; set; }    // 레시피 SPEC_THETA

        public Point2D TCenter { get; set; }     // Top 중심
        public Point2D BCenter { get; set; }     // Btm 중심


        public double ResultX { get; set; }
        public double ResultY { get; set; }
        public double ResultT { get; set; }


        public bool AvgMove { get; set; } = false;

    }
}