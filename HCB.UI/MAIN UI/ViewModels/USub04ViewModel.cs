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

    // ================ 페이징 및 검색 =========================
    [ObservableProperty] private int pageSize = 20;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private int currentPageIndex = 0;

    [ObservableProperty] private DateTime startSearchDate = DateTime.Now.AddDays(-7); // 기본값 일주일 전
    [ObservableProperty] private DateTime endSearchDate = DateTime.Now;
    [ObservableProperty] private string searchText = string.Empty;
    // ========================================================= 

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

            // 종료 날짜의 시간을 23:59:59로 설정하여 해당 날짜 전체를 포함
            var endDateTime = EndSearchDate.Date.AddDays(1).AddTicks(-1);
            var startDateTime = StartSearchDate.Date;

            TotalCount = await alarmService.GetSearchCount(startDateTime, endDateTime, SearchText);
            AlarmHistoryList = await alarmService.SearchAlarmHistory(
                startDateTime, endDateTime, SearchText, CurrentPageIndex + 1, PageSize);
        }
        catch (Exception ex)
        {
            // 로깅 추가
        }
        finally
        {
            isLoading = false;
        }
    }

    [RelayCommand]
    public async Task Search()
    {
        CurrentPageIndex = 0; // 검색 시 첫 페이지로 이동
        await LoadPageData();
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
