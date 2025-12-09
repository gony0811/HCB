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

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub05ViewModel : ObservableObject
    {

        private readonly AlarmService _alarmService;


        [ObservableProperty] private ObservableCollection<AlarmDto> alarmList = new ObservableCollection<AlarmDto>();

        [ObservableProperty] private AlarmDto selectedAlarm;

        [ObservableProperty] private bool isBusy;
        public USub05ViewModel(AlarmService alarmService)
        {
            _alarmService = alarmService;

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
                Enabled = true,
                Level = AlarmLevel.Light,
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
                // 신규 항목 (Id == 0)
                var toAdd = AlarmList
                    .Where(a => a.Id == 0 && a.IsModified)
                    .Select(a => a.ToEntity())
                    .ToList();

                // 수정된 항목 (Id > 0)
                var toUpdate = AlarmList
                    .Where(a => a.Id > 0 && a.IsModified)
                    .Select(a => a.ToEntity())
                    .ToList();

                if (!toAdd.Any() && !toUpdate.Any())
                {
                    AlertModal.Ask(GetOwnerWindow(), "저장", "변경된 내용이 없습니다.");
                    return;
                }

                // 신규 저장
                if (toAdd.Any())
                    await _alarmService.AddAlarm(toAdd);

                // 기존 항목 수정
                if (toUpdate.Any())
                    await _alarmService.UpdateAlarm(toUpdate);

                // 상태 초기화
                foreach (var alarm in AlarmList)
                    alarm.IsModified = false;

                AlertModal.Ask(GetOwnerWindow(), "저장", "저장되었습니다.");
            }
            catch (Exception e)
            {
                AlertModal.Ask(GetOwnerWindow(), "저장 실패", $"저장 중 오류 발생:\n{e.Message}");
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
                SelectedAlarm = AlarmList.First(null);
                AlertModal.Ask(GetOwnerWindow(), "되돌리기", "모든 변경사항이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                AlertModal.Ask(GetOwnerWindow(), "오류", $"되돌리기 중 오류 발생:\n{ex.Message}");
            }
        }


        //private static void CommitPendingEdits(IList<Alarm> items)
        //{
        //    var view = CollectionViewSource.GetDefaultView(items);
        //    if (view is IEditableCollectionView v)
        //    {
        //        if (v.IsEditingItem) v.CommitEdit();
        //        if (v.IsAddingNew) v.CommitNew();
        //    }
        //}

        private static Window? GetOwnerWindow() =>
            Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? Application.Current?.MainWindow;
    }
}
