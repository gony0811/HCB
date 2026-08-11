// AlignContext.cs
using Telerik.Windows.Persistence.Core;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    /// <summary>
    /// TopHighAlign ~ BtmHighAlign ~ CoordinateSystemIntegration 전 구간에서
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

        public Point2D BtmRightFidRaw { get; set; } = Point2D.Zero;
        public Point2D BtmRightAlignRaw { get; set; } = Point2D.Zero;
        public Point2D BtmLeftFidRaw { get; set; } = Point2D.Zero;
        public Point2D BtmLeftAlignRaw { get; set; } = Point2D.Zero;

        public double PcTRad { get; set; }
        public double Hc1Rad { get; set; }
        public double Hc2Rad { get; set; }
        public Point2D Hcro { get; set; }= Point2D.Zero;
        public Point2D PcHcro { get; set; }
        public Point2D Hc2Offset { get; set; } = Point2D.Zero;

        public Point2D OffsetXY { get; set; } = Point2D.Zero;
        public double OffsetT { get; set; }

        // ── CoordinateSystemIntegration 중간 계산값 ──
        public Point2D LDist { get; set; } = Point2D.Zero;      // Top Left: Align - Fid (cam)
        public Point2D RDist { get; set; } = Point2D.Zero;      // Top Right: Align - Fid (cam)

        public Point2D BL { get; set; } = Point2D.Zero;         // Btm Left Align (HcRO 기준)
        public Point2D BR { get; set; } = Point2D.Zero;        // Btm Right Align (HcRO 기준)
        public Point2D TL { get; set; } = Point2D.Zero;        // Top Left (회전 후, HcRO 기준)
        public Point2D TR { get; set; } = Point2D.Zero;        // Top Right (회전 후, HcRO 기준)
        public Point2D BFL { get; set; } = Point2D.Zero;       // Btm Left Fid (raw)
        public Point2D BFR { get; set; } = Point2D.Zero;       // Btm Right Fid (raw)

        public double BTheta { get; set; }       // atan2(br-bl) rad
        public double TTheta { get; set; }       // atan2(tr-tl) rad
        public double ThetaF { get; set; }       // 최종 보정 θ (deg)
        public double ThetaFRad { get; set; }    // thetaF in rad
        public double SpecTheta { get; set; }    // 레시피 SPEC_THETA

        public Point2D TCenter { get; set; }     // Top 중심
        public Point2D BCenter { get; set; }     // Btm 중심

        // 선분 길이 측정값
        public double BtmAlignDist { get; set; }
        public double BtmAlignDistX { get; set; }
        public double BtmAlignDistY { get; set; }
        public double TopAlignDist { get; set; }
        public double TopAlignDistX { get; set; }
        public double TopAlignDistY { get; set; }
        public double BtmFidDist { get; set; }
        public double BtmFidDistX { get; set; }
        public double BtmFidDistY { get; set; }
        public double TopFidDist { get; set; }
        public double TopFidDistX { get; set; }
        public double TopFidDistY { get; set; }

        public double ResultX { get; set; } = 0;
        public double ResultY { get; set; } = 0;
        public double ResultT { get; set; } = 0;


        public bool AvgMove { get; set; } = false;
        public bool Use2DMapping { get; set; } = true;
        public TracingMode TracingMode { get; set; } = TracingMode.Auto;
        public bool UseBtmIndividualMeasure { get; set; } = false;
        public bool UseFiducialTracking { get; set; } = false;
        public bool UseRightFidSimilarity { get; set; } = false;   // 우측 피듀셜 P-TABLE↔W-TABLE 닮음변환 보정 ON/OFF

        // 우측 피듀셜 닮음변환 진단/로그용 (계산 결과 저장)
        public double RightFidSimTheta { get; set; }   // 닮음변환 회전각 (deg)
        public double RightFidSimScale { get; set; }   // 닮음변환 스케일 (≈1.0 기대)

        public bool UseFidCenterAlign { get; set; } = false;       // 피듀셜 중심 기준 강체 정렬 ON/OFF

        // 피듀셜 중심 강체 정렬 진단/로그용 (계산 결과 저장)
        public double FidCenterDTheta { get; set; }    // P→W 피듀셜 각도 변화량 (deg)
        public double FidCenterShiftX { get; set; }    // P→W 피듀셜 중심 이동량 X
        public double FidCenterShiftY { get; set; }    // P→W 피듀셜 중심 이동량 Y

        // 피듀셜 트래킹 결과
        public Point2D Hc1FidCurrent { get; set; } = Point2D.Zero;
        public Point2D Hc2FidCurrent { get; set; } = Point2D.Zero;
        public Point2D Hc1FidRef { get; set; } = Point2D.Zero;
        public Point2D Hc2FidRef { get; set; } = Point2D.Zero;
        public Point2D Hc1FidDrift { get; set; } = Point2D.Zero;
        public Point2D Hc2FidDrift { get; set; } = Point2D.Zero;
        public double FidCurrentDist { get; set; }

        // 측정2/3 Fiducial Theta (deg)
        public double M2FidTheta { get; set; }
        public double M3FidTheta { get; set; }

        // ── 측정 시 수집만 하고, 보정/계산은 CoordinateSystemIntegration에서 수행 ──
        //   H_Z tilt 투영용 ΔZ (측정만; PC/HC 계수 곱은 계산 단계에서 적용)
        public double TopRightDz { get; set; }   // = rightFidZ - rightAlignZ
        public double TopLeftDz { get; set; }    // = leftFidZ - leftAlignZ
        public double BtmDz { get; set; }        // = btmAlignZ - btmFidZ
        //   HcRO 회전중심 계산용 raw 측정점(0°/±0.75°). 계산은 ComputeHcroCenter에서.
        public System.Collections.Generic.List<Point2D> Hc1RoRaw { get; set; }
        public System.Collections.Generic.List<Point2D> Hc2RoRaw { get; set; }
    }

    public class FiducialAngleResult
    {
        // PC Table (TopDIE Fiducial — CenterX/CenterY 기준)
        public Point2D PcLeftFid { get; set; } = Point2D.Zero;
        public Point2D PcRightFid { get; set; } = Point2D.Zero;
        public double PcAngleDeg { get; set; }

        // Hc1/Hc2 (Bonding 위치 — camOffset 좌표 변환)
        public Point2D HcLeftFid { get; set; } = Point2D.Zero;
        public Point2D HcRightFid { get; set; } = Point2D.Zero;
        public double HcAngleDeg { get; set; }

        // Wafer Table (camOffset 좌표 변환)
        public Point2D WaferLeftFid { get; set; } = Point2D.Zero;
        public Point2D WaferRightFid { get; set; } = Point2D.Zero;
        public double WaferAngleDeg { get; set; }
    }
}