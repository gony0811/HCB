using HCB.Data.Entity;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HCB.UI
{
    /// <summary>버니어 로그(VernierLog) 목록을 CSV 문자열로 변환한다.</summary>
    public static class VernierCsvExporter
    {
        private static string S(double? v) => v?.ToString("F6", CultureInfo.InvariantCulture) ?? "";

        public static string BuildCsv(IEnumerable<VernierLog> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Time,Pos,V1_X,V1_Y,V3_X,V3_Y,OffsetX,OffsetY,OffsetT");

            foreach (var r in rows)
                sb.AppendLine(string.Join(",",
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    r.Name,
                    S(r.V1X), S(r.V1Y), S(r.V3X), S(r.V3Y),
                    S(r.OffsetX), S(r.OffsetY), S(r.OffsetT)));

            return sb.ToString();
        }
    }
}
