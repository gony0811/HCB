using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>추가 설정(얼라인 오프셋). PK = FK = BondingRecordId (공유키 1:1).</summary>
    [Table("BondingSetting")]
    public class BondingSetting
    {
        [Key]
        public int BondingRecordId { get; set; }

        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetT { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
