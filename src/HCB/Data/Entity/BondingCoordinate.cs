using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 통합 좌표(HCRO 기준). BF=Btm Fid, B=Btm Align, T=Top. PK = FK = BondingRecordId (공유키 1:1).
    /// </summary>
    [Table("BondingCoordinate")]
    public class BondingCoordinate
    {
        [Key]
        public int BondingRecordId { get; set; }

        public double BFL_X { get; set; }
        public double BFL_Y { get; set; }
        public double BFR_X { get; set; }
        public double BFR_Y { get; set; }
        public double BL_X { get; set; }
        public double BL_Y { get; set; }
        public double BR_X { get; set; }
        public double BR_Y { get; set; }
        public double TL_X { get; set; }
        public double TL_Y { get; set; }
        public double TR_X { get; set; }
        public double TR_Y { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
