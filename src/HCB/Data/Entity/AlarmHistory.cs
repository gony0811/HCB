using HCB.Data.Entity.Type;
using HCB.Data.Interface;

namespace HCB.Data.Entity
{
    public class AlarmHistory : IEntity
    {
        public int AlarmId { get; set; } // 알람 식별자
        public AlarmLevel Level { get; set; } // 알람 레벨
        public AlarmStatus Status { get; set; } // 알람 상태
        public DateTime UpdateTime { get; set; }

    }
}
