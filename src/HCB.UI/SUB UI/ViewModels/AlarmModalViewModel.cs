using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class AlarmModalViewModel : ObservableObject
    {
        private readonly AlarmService _alarmService;

        // 창 닫기 요청을 위한 액션
        public Action RequestClose { get; set; }

        [ObservableProperty]
        private ObservableCollection<Alarm> alarmList;

        [ObservableProperty]
        private Alarm selectedAlarm;

        public AlarmModalViewModel(AlarmService alarmService)
        {
            _alarmService = alarmService;
        }

        [RelayCommand]
        private async Task ResetAll()
        {
            await _alarmService.ResetAllAlarms();

            // 서비스 로직 완료 후 View에 닫기 요청
            RequestClose?.Invoke();
        }
    }
}