using System;

namespace HCB.UI.SERVICE
{
    public class CalibrationService
    {
        public struct Point2D
        {
            public double X, Y;
            public Point2D(double x, double y) { X = x; Y = y; }
        }

        /// <summary>
        /// 비전 측정으로 얻은 마크의 Stage/Camera 좌표.
        /// 카메라별 Y 부호 규칙은 CenterY가 CameraType에 따라 자동 분기한다.
        ///   P-Table (Pc, Top) : StageY + DyCamToMark
        ///   W-Table (Hc, Btm) : StageY − DyCamToMark
        /// CameraType은 측정 시점에 반드시 설정해야 하며,
        /// 미설정 상태로 CenterY를 호출하면 예외가 발생한다.
        /// </summary>
        public class VisionMarkResult
        {
            public double StageX { get; set; }
            public double StageY { get; set; }

            // 비전 오프셋 (카메라 → 마크)
            public double DxCamToMark { get; set; }
            public double DyCamToMark { get; set; }

            public CameraType CameraType { get; set; }
            public MarkType MarkType { get; set; }
            public DirectType DirectType { get; set; }

            /// <summary>
            /// 마크 절대 X 좌표. 모든 카메라 공통.
            ///     = StageX − DxCamToMark
            /// </summary>
            public double CenterX => StageX - DxCamToMark;

            /// <summary>
            /// 마크 절대 Y 좌표. 카메라 종류에 따라 자동 분기.
            ///   P-Table (Pc, Top) : StageY + DyCamToMark
            ///   W-Table (Hc, Btm) : StageY − DyCamToMark
            /// CameraType이 설정되지 않았거나 미지원이면 예외.
            /// </summary>
            public double CenterY => CameraType switch
            {
                CameraType.PC_HIGH or CameraType.PC_LOW
                    => StageY + DyCamToMark,

                CameraType.HC1_HIGH or CameraType.HC2_HIGH => StageY - DyCamToMark,

                _ => throw new InvalidOperationException(
                    $"VisionMarkResult.CenterY 호출 실패: CameraType이 설정되지 않았거나 미지원입니다. " +
                    $"(CameraType={CameraType}) 측정 시점에 CameraType을 반드시 지정하세요.")
            };

            public VisionMarkResult Clone() => (VisionMarkResult)MemberwiseClone();
        }

        // ── 일반 좌표계 ──────────────────────────────────────
        public static Point2D FidToDie(VisionMarkResult fid, VisionMarkResult die)
        {
            double dx = fid.CenterX - die.CenterX;
            double dy = fid.CenterY - die.CenterY;
            return new Point2D(dx, dy);
        }

        public static Point2D FidToWafer(VisionMarkResult fid, VisionMarkResult die)
        {
            double dx = fid.CenterX - die.CenterX;
            double dy = fid.CenterY - die.CenterY;
            return new Point2D(dx, dy);
        }

        /// <summary>
        /// 두 마크 사이 각도 (degree). CenterY가 CameraType에 따라 자동 분기.
        /// 두 마크는 같은 카메라에서 측정된 것이어야 한다.
        /// </summary>
        public static double CalcTheta(VisionMarkResult mark1, VisionMarkResult mark2)
        {
            double dx = mark2.CenterX - mark1.CenterX;
            double dy = mark2.CenterY - mark1.CenterY;
            return Math.Atan2(dy, dx) * (180.0 / Math.PI);
        }

        // NOTE: 기존 WaferCalcTheta 는 CenterWaferY 제거로 인해 삭제.
        // W-Table 마크는 CameraType = HC* 로 생성되면 CalcTheta 를 그대로 호출하면 된다.
    }
}