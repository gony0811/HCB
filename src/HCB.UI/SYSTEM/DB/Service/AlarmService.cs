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
        private readonly OperationService operationService;
        private readonly AlarmRepository alarmRepository;
        private readonly AlarmHistoryRepository alarmHistoryRepository;

        private ObservableCollection<Alarm> alarmList = new ObservableCollection<Alarm>();

        private ObservableCollection<Alarm> currentAlarms = new ObservableCollection<Alarm>();

        public AlarmService(AlarmRepository alarmRepository, AlarmHistoryRepository alarmHistoryRepository, OperationService operationService)
        {
            this.alarmRepository = alarmRepository;
            this.alarmHistoryRepository = alarmHistoryRepository;
            this.operationService = operationService;
            LoadAlarms();
        }

        private void LoadAlarms()
        {
            var alarms = alarmRepository.ListAsync().Result;
            alarmList = new ObservableCollection<Alarm>(alarms);
        }

        private Alarm? FindAlarm(int id)
        {
            return alarmList.FirstOrDefault((x) => x.Id == id);
        }

        private Alarm? FindAlarm(string code)
        {
            return alarmList.FirstOrDefault((x) => x.Code == code);
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
            var alarm = FindAlarm(id.ToString());
            if (alarm != null) await ProcessSetAlarm(alarm);
        }

        public async Task SetAlarm(string code)
        {
            var alarm = FindAlarm(code);
            if (alarm != null && alarm.Enable == AlarmEnable.ENABLED) await ProcessSetAlarm(alarm);
        }

        // 중복 로직 처리를 위한 내부 메서드
        private async Task ProcessSetAlarm(Alarm alarm)
        {
            if (currentAlarms.FirstOrDefault(x => x.Code == alarm.Code) != null)
            {
                // 이미 활성화된 알람인 경우 무시
                return;
            }
            else
            {
                currentAlarms.Add(alarm);
            }           

            var history = new AlarmHistory
            {
                AlarmId = alarm.Id,
                Status = AlarmStatus.SET,
                CreateAt = DateTime.Now
            };



            history = await alarmHistoryRepository.AddAsync(history);
            history.Alarm = alarm;

            AlarmHistoryAdded?.Invoke(AlarmHistoryDto.ToDTO(history));
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
            operationService.Status.Alarm = AlarmState.NO_ALARM;
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

            operationService.Status.Alarm = AlarmState.NO_ALARM;
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

        public async Task<ObservableCollection<AlarmHistoryDto>> SearchAlarmHistory(
            DateTime startDate,
            DateTime endDate,
            string searchText = "",
            int pageNumber = 1,
            int pageSize = 50)
        {
            int skip = (pageNumber - 1) * pageSize;

            // 검색 조건 구성
            var histories = await alarmHistoryRepository.ListAsync(
                predicate: x => x.CreateAt >= startDate &&
                                x.CreateAt <= endDate &&
                                (string.IsNullOrEmpty(searchText) || x.Alarm.Name.Contains(searchText) || x.Alarm.Code.Contains(searchText)),
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
        public async Task<int> GetSearchCount(DateTime start, DateTime end, string text)
        {
            return await alarmHistoryRepository.CountAsync(x =>
                x.CreateAt >= start && x.CreateAt <= end &&
                (string.IsNullOrEmpty(text) || x.Alarm.Name.Contains(text)));
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
            var alarm = level switch
            {
                AlarmLevel.HEAVY => AlarmState.HEAVY,
                AlarmLevel.LIGHT => AlarmState.LIGHT,
                _ => AlarmState.NO_ALARM
            };

            var availability = level switch
            {
                AlarmLevel.HEAVY => Availability.Down,
                AlarmLevel.LIGHT => Availability.Up,
                _ => Availability.Down
            };

            operationService.SetAlarm(alarm);
            operationService.SetAvailability(availability);
        }
    }
}