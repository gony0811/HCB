using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;

namespace HCB.UI
{
    public sealed class AlarmHistoryDto
    {
        public long Id { get; set; }
        public int AlarmId { get; set; }
        public AlarmLevel Level { get; set; }
        public AlarmStatus Status { get; set; }
        public DateTime UpdateTime { get; set; }

        private AlarmHistoryDto()
        {
        }
        private AlarmHistoryDto(long id, int alarmId, AlarmLevel level, AlarmStatus status, DateTime updateTime)
        {
            Id = id;
            AlarmId = alarmId;
            Level = level;
            Status = status;
            UpdateTime = updateTime;
        }

        public static AlarmHistoryDto of(AlarmHistory history)
        {
            return new AlarmHistoryDto
            {
                Id = history.Id,
                AlarmId = history.AlarmId,
                Level = history.Level,
                Status = history.Status,
                UpdateTime = history.UpdateTime
            };
        }
    }
}
