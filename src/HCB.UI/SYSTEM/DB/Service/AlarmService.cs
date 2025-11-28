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
