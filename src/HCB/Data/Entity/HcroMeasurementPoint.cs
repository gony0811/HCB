using HCB.Data.Interface;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 회전중심(HcRO) 계산에 사용된 측정점 1개. H_T 각도(모션)별 HC1/HC2 피듀셜 비전 좌표.
    /// 한 본딩당 [0°, -1.2°, +1.2°] × 반복 개수만큼의 행을 가진다(1:N). Manual 트레이싱에서만 측정된다.
    /// </summary>
    [Table("HcroMeasurementPoint")]
    public class HcroMeasurementPoint : IEntity   // Id (PK) 자동
    {
        public int BondingRecordId { get; set; }

        /// <summary>측정 순서(0부터).</summary>
        public int PointIndex { get; set; }

        /// <summary>H_T 회전 각도(deg) — 모션.</summary>
        public double Angle { get; set; }

        // ── 비전: HC1/HC2 피듀셜 측정 좌표 ──
        public double Hc1_X { get; set; }
        public double Hc1_Y { get; set; }
        public double Hc2_X { get; set; }
        public double Hc2_Y { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
