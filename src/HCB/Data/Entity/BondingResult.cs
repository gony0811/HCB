using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 본딩 결과(최종 보정량 + Vernier 잔차). PK = FK = BondingRecordId (공유키 1:1).
    /// Vernier_* 는 미측정 시 NULL.
    /// </summary>
    [Table("BondingResult")]
    public class BondingResult
    {
        [Key]
        public int BondingRecordId { get; set; }

        public double ResultX { get; set; }
        public double ResultY { get; set; }
        public double ResultT { get; set; }

        public double? Vernier_OffsetX { get; set; }
        public double? Vernier_OffsetY { get; set; }
        public double? Vernier_OffsetT { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
