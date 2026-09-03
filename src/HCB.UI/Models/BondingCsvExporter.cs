using HCB.Data.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HCB.UI
{
    /// <summary>
    /// BondingRecord(+ 6개 자식 테이블) 목록을 전체 상세 CSV 문자열로 변환한다.
    /// 그리드는 요약만 보여주고, CSV는 측정/설비/설정/분석/통합좌표/결과 전 컬럼을 내보낸다.
    /// </summary>
    public static class BondingCsvExporter
    {
        private static string S(double? v) => v?.ToString("F6", CultureInfo.InvariantCulture) ?? "";

        // (헤더, 값 추출) 쌍을 한 곳에서 관리해 헤더와 행이 어긋나지 않게 한다.
        private static readonly List<(string Header, Func<BondingRecord, string> Cell)> Columns = BuildColumns();

        public static string BuildCsv(IEnumerable<BondingRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Columns.Select(c => c.Header)));

            foreach (var r in records)
                sb.AppendLine(string.Join(",", Columns.Select(c => c.Cell(r))));

            return sb.ToString();
        }

        private static List<(string, Func<BondingRecord, string>)> BuildColumns()
        {
            var c = new List<(string, Func<BondingRecord, string>)>();

            // ── 본딩 정보 (마스터) ──
            c.Add(("Id", r => r.Id.ToString()));
            c.Add(("Time", r => r.Time.ToString("yyyy-MM-dd HH:mm:ss")));
            c.Add(("Kind", r => r.Kind.ToString()));
            c.Add(("ParentRecordId", r => r.ParentRecordId?.ToString() ?? ""));
            c.Add(("AvgMode", r => r.AvgMode.ToString()));
            c.Add(("Use2DMapping", r => r.Use2DMapping.ToString()));
            c.Add(("TracingMode", r => r.TracingMode ?? ""));
            c.Add(("UseBtmIndividualMeasure", r => r.UseBtmIndividualMeasure.ToString()));
            c.Add(("UseFiducialTracking", r => r.UseFiducialTracking.ToString()));
            c.Add(("UseRightFidSimilarity", r => r.UseRightFidSimilarity.ToString()));

            // ── 설비 정보 ──
            c.Add(("PcTRad", r => S(r.Equipment?.PcTRad)));
            c.Add(("Hc1Rad", r => S(r.Equipment?.Hc1Rad)));
            c.Add(("Hc2Rad", r => S(r.Equipment?.Hc2Rad)));
            c.Add(("Hcro_X", r => S(r.Equipment?.Hcro_X)));
            c.Add(("Hcro_Y", r => S(r.Equipment?.Hcro_Y)));
            c.Add(("Hc2Offset_X", r => S(r.Equipment?.Hc2Offset_X)));
            c.Add(("Hc2Offset_Y", r => S(r.Equipment?.Hc2Offset_Y)));

            // ── 추가 설정 ──
            c.Add(("OffsetX", r => S(r.Setting?.OffsetX)));
            c.Add(("OffsetY", r => S(r.Setting?.OffsetY)));
            c.Add(("OffsetT", r => S(r.Setting?.OffsetT)));

            // ── 측정 정보 (Top: 6항목 / Btm: DxCam·DyCam) ──
            c.Add(("TopRF_StageX", r => S(r.Measurement?.TopRF_StageX)));
            c.Add(("TopRF_StageY", r => S(r.Measurement?.TopRF_StageY)));
            c.Add(("TopRF_DxCam", r => S(r.Measurement?.TopRF_DxCam)));
            c.Add(("TopRF_DyCam", r => S(r.Measurement?.TopRF_DyCam)));
            c.Add(("TopRF_CenterX", r => S(r.Measurement?.TopRF_CenterX)));
            c.Add(("TopRF_CenterY", r => S(r.Measurement?.TopRF_CenterY)));
            c.Add(("TopRA_StageX", r => S(r.Measurement?.TopRA_StageX)));
            c.Add(("TopRA_StageY", r => S(r.Measurement?.TopRA_StageY)));
            c.Add(("TopRA_DxCam", r => S(r.Measurement?.TopRA_DxCam)));
            c.Add(("TopRA_DyCam", r => S(r.Measurement?.TopRA_DyCam)));
            c.Add(("TopRA_CenterX", r => S(r.Measurement?.TopRA_CenterX)));
            c.Add(("TopRA_CenterY", r => S(r.Measurement?.TopRA_CenterY)));
            c.Add(("TopLF_StageX", r => S(r.Measurement?.TopLF_StageX)));
            c.Add(("TopLF_StageY", r => S(r.Measurement?.TopLF_StageY)));
            c.Add(("TopLF_DxCam", r => S(r.Measurement?.TopLF_DxCam)));
            c.Add(("TopLF_DyCam", r => S(r.Measurement?.TopLF_DyCam)));
            c.Add(("TopLF_CenterX", r => S(r.Measurement?.TopLF_CenterX)));
            c.Add(("TopLF_CenterY", r => S(r.Measurement?.TopLF_CenterY)));
            c.Add(("TopLA_StageX", r => S(r.Measurement?.TopLA_StageX)));
            c.Add(("TopLA_StageY", r => S(r.Measurement?.TopLA_StageY)));
            c.Add(("TopLA_DxCam", r => S(r.Measurement?.TopLA_DxCam)));
            c.Add(("TopLA_DyCam", r => S(r.Measurement?.TopLA_DyCam)));
            c.Add(("TopLA_CenterX", r => S(r.Measurement?.TopLA_CenterX)));
            c.Add(("TopLA_CenterY", r => S(r.Measurement?.TopLA_CenterY)));
            c.Add(("BtmRF_DxCam", r => S(r.Measurement?.BtmRF_DxCam)));
            c.Add(("BtmRF_DyCam", r => S(r.Measurement?.BtmRF_DyCam)));
            c.Add(("BtmRA_DxCam", r => S(r.Measurement?.BtmRA_DxCam)));
            c.Add(("BtmRA_DyCam", r => S(r.Measurement?.BtmRA_DyCam)));
            c.Add(("BtmLF_DxCam", r => S(r.Measurement?.BtmLF_DxCam)));
            c.Add(("BtmLF_DyCam", r => S(r.Measurement?.BtmLF_DyCam)));
            c.Add(("BtmLA_DxCam", r => S(r.Measurement?.BtmLA_DxCam)));
            c.Add(("BtmLA_DyCam", r => S(r.Measurement?.BtmLA_DyCam)));

            // ── 분석 데이터 ──
            c.Add(("P_PC_Fid_DX", r => S(r.Analysis?.P_PC_Fid_DX)));
            c.Add(("P_PC_Fid_DY", r => S(r.Analysis?.P_PC_Fid_DY)));
            c.Add(("P_PC_Fid_Dist", r => S(r.Analysis?.P_PC_Fid_Dist)));
            c.Add(("P_PC_Fid_Theta", r => S(r.Analysis?.P_PC_Fid_Theta)));
            c.Add(("P_PC_Align_DX", r => S(r.Analysis?.P_PC_Align_DX)));
            c.Add(("P_PC_Align_DY", r => S(r.Analysis?.P_PC_Align_DY)));
            c.Add(("P_PC_Align_Dist", r => S(r.Analysis?.P_PC_Align_Dist)));
            c.Add(("P_PC_Align_Theta", r => S(r.Analysis?.P_PC_Align_Theta)));
            c.Add(("P_HC_Fid_L_X", r => S(r.Analysis?.P_HC_Fid_L_X)));
            c.Add(("P_HC_Fid_L_Y", r => S(r.Analysis?.P_HC_Fid_L_Y)));
            c.Add(("P_HC_Fid_R_X", r => S(r.Analysis?.P_HC_Fid_R_X)));
            c.Add(("P_HC_Fid_R_Y", r => S(r.Analysis?.P_HC_Fid_R_Y)));
            c.Add(("P_HC_Fid_DX", r => S(r.Analysis?.P_HC_Fid_DX)));
            c.Add(("P_HC_Fid_DY", r => S(r.Analysis?.P_HC_Fid_DY)));
            c.Add(("P_HC_Fid_Dist", r => S(r.Analysis?.P_HC_Fid_Dist)));
            c.Add(("P_HC_Fid_Theta", r => S(r.Analysis?.P_HC_Fid_Theta)));
            c.Add(("W_HC_Fid_L_X", r => S(r.Analysis?.W_HC_Fid_L_X)));
            c.Add(("W_HC_Fid_L_Y", r => S(r.Analysis?.W_HC_Fid_L_Y)));
            c.Add(("W_HC_Fid_R_X", r => S(r.Analysis?.W_HC_Fid_R_X)));
            c.Add(("W_HC_Fid_R_Y", r => S(r.Analysis?.W_HC_Fid_R_Y)));
            c.Add(("W_HC_Fid_DX", r => S(r.Analysis?.W_HC_Fid_DX)));
            c.Add(("W_HC_Fid_DY", r => S(r.Analysis?.W_HC_Fid_DY)));
            c.Add(("W_HC_Fid_Dist", r => S(r.Analysis?.W_HC_Fid_Dist)));
            c.Add(("W_HC_Fid_Theta", r => S(r.Analysis?.W_HC_Fid_Theta)));
            c.Add(("W_HC_Align_L_X", r => S(r.Analysis?.W_HC_Align_L_X)));
            c.Add(("W_HC_Align_L_Y", r => S(r.Analysis?.W_HC_Align_L_Y)));
            c.Add(("W_HC_Align_R_X", r => S(r.Analysis?.W_HC_Align_R_X)));
            c.Add(("W_HC_Align_R_Y", r => S(r.Analysis?.W_HC_Align_R_Y)));
            c.Add(("W_HC_Align_DX", r => S(r.Analysis?.W_HC_Align_DX)));
            c.Add(("W_HC_Align_DY", r => S(r.Analysis?.W_HC_Align_DY)));
            c.Add(("W_HC_Align_Dist", r => S(r.Analysis?.W_HC_Align_Dist)));
            c.Add(("W_HC_Align_Theta", r => S(r.Analysis?.W_HC_Align_Theta)));

            // ── 통합 좌표 ──
            c.Add(("BFL_X", r => S(r.Coordinate?.BFL_X)));
            c.Add(("BFL_Y", r => S(r.Coordinate?.BFL_Y)));
            c.Add(("BFR_X", r => S(r.Coordinate?.BFR_X)));
            c.Add(("BFR_Y", r => S(r.Coordinate?.BFR_Y)));
            c.Add(("BL_X", r => S(r.Coordinate?.BL_X)));
            c.Add(("BL_Y", r => S(r.Coordinate?.BL_Y)));
            c.Add(("BR_X", r => S(r.Coordinate?.BR_X)));
            c.Add(("BR_Y", r => S(r.Coordinate?.BR_Y)));
            c.Add(("TL_X", r => S(r.Coordinate?.TL_X)));
            c.Add(("TL_Y", r => S(r.Coordinate?.TL_Y)));
            c.Add(("TR_X", r => S(r.Coordinate?.TR_X)));
            c.Add(("TR_Y", r => S(r.Coordinate?.TR_Y)));

            // ── 본딩 결과 ──
            c.Add(("ResultX", r => S(r.Result?.ResultX)));
            c.Add(("ResultY", r => S(r.Result?.ResultY)));
            c.Add(("ResultT", r => S(r.Result?.ResultT)));
            c.Add(("Vernier_OffsetX", r => S(r.Result?.Vernier_OffsetX)));
            c.Add(("Vernier_OffsetY", r => S(r.Result?.Vernier_OffsetY)));
            c.Add(("Vernier_OffsetT", r => S(r.Result?.Vernier_OffsetT)));

            return c;
        }
    }
}
