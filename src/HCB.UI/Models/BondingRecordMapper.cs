using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    /// <summary>
    /// AlignData(+ Vernier 결과) → BondingRecord 및 6개 자식 엔티티 매핑.
    /// 분석 데이터의 상대거리/각도 계산은 기존 CSV(CsvMeasurementData)와 동일한 로직을 사용한다.
    /// </summary>
    public static class BondingRecordMapper
    {
        public static BondingRecord ToEntity(AlignData d, VernierResult vernier, BondingKind kind)
        {
            return new BondingRecord
            {
                Time = DateTime.Now,
                AvgMode = d.AvgMove,
                Kind = kind,
                Use2DMapping = d.Use2DMapping,
                TracingMode = d.TracingMode.ToString(),
                UseBtmIndividualMeasure = d.UseBtmIndividualMeasure,
                UseFiducialTracking = d.UseFiducialTracking,
                UseRightFidSimilarity = d.UseRightFidSimilarity,
                Measurement = BuildMeasurement(d),
                Equipment = BuildEquipment(d),
                Setting = BuildSetting(d),
                Analysis = BuildAnalysis(d),
                Coordinate = BuildCoordinate(d),
                Result = BuildResult(d, vernier),
                CamDist = BuildCamDist(d),
                HcroPoints = BuildHcroPoints(d),
            };
        }

        // 카메라 거리 측정 원본 (Manual 트레이싱에서 측정된 경우만). 미측정이면 null → 행 미생성.
        private static CamDistMeasurement BuildCamDist(AlignData d)
        {
            if (!d.CamDistMeasured) return null;

            return new CamDistMeasurement
            {
                Hc1_StageX = d.CamDistHc1Stage?.X ?? 0,
                Hc1_StageY = d.CamDistHc1Stage?.Y ?? 0,
                Hc1_DxCam = d.CamDistHc1Residual?.X ?? 0,
                Hc1_DyCam = d.CamDistHc1Residual?.Y ?? 0,
                Hc1_CenterX = d.CamDistHc1Center?.X ?? 0,
                Hc1_CenterY = d.CamDistHc1Center?.Y ?? 0,
                Hc2_StageX = d.CamDistHc2Stage?.X ?? 0,
                Hc2_StageY = d.CamDistHc2Stage?.Y ?? 0,
                Hc2_DxCam = d.CamDistHc2Residual?.X ?? 0,
                Hc2_DyCam = d.CamDistHc2Residual?.Y ?? 0,
                Hc2_CenterX = d.CamDistHc2Center?.X ?? 0,
                Hc2_CenterY = d.CamDistHc2Center?.Y ?? 0,
                Hc2Offset_X = d.Hc2Offset?.X ?? 0,
                Hc2Offset_Y = d.Hc2Offset?.Y ?? 0,
            };
        }

        // 회전중심 측정점 (Hc1RoRaw/Hc2RoRaw + HcroAngles). 미측정이면 빈 목록 → 행 미생성.
        private static List<HcroMeasurementPoint> BuildHcroPoints(AlignData d)
        {
            var list = new List<HcroMeasurementPoint>();
            if (d.Hc1RoRaw == null || d.Hc1RoRaw.Count == 0) return list;

            int n = d.Hc1RoRaw.Count;
            for (int i = 0; i < n; i++)
            {
                var hc1 = d.Hc1RoRaw[i];
                var hc2 = (d.Hc2RoRaw != null && i < d.Hc2RoRaw.Count) ? d.Hc2RoRaw[i] : null;
                double angle = (d.HcroAngles != null && i < d.HcroAngles.Count) ? d.HcroAngles[i] : 0;

                list.Add(new HcroMeasurementPoint
                {
                    PointIndex = i,
                    Angle = angle,
                    Hc1_X = hc1?.X ?? 0,
                    Hc1_Y = hc1?.Y ?? 0,
                    Hc2_X = hc2?.X ?? 0,
                    Hc2_Y = hc2?.Y ?? 0,
                });
            }
            return list;
        }

        private static BondingMeasurement BuildMeasurement(AlignData d)
        {
            var m = new BondingMeasurement();

            SetMark(d.TopRightFidRaw, v => m.TopRF_StageX = v, v => m.TopRF_StageY = v,
                v => m.TopRF_DxCam = v, v => m.TopRF_DyCam = v, v => m.TopRF_CenterX = v, v => m.TopRF_CenterY = v);
            SetMark(d.TopRightAlignRaw, v => m.TopRA_StageX = v, v => m.TopRA_StageY = v,
                v => m.TopRA_DxCam = v, v => m.TopRA_DyCam = v, v => m.TopRA_CenterX = v, v => m.TopRA_CenterY = v);
            SetMark(d.TopLeftFidRaw, v => m.TopLF_StageX = v, v => m.TopLF_StageY = v,
                v => m.TopLF_DxCam = v, v => m.TopLF_DyCam = v, v => m.TopLF_CenterX = v, v => m.TopLF_CenterY = v);
            SetMark(d.TopLeftAlignRaw, v => m.TopLA_StageX = v, v => m.TopLA_StageY = v,
                v => m.TopLA_DxCam = v, v => m.TopLA_DyCam = v, v => m.TopLA_CenterX = v, v => m.TopLA_CenterY = v);

            m.BtmRF_DxCam = d.BtmRightFidRaw?.X;   m.BtmRF_DyCam = d.BtmRightFidRaw?.Y;
            m.BtmRA_DxCam = d.BtmRightAlignRaw?.X;  m.BtmRA_DyCam = d.BtmRightAlignRaw?.Y;
            m.BtmLF_DxCam = d.BtmLeftFidRaw?.X;     m.BtmLF_DyCam = d.BtmLeftFidRaw?.Y;
            m.BtmLA_DxCam = d.BtmLeftAlignRaw?.X;   m.BtmLA_DyCam = d.BtmLeftAlignRaw?.Y;

            return m;
        }

        private static void SetMark(VisionMarkResult src,
            Action<double?> stageX, Action<double?> stageY,
            Action<double?> dxCam, Action<double?> dyCam,
            Action<double?> centerX, Action<double?> centerY)
        {
            if (src == null) return;
            stageX(src.StageX); stageY(src.StageY);
            dxCam(src.DxCamToMark); dyCam(src.DyCamToMark);
            centerX(src.CenterX); centerY(src.CenterY);
        }

        private static BondingEquipment BuildEquipment(AlignData d) => new BondingEquipment
        {
            PcTRad = d.PcTRad,
            Hc1Rad = d.Hc1Rad,
            Hc2Rad = d.Hc2Rad,
            Hcro_X = d.Hcro?.X ?? 0,
            Hcro_Y = d.Hcro?.Y ?? 0,
            Hc2Offset_X = d.Hc2Offset?.X ?? 0,
            Hc2Offset_Y = d.Hc2Offset?.Y ?? 0,
        };

        private static BondingSetting BuildSetting(AlignData d) => new BondingSetting
        {
            OffsetX = d.OffsetXY?.X ?? 0,
            OffsetY = d.OffsetXY?.Y ?? 0,
            OffsetT = d.OffsetT,
        };

        private static BondingCoordinate BuildCoordinate(AlignData d) => new BondingCoordinate
        {
            BFL_X = d.BFL?.X ?? 0, BFL_Y = d.BFL?.Y ?? 0,
            BFR_X = d.BFR?.X ?? 0, BFR_Y = d.BFR?.Y ?? 0,
            BL_X = d.BL?.X ?? 0, BL_Y = d.BL?.Y ?? 0,
            BR_X = d.BR?.X ?? 0, BR_Y = d.BR?.Y ?? 0,
            TL_X = d.TL?.X ?? 0, TL_Y = d.TL?.Y ?? 0,
            TR_X = d.TR?.X ?? 0, TR_Y = d.TR?.Y ?? 0,
        };

        private static BondingResult BuildResult(AlignData d, VernierResult v) => new BondingResult
        {
            ResultX = d.ResultX,
            ResultY = d.ResultY,
            ResultT = d.ResultT,
            Vernier_OffsetX = v?.OffsetX,
            Vernier_OffsetY = v?.OffsetY,
            Vernier_OffsetT = v?.OffsetT,
        };

        private static BondingAnalysis BuildAnalysis(AlignData d)
        {
            var a = new BondingAnalysis();
            var offset = d.Hc2Offset;

            // 측정1: P_TABLE PC_Camera (CenterX/Y 기준)
            if (d.TopLeftFidRaw != null && d.TopRightFidRaw != null)
            {
                var r = CalibrationMath.CalcRelative(
                    d.TopLeftFidRaw.CenterX, d.TopLeftFidRaw.CenterY,
                    d.TopRightFidRaw.CenterX, d.TopRightFidRaw.CenterY);
                a.P_PC_Fid_DX = r.dx; a.P_PC_Fid_DY = r.dy; a.P_PC_Fid_Dist = r.dist; a.P_PC_Fid_Theta = r.theta;
            }
            if (d.TopLeftAlignRaw != null && d.TopRightAlignRaw != null)
            {
                var r = CalibrationMath.CalcRelative(
                    d.TopLeftAlignRaw.CenterX, d.TopLeftAlignRaw.CenterY,
                    d.TopRightAlignRaw.CenterX, d.TopRightAlignRaw.CenterY);
                a.P_PC_Align_DX = r.dx; a.P_PC_Align_DY = r.dy; a.P_PC_Align_Dist = r.dist; a.P_PC_Align_Theta = r.theta;
            }

            // 측정2: P_TABLE HC1/HC2 Fiducial (Hc2Offset 보정)
            if (offset != null && d.Hc1FidCurrent != null && d.Hc2FidCurrent != null)
            {
                double lx = -d.Hc1FidCurrent.X, ly = -d.Hc1FidCurrent.Y;
                double rx = offset.X - d.Hc2FidCurrent.X, ry = offset.Y - d.Hc2FidCurrent.Y;
                var r = CalibrationMath.CalcRelative(lx, ly, rx, ry);
                a.P_HC_Fid_L_X = lx; a.P_HC_Fid_L_Y = ly; a.P_HC_Fid_R_X = rx; a.P_HC_Fid_R_Y = ry;
                a.P_HC_Fid_DX = r.dx; a.P_HC_Fid_DY = r.dy; a.P_HC_Fid_Dist = r.dist; a.P_HC_Fid_Theta = r.theta;
            }

            // 측정3: W_TABLE HC1/HC2 Fiducial
            if (offset != null && d.BtmLeftFidRaw != null && d.BtmRightFidRaw != null)
            {
                double lx = -d.BtmLeftFidRaw.X, ly = -d.BtmLeftFidRaw.Y;
                double rx = offset.X - d.BtmRightFidRaw.X, ry = offset.Y - d.BtmRightFidRaw.Y;
                var r = CalibrationMath.CalcRelative(lx, ly, rx, ry);
                a.W_HC_Fid_L_X = lx; a.W_HC_Fid_L_Y = ly; a.W_HC_Fid_R_X = rx; a.W_HC_Fid_R_Y = ry;
                a.W_HC_Fid_DX = r.dx; a.W_HC_Fid_DY = r.dy; a.W_HC_Fid_Dist = r.dist; a.W_HC_Fid_Theta = r.theta;
            }

            // 측정3: W_TABLE HC1/HC2 Align
            if (offset != null && d.BtmLeftAlignRaw != null && d.BtmRightAlignRaw != null)
            {
                double lx = -d.BtmLeftAlignRaw.X, ly = -d.BtmLeftAlignRaw.Y;
                double rx = offset.X - d.BtmRightAlignRaw.X, ry = offset.Y - d.BtmRightAlignRaw.Y;
                var r = CalibrationMath.CalcRelative(lx, ly, rx, ry);
                a.W_HC_Align_L_X = lx; a.W_HC_Align_L_Y = ly; a.W_HC_Align_R_X = rx; a.W_HC_Align_R_Y = ry;
                a.W_HC_Align_DX = r.dx; a.W_HC_Align_DY = r.dy; a.W_HC_Align_Dist = r.dist; a.W_HC_Align_Theta = r.theta;
            }

            return a;
        }
    }
}
