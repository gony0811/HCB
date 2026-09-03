using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>설비 정보(캘리브레이션 파라미터). PK = FK = BondingRecordId (공유키 1:1).</summary>
    [Table("BondingEquipment")]
    public class BondingEquipment
    {
        [Key]
        public int BondingRecordId { get; set; }

        public double PcTRad { get; set; }
        public double Hc1Rad { get; set; }
        public double Hc2Rad { get; set; }
        public double Hcro_X { get; set; }
        public double Hcro_Y { get; set; }
        public double Hc2Offset_X { get; set; }
        public double Hc2Offset_Y { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
