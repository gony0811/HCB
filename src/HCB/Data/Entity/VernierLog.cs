using HCB.Data.Interface;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 버니어 측정 결과(포인트 단위 1행). 한 번의 측정은 여러 포인트(Pos "1"/"3") 행으로 저장되며,
    /// 같은 측정의 행들은 Time과 Offset(측정당 계산된 잔차)을 공유한다.
    /// </summary>
    [Table("VernierLog")]
    public class VernierLog : IEntity   // Id (PK) 자동
    {
        public DateTime Time { get; set; } = DateTime.Now;

        /// <summary>포인트 위치 태그 (예: "1", "3").</summary>
        public string Name { get; set; } = "";

        public double? V1X { get; set; }
        public double? V1Y { get; set; }
        public double? V3X { get; set; }
        public double? V3Y { get; set; }

        // 해당 측정의 계산 오프셋 (측정 단위로 동일 값 반복 저장)
        public double? OffsetX { get; set; }
        public double? OffsetY { get; set; }
        public double? OffsetT { get; set; }
    }
}
