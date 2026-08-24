using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HCB.UI
{
    /// <summary>카메라 거리(CamDistRow)·회전중심(HcroPointRow) 목록을 CSV 문자열로 변환한다.</summary>
    public static class CamHcroCsvExporter
    {
        private static string S(double v) => v.ToString("F6", CultureInfo.InvariantCulture);

        public static string BuildCamDistCsv(IEnumerable<CamDistRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Time,BondingId,Hc1_StageX,Hc1_StageY,Hc1_DxCam,Hc1_DyCam,Hc1_CenterX,Hc1_CenterY,Hc2_StageX,Hc2_StageY,Hc2_DxCam,Hc2_DyCam,Hc2_CenterX,Hc2_CenterY,Hc2Offset_X,Hc2Offset_Y");

            foreach (var r in rows)
                sb.AppendLine(string.Join(",",
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"), r.BondingId,
                    S(r.Hc1_StageX), S(r.Hc1_StageY), S(r.Hc1_DxCam), S(r.Hc1_DyCam), S(r.Hc1_CenterX), S(r.Hc1_CenterY),
                    S(r.Hc2_StageX), S(r.Hc2_StageY), S(r.Hc2_DxCam), S(r.Hc2_DyCam), S(r.Hc2_CenterX), S(r.Hc2_CenterY),
                    S(r.Hc2Offset_X), S(r.Hc2Offset_Y)));

            return sb.ToString();
        }

        public static string BuildHcroCsv(IEnumerable<HcroPointRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Time,BondingId,PointIndex,Angle,Hc1_X,Hc1_Y,Hc2_X,Hc2_Y");

            foreach (var r in rows)
                sb.AppendLine(string.Join(",",
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"), r.BondingId,
                    r.PointIndex, S(r.Angle),
                    S(r.Hc1_X), S(r.Hc1_Y), S(r.Hc2_X), S(r.Hc2_Y)));

            return sb.ToString();
        }
    }
}
