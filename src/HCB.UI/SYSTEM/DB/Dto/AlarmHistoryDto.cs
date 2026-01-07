using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;

namespace HCB.UI
{
    public sealed partial class AlarmHistoryDto : ObservableObject
    {
        public int Id { get; set; } // 히스토리 아이디

        public string Code { get; set; }

        public string Name { get; set; }

        [ObservableProperty]
        private AlarmLevel level;

        [ObservableProperty]
        private AlarmStatus status;

        [ObservableProperty]
        private DateTime createDate;

        [ObservableProperty]
        private TimeSpan? createTime;

        [ObservableProperty]
        private DateTime? acknowledgeTime;

        [ObservableProperty]
        private DateTime? resetTime;


        public AlarmHistoryDto()
        {
        }

        public static AlarmHistoryDto ToDTO(AlarmHistory history)
        {
            return new AlarmHistoryDto
            {
                Id = history.Id,
                Name = history.Alarm.Name,
                Level = history.Alarm.Level,
                Status = history.Status,
                CreateDate = history.CreateAt.Date,
                CreateTime = history.CreateAt.TimeOfDay,
                AcknowledgeTime = history.AcknowledgeTime,
                ResetTime = history.ResetTime
            };
        }
    }
}
