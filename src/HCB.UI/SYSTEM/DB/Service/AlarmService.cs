using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public partial class AlarmService : ObservableObject
    {
        private readonly AlarmRepository alarmRepository;
        private readonly AlarmHistoryRepository alarmHistoryRepository;

        public AlarmService(AlarmRepository alarmRepository, AlarmHistoryRepository alarmHistoryRepository)
        {
            this.alarmRepository = alarmRepository;
            this.alarmHistoryRepository = alarmHistoryRepository;
        }

        public async Task SetAlarm(string alarmCode)
        {
            var alarms = await this.GetAlarmList();

            if (alarms == null) return;

            var alarm = alarms.FirstOrDefault(a => a.Code == alarmCode);

            if (alarm == null) return;

            alarm.Status = AlarmStatus.SET;
            await this.UpdateAlarm(alarm);

            if (alarm.Level == AlarmLevel.HEAVY)
            {
                EQStatus.Alarm = AlarmState.LIGHT;
            }
            else if (alarm.Level == AlarmLevel.LIGHT)
            {
                EQStatus.Alarm = AlarmState.LIGHT;
            }
            else
            {
                EQStatus.Alarm = AlarmState.NO_ALARM;
            }

            
        }

        /// <summary>
        /// 알람을 하나만 리셋하는 경우가 있을까?
        /// </summary>
        /// <param name="alarmCode"></param>
        /// <returns></returns>

        //deprecated
        public async Task ResetAlarm(string alarmCode)
        {
            var alarms = await this.GetAlarmList();
            if (alarms == null) return;
            var alarm = alarms.FirstOrDefault(a => a.Code == alarmCode);
            if (alarm == null) return;
            alarm.Status = AlarmStatus.RESET;
            await this.UpdateAlarm(alarm);

            if (alarms.Count == 0)
            {
                EQStatus.Alarm = AlarmState.NO_ALARM;
            }
        }

        public async Task ResetAllAlarms()
        {
            var alarms = await this.GetAlarmList();
            if (alarms == null) return;
            foreach (var alarm in alarms)
            {
                alarm.Status = AlarmStatus.RESET;
            }

            await this.UpdateAlarm(alarms);

            EQStatus.Alarm = AlarmState.NO_ALARM;
        }

        // 전체 알람 조회 ( 정렬 : 레벨순 ) 
        public async Task<IReadOnlyList<Alarm>> GetAlarmList(Sort sort = Sort.Ascending)
        {
            if (sort == Sort.Ascending) return await alarmRepository.ListAsync(orderBy: q => q.OrderBy(a => a.Code));
            
            return await alarmRepository.ListAsync( orderBy: q => q.OrderByDescending(a => a.Code));    
        }

        // 알람 Set 상태 조회
        public async Task<IReadOnlyList<Alarm>> GetSetAlarmList()
        {
            return await alarmRepository.ListAsync(a => a.Status == AlarmStatus.SET, orderBy: q => q.OrderByDescending(a => a.Code));
        }


        // 알람 추가
        public async Task AddAlarm(List<Alarm> alarmList)
        {
            await alarmRepository.AddRangeAsync(alarmList);
        }

        // 알람 수정
        public async Task UpdateAlarm(Alarm alarm)
        {
            await alarmRepository.Update(alarm);
        }

        public async Task UpdateAlarm(IEnumerable<Alarm> alarms)
        {
            await alarmRepository.UpdateRange(alarms);
        }

        // 알람 삭제
        public async Task DeleteAlarm(int alarmId)
        {
            await alarmRepository.Remove(alarmId);
        }

        // 되돌리기 
        public async Task DiscardChangesAsync(CancellationToken ct = default)
        {
            await alarmRepository.DiscardChangesAsync(ct);
        }

        //-------------------------------------------------------------- History ----------------------------------------------------------------//
        //  전체 조회
        public async Task<IReadOnlyList<AlarmHistory>> GetAlarmHistoryList()
        {
            return await alarmHistoryRepository.ListAsync();
        }

        // 조회 _ 시간 범위
        public IReadOnlyList<AlarmHistory> GetAlarmHistoryListByTimeRange(DateTime from, DateTime to)
        {           
            return alarmHistoryRepository.GetAlarmHistoryListByTimeRange(from: from, to: to);
        }


    }
}
