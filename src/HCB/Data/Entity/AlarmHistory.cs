using HCB.Data.Entity.Type;
using HCB.Data.Interface;

namespace HCB.Data.Entity
{
    public class AlarmHistory : IEntity
    {
        public int AlarmId { get; set; } // 알람 식별자
        public AlarmStatus Status { get; set; } // 알람 상태
        public DateTime CreateAt { get; set; }  // 발생 시간
        public DateTime? ResetTime { get; set; }
        public DateTime? AcknowledgeTime { get; set; }
        public Alarm? Alarm { get; set; } = null;
    }
}
