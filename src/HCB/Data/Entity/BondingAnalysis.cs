using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 분석 데이터(상대거리·각도 계산 결과). PK = FK = BondingRecordId (공유키 1:1).
    /// P_ = P-Table, W_ = W-Table, PC/HC = 카메라. 미계산 항목은 NULL.
    /// </summary>
    [Table("BondingAnalysis")]
    public class BondingAnalysis
    {
        [Key]
        public int BondingRecordId { get; set; }

        // ── P-Table PC 카메라 ──
        public double? P_PC_Fid_DX { get; set; }
        public double? P_PC_Fid_DY { get; set; }
        public double? P_PC_Fid_Dist { get; set; }
        public double? P_PC_Fid_Theta { get; set; }

        public double? P_PC_Align_DX { get; set; }
        public double? P_PC_Align_DY { get; set; }
        public double? P_PC_Align_Dist { get; set; }
        public double? P_PC_Align_Theta { get; set; }

        // ── P-Table HC1/HC2 Fiducial ──
        public double? P_HC_Fid_L_X { get; set; }
        public double? P_HC_Fid_L_Y { get; set; }
        public double? P_HC_Fid_R_X { get; set; }
        public double? P_HC_Fid_R_Y { get; set; }
        public double? P_HC_Fid_DX { get; set; }
        public double? P_HC_Fid_DY { get; set; }
        public double? P_HC_Fid_Dist { get; set; }
        public double? P_HC_Fid_Theta { get; set; }

        // ── W-Table HC1/HC2 Fiducial ──
        public double? W_HC_Fid_L_X { get; set; }
        public double? W_HC_Fid_L_Y { get; set; }
        public double? W_HC_Fid_R_X { get; set; }
        public double? W_HC_Fid_R_Y { get; set; }
        public double? W_HC_Fid_DX { get; set; }
        public double? W_HC_Fid_DY { get; set; }
        public double? W_HC_Fid_Dist { get; set; }
        public double? W_HC_Fid_Theta { get; set; }

        // ── W-Table HC1/HC2 Align ──
        public double? W_HC_Align_L_X { get; set; }
        public double? W_HC_Align_L_Y { get; set; }
        public double? W_HC_Align_R_X { get; set; }
        public double? W_HC_Align_R_Y { get; set; }
        public double? W_HC_Align_DX { get; set; }
        public double? W_HC_Align_DY { get; set; }
        public double? W_HC_Align_Dist { get; set; }
        public double? W_HC_Align_Theta { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
