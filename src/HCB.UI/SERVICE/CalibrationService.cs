

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


        public class VisionMarkResult
        {
            public double StageX { get; set; }
            public double StageY { get; set; }

            // 비전 오프셋 (카메라 → 마크)
            public double DxCamToMark { get; set; }
            public double DyCamToMark { get; set; }

            // 실제 마크 중심좌표 (Stage + Offset) - 일반
            public double CenterX => StageX - DxCamToMark;
            public double CenterY => StageY + DyCamToMark;
            public double CenterWaferY => StageY - DyCamToMark;


            public MarkType MarkType { get; set; }
            public DirectType DirectType { get; set; }
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
            double dy = fid.CenterWaferY - die.CenterWaferY;
            return new Point2D(dx, dy);
        }

        //public static double CalcTheta(VisionMarkResult mark1, VisionMarkResult mark2, bool isWaferTable = false)
        //{
        //    double dx = mark2.CenterX - mark1.CenterX;
        //    double dy = isWaferTable
        //        ? mark2.CenterWaferY - mark1.CenterWaferY
        //        : mark2.CenterY - mark1.CenterY;
        //    return Math.Atan2(dy, dx) * (180.0 / Math.PI);
        //}

        public static double CalcTheta(VisionMarkResult mark1, VisionMarkResult mark2)
        {
            double dx = mark2.CenterX - mark1.CenterX;
            double dy = mark2.CenterY - mark1.CenterY;
            return Math.Atan2(dy, dx) * (180.0 / Math.PI);
        }

        public static double WaferCalcTheta(VisionMarkResult mark1, VisionMarkResult mark2, double offsetX, double offsetY)
        {
            double dx = mark2.CenterX - mark1.CenterX + offsetX;
            double dy = mark2.CenterWaferY - mark1.CenterWaferY + offsetY;
            return Math.Atan2(dy, dx) * (180.0 / Math.PI);
        }

    }
}
