using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        /* ============================
         * Events
         * ============================ */
        public event Action<AlarmHistoryDto>? AlarmHistoryAdded;
        public event Action<int>? AlarmHistoryReset; // historyId

        /* ============================
         * Alarm 발생 (Set)
         * ============================ */
        public async Task SetAlarm(int id)
        {
            var alarm = await alarmRepository.FindAsync(keyValues: id);
            if (alarm != null) await ProcessSetAlarm(alarm);
        }

        public async Task SetAlarm(string code)
        {
            var alarm = await alarmRepository.FindAsync(x => x.Code == code);
            if (alarm != null) await ProcessSetAlarm(alarm);
        }

        // 중복 로직 처리를 위한 내부 메서드
        private async Task ProcessSetAlarm(Alarm alarm)
        {
            var entity = new AlarmHistory
            {
                AlarmId = alarm.Id,
                Status = AlarmStatus.SET,
                CreateAt = DateTime.Now
            };

            entity = await alarmHistoryRepository.AddAsync(entity);
            entity.Alarm = alarm;

            AlarmHistoryAdded?.Invoke(AlarmHistoryDto.ToDTO(entity));
            UpdateEQStatus(alarm.Level);
        }

        /* ============================
         * Reset (단일 및 전체)
         * ============================ */

        // 단일 알람 해제
        public async Task ResetAlarm(int historyId)
        {
            var entity = await alarmHistoryRepository.FindAsync(keyValues: historyId);
            if (entity == null) return;

            entity.Status = AlarmStatus.RESET;
            entity.ResetTime = DateTime.Now;
            entity.AcknowledgeTime = DateTime.Now;

            await alarmHistoryRepository.Update(entity);

            AlarmHistoryReset?.Invoke(historyId);
            EQStatus.Alarm = AlarmState.NO_ALARM;
        }

        // 모든 알람 일괄 해제 (추가된 기능)
        public async Task ResetAllAlarms()
        {
            // 현재 SET 상태인 모든 히스토리 조회
            var activeAlarms = await alarmHistoryRepository.ListAsync(
                predicate: x => x.Status == AlarmStatus.SET);

            if (!activeAlarms.Any()) return;

            var now = DateTime.Now;
            foreach (var entity in activeAlarms)
            {
                entity.Status = AlarmStatus.RESET;
                entity.ResetTime = now;
                entity.AcknowledgeTime = now;
            }

            // DB 일괄 업데이트 (성능 최적화)
            await alarmHistoryRepository.UpdateRange(activeAlarms);

            // UI 통지를 위해 개별 이벤트 발생
            foreach (var entity in activeAlarms)
            {
                AlarmHistoryReset?.Invoke(entity.Id);
            }

            EQStatus.Alarm = AlarmState.NO_ALARM;
        }

        /* ============================
         * 조회 및 Paging
         * ============================ */
        public async Task<ObservableCollection<AlarmHistoryDto>> GetAlarmHistoryList(
            int pageNumber = 1,
            int pageSize = 50)
        {
            int skip = (pageNumber - 1) * pageSize;

            var histories = await alarmHistoryRepository.ListAsync(
                include: i => i.Include(x => x.Alarm),
                orderBy: q => q.OrderByDescending(x => x.CreateAt),
                skip: skip,
                take: pageSize);

            return new ObservableCollection<AlarmHistoryDto>(
                histories.Select(AlarmHistoryDto.ToDTO));
        }

        // 전체 알람 설정 리스트 조회
        public async Task<IReadOnlyList<Alarm>> GetAlarmList(Sort sort = Sort.Ascending)
        {
            if (sort == Sort.Ascending)
                return await alarmRepository.ListAsync(orderBy: q => q.OrderBy(a => a.Code));

            return await alarmRepository.ListAsync(orderBy: q => q.OrderByDescending(a => a.Code));
        }

        /* ============================
         * CRUD Operations
         * ============================ */
        public async Task AddAlarm(List<Alarm> alarmList) => await alarmRepository.AddRangeAsync(alarmList);
        public async Task UpdateAlarm(Alarm alarm) => await alarmRepository.Update(alarm);
        public async Task UpdateAlarm(IEnumerable<Alarm> alarms) => await alarmRepository.UpdateRange(alarms);
        public async Task DeleteAlarm(int alarmId) => await alarmRepository.Remove(alarmId);
        public async Task DiscardChangesAsync(CancellationToken ct = default) => await alarmRepository.DiscardChangesAsync(ct);

        /* ============================
         * Helper Methods
         * ============================ */
        private void UpdateEQStatus(AlarmLevel level)
        {
            EQStatus.Alarm = level switch
            {
                AlarmLevel.HEAVY => AlarmState.HEAVY,
                AlarmLevel.LIGHT => AlarmState.LIGHT,
                _ => AlarmState.NO_ALARM
            };
        }
    }
}