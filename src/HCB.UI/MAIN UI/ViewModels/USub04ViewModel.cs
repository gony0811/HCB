using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Repository;
using HCB.IoC;
using HCB.UI;
using System;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;

[ViewModel(Lifetime.Scoped)]
public partial class USub04ViewModel : ObservableObject, IDisposable
{
    private readonly AlarmHistoryRepository alarmHistoryRepository;
    private readonly AlarmService alarmService;

    [ObservableProperty]
    private ObservableCollection<AlarmHistoryDto> alarmHistoryList = new();

    [ObservableProperty]
    private AlarmHistoryDto selectedHistory;

    [ObservableProperty] private int pageSize = 20;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private int currentPageIndex = 0;

    private bool isLoading;

    public USub04ViewModel(
        AlarmService alarmService,
        AlarmHistoryRepository alarmHistoryRepository)
    {
        this.alarmService = alarmService;
        this.alarmHistoryRepository = alarmHistoryRepository;

        alarmService.AlarmHistoryAdded += OnAlarmHistoryAdded;
        alarmService.AlarmHistoryReset += OnAlarmHistoryReset;

        _ = LoadPageData();
    }

    partial void OnCurrentPageIndexChanged(int value)
    {
        _ = LoadPageData();
    }

    [RelayCommand]
    public async Task HistoryCreate()
    {
        await alarmService.SetAlarm(1);
    }

    [RelayCommand]
    public async Task AllReset()
    {
        await alarmService.ResetAllAlarms();
    }

    public async Task LoadPageData()
    {
        if (isLoading) return;

        try
        {
            isLoading = true;

            TotalCount = await alarmHistoryRepository.CountAsync();
            AlarmHistoryList = await alarmService.GetAlarmHistoryList(
                CurrentPageIndex + 1, PageSize);
        }
        finally
        {
            isLoading = false;
        }
    }

    /* ============================
     * Event Handlers
     * ============================ */
    private void OnAlarmHistoryAdded(AlarmHistoryDto dto)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            TotalCount++;

            // 최신 페이지에서만 실시간 반영
            if (CurrentPageIndex == 0)
            {
                AlarmHistoryList.Insert(0, dto);

                if (AlarmHistoryList.Count > PageSize)
                    AlarmHistoryList.RemoveAt(PageSize);
            }
        });
    }

    private void OnAlarmHistoryReset(int historyId)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var target = AlarmHistoryList.FirstOrDefault(x => x.Id == historyId);
            if (target != null)
            {
                AlarmHistoryList.Remove(target);
                TotalCount--;
            }
        });
    }

    public void Dispose()
    {
        alarmService.AlarmHistoryAdded -= OnAlarmHistoryAdded;
        alarmService.AlarmHistoryReset -= OnAlarmHistoryReset;
    }
}
