using System;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class SequenceService
    {
        // ═════════════════════════════════════════════════════
        //  측정 처리 (거리 계산 + 로그)
        // ═════════════════════════════════════════════════════

        public void ProcessMeasurement(AlignData data, int phase)
        {
            ComputeDistances(data);
            switch (phase)
            {
                case 1: LogMeasurement1(data); break;
                case 2: LogMeasurement2(data); break;
                case 3: LogMeasurement3(data); break;
            }
        }

        public void ComputeDistances(AlignData data)
        {
            if (data == null) return;

            if (data.BL != null && data.BR != null)
            {
                data.BtmAlignDist = CalibrationMath.Dist(data.BR, data.BL);
                data.BtmAlignDistX = data.BR.X - data.BL.X;
                data.BtmAlignDistY = data.BR.Y - data.BL.Y;
            }

            if (data.BFL != null && data.BFR != null)
            {
                data.BtmFidDist = CalibrationMath.Dist(data.BFR, data.BFL);
                data.BtmFidDistX = data.BFR.X - data.BFL.X;
                data.BtmFidDistY = data.BFR.Y - data.BFL.Y;
            }

            if (data.TL != null && data.TR != null)
            {
                data.TopAlignDist = CalibrationMath.Dist(data.TR, data.TL);
                data.TopAlignDistX = data.TR.X - data.TL.X;
                data.TopAlignDistY = data.TR.Y - data.TL.Y;
            }

            if (data.TopLeftFidRaw != null && data.TopRightFidRaw != null)
            {
                var dx = data.TopRightFidRaw.CenterX - data.TopLeftFidRaw.CenterX;
                var dy = data.TopRightFidRaw.CenterY - data.TopLeftFidRaw.CenterY;
                data.TopFidDist = Math.Sqrt(dx * dx + dy * dy);
                data.TopFidDistX = dx;
                data.TopFidDistY = dy;
            }
        }

        // ═════════════════════════════════════════════════════
        //  측정 로그 (내부)
        // ═════════════════════════════════════════════════════

        private void LogVisionMark(string prefix, string camera, string mark, string side, VisionMarkResult m)
        {
            if (m == null) return;
            _logger.Information(
                "{Prefix} | {Camera} | {Mark} | {Side} | 비전({DxCam:F6}, {DyCam:F6}) 모션({StageX:F6}, {StageY:F6}) 절대({CenterX:F6}, {CenterY:F6})",
                prefix, camera, mark, side, m.DxCamToMark, m.DyCamToMark, m.StageX, m.StageY, m.CenterX, m.CenterY);
        }

        private void LogHcMark(string prefix, string camera, string mark, string side,
            double visionX, double visionY, double absX, double absY)
        {
            _logger.Information(
                "{Prefix} | {Camera} | {Mark} | {Side} | 비전({VisionX:F6}, {VisionY:F6}) 절대({AbsX:F6}, {AbsY:F6})",
                prefix, camera, mark, side, visionX, visionY, absX, absY);
        }

        private void LogHcMarkWithOffset(string prefix, string camera, string mark, string side,
            double visionX, double visionY, double offsetX, double offsetY, double absX, double absY)
        {
            _logger.Information(
                "{Prefix} | {Camera} | {Mark} | {Side} | 비전({VisionX:F6}, {VisionY:F6}) Hc2Offset({OffX:F6}, {OffY:F6}) 절대({AbsX:F6}, {AbsY:F6})",
                prefix, camera, mark, side, visionX, visionY, offsetX, offsetY, absX, absY);
        }

        private void LogRelativeDistance(string prefix, string camera, string mark,
            double dx, double dy, double dist, double theta)
        {
            _logger.Information(
                "{Prefix} | {Camera} | {Mark} | 상대거리 | ΔX={DX:F6} ΔY={DY:F6} Dist={Dist:F6} Theta={Theta:F4}°",
                prefix, camera, mark, dx, dy, dist, theta);
        }

        private void LogMeasurement1(AlignData data)
        {
            if (data == null) return;
            const string h = "[측정1] P_TABLE";
            const string cam = "PC_Camera";

            LogVisionMark(h, cam, "Fiducial", "Right", data.TopRightFidRaw);
            LogVisionMark(h, cam, "Fiducial", "Left", data.TopLeftFidRaw);
            if (data.TopRightFidRaw != null && data.TopLeftFidRaw != null)
            {
                var r = CalibrationMath.CalcRelative(
                    data.TopLeftFidRaw.CenterX, data.TopLeftFidRaw.CenterY,
                    data.TopRightFidRaw.CenterX, data.TopRightFidRaw.CenterY);
                LogRelativeDistance(h, cam, "Fiducial", r.dx, r.dy, r.dist, r.theta);
            }

            LogVisionMark(h, cam, "Align", "Right", data.TopRightAlignRaw);
            LogVisionMark(h, cam, "Align", "Left", data.TopLeftAlignRaw);
            if (data.TopRightAlignRaw != null && data.TopLeftAlignRaw != null)
            {
                var r = CalibrationMath.CalcRelative(
                    data.TopLeftAlignRaw.CenterX, data.TopLeftAlignRaw.CenterY,
                    data.TopRightAlignRaw.CenterX, data.TopRightAlignRaw.CenterY);
                LogRelativeDistance(h, cam, "Align", r.dx, r.dy, r.dist, r.theta);
            }
        }

        private void LogMeasurement2(AlignData data)
        {
            if (data?.Hc2Offset == null) return;
            const string h = "[측정2] P_TABLE";
            var offset = data.Hc2Offset;

            if (data.Hc1FidCurrent != null && data.Hc2FidCurrent != null)
            {
                double lfAbsX = -data.Hc1FidCurrent.X;
                double lfAbsY = -data.Hc1FidCurrent.Y;
                double rfAbsX = offset.X - data.Hc2FidCurrent.X;
                double rfAbsY = offset.Y - data.Hc2FidCurrent.Y;

                LogHcMark(h, "HC1_Camera", "Fiducial", "Left",
                    data.Hc1FidCurrent.X, data.Hc1FidCurrent.Y, lfAbsX, lfAbsY);
                LogHcMarkWithOffset(h, "HC2_Camera", "Fiducial", "Right",
                    data.Hc2FidCurrent.X, data.Hc2FidCurrent.Y, offset.X, offset.Y, rfAbsX, rfAbsY);

                var r = CalibrationMath.CalcRelative(lfAbsX, lfAbsY, rfAbsX, rfAbsY);
                LogRelativeDistance(h, "HC1/HC2", "Fiducial", r.dx, r.dy, r.dist, r.theta);
            }

            if (data.TL != null && data.TR != null)
            {
                _logger.Information(
                    "{Header} | HC1_Camera | Align | Left | 절대({X:F6}, {Y:F6})",
                    h, data.TL.X, data.TL.Y);
                _logger.Information(
                    "{Header} | HC2_Camera | Align | Right | 절대({X:F6}, {Y:F6})",
                    h, data.TR.X, data.TR.Y);

                var r = CalibrationMath.CalcRelative(data.TL.X, data.TL.Y, data.TR.X, data.TR.Y);
                LogRelativeDistance(h, "HC1/HC2", "Align", r.dx, r.dy, r.dist, r.theta);
            }
        }

        private void LogMeasurement3(AlignData data)
        {
            if (data?.Hc2Offset == null) return;
            const string h = "[측정3] W_TABLE";
            var offset = data.Hc2Offset;

            if (data.BtmLeftFidRaw != null && data.BtmRightFidRaw != null)
            {
                double lfAbsX = -data.BtmLeftFidRaw.X;
                double lfAbsY = -data.BtmLeftFidRaw.Y;
                double rfAbsX = offset.X - data.BtmRightFidRaw.X;
                double rfAbsY = offset.Y - data.BtmRightFidRaw.Y;

                LogHcMark(h, "HC1_Camera", "Fiducial", "Left",
                    data.BtmLeftFidRaw.X, data.BtmLeftFidRaw.Y, lfAbsX, lfAbsY);
                LogHcMarkWithOffset(h, "HC2_Camera", "Fiducial", "Right",
                    data.BtmRightFidRaw.X, data.BtmRightFidRaw.Y, offset.X, offset.Y, rfAbsX, rfAbsY);

                var r = CalibrationMath.CalcRelative(lfAbsX, lfAbsY, rfAbsX, rfAbsY);
                LogRelativeDistance(h, "HC1/HC2", "Fiducial", r.dx, r.dy, r.dist, r.theta);
            }

            if (data.BtmLeftAlignRaw != null && data.BtmRightAlignRaw != null)
            {
                double laAbsX = -data.BtmLeftAlignRaw.X;
                double laAbsY = -data.BtmLeftAlignRaw.Y;
                double raAbsX = offset.X - data.BtmRightAlignRaw.X;
                double raAbsY = offset.Y - data.BtmRightAlignRaw.Y;

                LogHcMark(h, "HC1_Camera", "Align", "Left",
                    data.BtmLeftAlignRaw.X, data.BtmLeftAlignRaw.Y, laAbsX, laAbsY);
                LogHcMarkWithOffset(h, "HC2_Camera", "Align", "Right",
                    data.BtmRightAlignRaw.X, data.BtmRightAlignRaw.Y, offset.X, offset.Y, raAbsX, raAbsY);

                var r = CalibrationMath.CalcRelative(laAbsX, laAbsY, raAbsX, raAbsY);
                LogRelativeDistance(h, "HC1/HC2", "Align", r.dx, r.dy, r.dist, r.theta);
            }
        }
    }
}
