using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;
using HCB.IoC;
using HCB.Data.Entity.Type;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub05ViewModel : ObservableObject
    {

        private readonly AlarmService _alarmService;
        private readonly DialogService _dialogService;

        [ObservableProperty] private ObservableCollection<AlarmDto> alarmList = new ObservableCollection<AlarmDto>();

        [ObservableProperty] private AlarmDto selectedAlarm;

        [ObservableProperty] private bool isBusy;
        public USub05ViewModel(AlarmService alarmService, DialogService dialogService)
        {
            _alarmService = alarmService;
            _dialogService = dialogService;
            Initialize();
        }

        public async Task Initialize()
        {
            AlarmList.Clear();

            var entities = await _alarmService.GetAlarmList();
            var dtos = entities.Select(AlarmDto.From).ToList();

            foreach (var dto in dtos)
                AlarmList.Add(dto);

            SelectedAlarm = AlarmList.First(null);
        }

        // 생성 
        [RelayCommand]
        public void CreateAlarm()
        {
            AlarmDto alarm = new AlarmDto
            {
                Name = "Undefined Alarm",
                Code = "E0000",
                Enabled = true,
                Level = AlarmLevel.LIGHT,
                IsModified = true
            };

            AlarmList.Add(alarm);
            SelectedAlarm = alarm;
        }

        // 저장
        [RelayCommand]
        public async Task SaveAlarm(CancellationToken ct = default)
        {
            try
            {
                // 1. 변경된 항목만 필터링 (가독성을 위해 수정)
                var modifiedItems = AlarmList.Where(a => a.IsModified).ToList();

                if (!modifiedItems.Any()) return;

                // 2. 신규/수정 분리
                var toAddModels = modifiedItems.Where(a => a.Id == 0).ToList();
                var toUpdateModels = modifiedItems.Where(a => a.Id > 0).ToList();

                // 3. 서비스 호출 (트랜잭션 처리가 되어있다고 가정하거나 통합 메서드 권장)
                // 만약 서비스에서 List<Entity>를 받는다면:
                if (toAddModels.Any())
                {
                    var newEntities = toAddModels.Select(a => a.ToEntity()).ToList();
                    await _alarmService.AddAlarm(newEntities);

                    // 중요: DB에서 생성된 ID를 ViewModel에 다시 반영
                    for (int i = 0; i < toAddModels.Count; i++)
                    {
                        toAddModels[i].Id = newEntities[i].Id;
                    }
                }

                if (toUpdateModels.Any())
                {
                    var updateEntities = toUpdateModels.Select(a => a.ToEntity()).ToList();
                    await _alarmService.UpdateAlarm(updateEntities);
                }

                // 4. 저장에 성공한 항목들만 상태 초기화
                foreach (var alarm in modifiedItems)
                {
                    alarm.IsModified = false;
                }

                _dialogService.ShowMessage("저장", "알람이 성공적으로 저장되었습니다.");
            }
            catch (OperationCanceledException)
            {
                // 작업 취소 시 처리 (선택 사항)
            }
            catch(DbUpdateException e)
            {
                _dialogService.ShowMessage("중복 코드", "중복되는 코드가 있습니다. \n 확인해주세요");
            }
            catch (Exception e)
            {
                _dialogService.ShowMessage("저장 실패", $"오류 발생: {e.Message}");
            }
        }

        [RelayCommand]
        public async Task Discard(CancellationToken ct = default)
        {
            try
            {
                AlarmList.Clear();

                var freshEntities = await _alarmService.GetAlarmList();
                var freshDtos = freshEntities.Select(AlarmDto.From).ToList();

                foreach (var dto in freshDtos)
                    AlarmList.Add(dto);

                SelectedAlarm = null;

                if (AlarmList.Count > 0)
                {
                    SelectedAlarm = AlarmList.First();
                }
                

                _dialogService.ShowMessage("되돌리기", "모든 변경사항이 취소되었습니다");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage("오류", $"되돌리기 중 오류 발생:\n{ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SetAlarm()
        {
            await _alarmService.SetAlarm(1);
        }

    }
}
