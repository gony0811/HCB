using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Repository;
using HCB.IoC;
using HCB.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class USub04ViewModel : ObservableObject, IDisposable
    {
        private readonly AlarmHistoryRepository alarmHistoryRepository;
        private readonly AlarmService alarmService;
        private readonly BondingRecordRepository bondingRecordRepository;

        [ObservableProperty]
        private ObservableCollection<AlarmHistoryDto> alarmHistoryList = new();

        [ObservableProperty]
        private AlarmHistoryDto selectedHistory;

        // ================ 시스템 로그 ============================
        [ObservableProperty]
        private ObservableCollection<LogModel> logs = new();

        private const int MaxLogCount = 5000;
        // =========================================================

        // ================ 페이징 및 검색 =========================
        [ObservableProperty] private int pageSize = 20;
        [ObservableProperty] private int totalCount;
        [ObservableProperty] private int currentPageIndex = 0;

        [ObservableProperty] private DateTime startSearchDate = DateTime.Now.AddDays(-7); // 기본값 일주일 전
        [ObservableProperty] private DateTime endSearchDate = DateTime.Now;
        [ObservableProperty] private string searchText = string.Empty;
        // =========================================================

        // ================ 본딩 정보 (페이징·검색·CSV) ============
        [ObservableProperty]
        private ObservableCollection<BondingRecordRow> bondingList = new();

        [ObservableProperty] private int bondingPageSize = 50;          // 50개 초과 시 다음 페이지
        [ObservableProperty] private int bondingTotalCount;
        [ObservableProperty] private int bondingCurrentPageIndex = 0;

        [ObservableProperty] private DateTime bondingStartDate = DateTime.Now.AddDays(-7);
        [ObservableProperty] private DateTime bondingEndDate = DateTime.Now;

        // 현재 페이지의 전체 상세(자식 포함) — CSV 출력에 사용
        private IReadOnlyList<BondingRecord> bondingPageRecords = Array.Empty<BondingRecord>();
        private bool isBondingLoading;
        // =========================================================

        private bool isLoading;

        public USub04ViewModel(
            AlarmService alarmService,
            AlarmHistoryRepository alarmHistoryRepository,
            BondingRecordRepository bondingRecordRepository)
        {
            this.alarmService = alarmService;
            this.alarmHistoryRepository = alarmHistoryRepository;
            this.bondingRecordRepository = bondingRecordRepository;

            alarmService.AlarmHistoryAdded += OnAlarmHistoryAdded;
            alarmService.AlarmHistoryReset += OnAlarmHistoryReset;
            GridLogSink.LogReceived += OnLogReceived;

            _ = LoadPageData();
            _ = LoadBondingPageAsync();
        }

        partial void OnCurrentPageIndexChanged(int value)
        {
            _ = LoadPageData();
        }

        partial void OnBondingCurrentPageIndexChanged(int value)
        {
            _ = LoadBondingPageAsync();
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
         * 본딩 정보 (페이징·검색·CSV)
         * ============================ */
        public async Task LoadBondingPageAsync()
        {
            if (isBondingLoading) return;

            try
            {
                isBondingLoading = true;

                var start = BondingStartDate.Date;
                var end = BondingEndDate.Date.AddDays(1).AddTicks(-1);   // 종료일 전체 포함

                var (items, total) = await bondingRecordRepository.ListPageWithDetailsAsync(
                    start, end, BondingCurrentPageIndex * BondingPageSize, BondingPageSize);

                bondingPageRecords = items;
                BondingTotalCount = total;
                BondingList = new ObservableCollection<BondingRecordRow>(
                    items.Select(BondingRecordRow.From));
            }
            catch (Exception)
            {
                // 조회 실패 시 조용히 무시 (알람 조회 패턴과 동일)
            }
            finally
            {
                isBondingLoading = false;
            }
        }

        [RelayCommand]
        public async Task BondingSearch()
        {
            BondingCurrentPageIndex = 0; // 검색 시 첫 페이지로
            await LoadBondingPageAsync();
        }

        [RelayCommand]
        public void ExportBondingCsv()
        {
            if (bondingPageRecords == null || bondingPageRecords.Count == 0)
            {
                MessageBox.Show("출력할 본딩 데이터가 없습니다.", "CSV 출력",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "본딩 데이터 CSV 출력 (현재 페이지)",
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = $"Bonding_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var csv = BondingCsvExporter.BuildCsv(bondingPageRecords);
                File.WriteAllText(dlg.FileName, csv, new UTF8Encoding(true)); // BOM: Excel 한글 대응
                MessageBox.Show($"CSV 출력 완료 ({bondingPageRecords.Count}건)\n{dlg.FileName}",
                    "CSV 출력", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 출력 실패\n{ex.Message}", "CSV 출력",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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

        /* ============================
         * 시스템 로그 수신
         * ============================ */
        private void OnLogReceived(LogModel log)
        {
            if (log.SourceContext?.Contains("Microsoft.EntityFrameworkCore") == true)
                return;
            if (log.Message.StartsWith("Executed DbCommand")
                || log.Message.StartsWith("Executing DbCommand"))
                return;

            App.Current.Dispatcher.InvokeAsync(() =>
            {
                Logs.Insert(0, log);
                if (Logs.Count > MaxLogCount)
                    Logs.RemoveAt(Logs.Count - 1);
            });
        }

        public void Dispose()
        {
            alarmService.AlarmHistoryAdded -= OnAlarmHistoryAdded;
            alarmService.AlarmHistoryReset -= OnAlarmHistoryReset;
            GridLogSink.LogReceived -= OnLogReceived;
        }
    }

    /// <summary>본딩 정보 그리드 표시용 요약 행 (마스터 + 결과 핵심 필드).</summary>
    public class BondingRecordRow
    {
        public int Id { get; set; }
        public DateTime Time { get; set; }
        public string Kind { get; set; } = "";
        public int? ParentRecordId { get; set; }
        public bool AvgMode { get; set; }
        public bool Use2DMapping { get; set; }
        public string TracingMode { get; set; } = "";
        public bool UseBtmIndividualMeasure { get; set; }
        public bool UseFiducialTracking { get; set; }
        public bool UseRightFidSimilarity { get; set; }
        public double ResultX { get; set; }
        public double ResultY { get; set; }
        public double ResultT { get; set; }
        public double? VernierOffsetX { get; set; }
        public double? VernierOffsetY { get; set; }
        public double? VernierOffsetT { get; set; }

        public static BondingRecordRow From(BondingRecord r) => new BondingRecordRow
        {
            Id = r.Id,
            Time = r.Time,
            Kind = r.Kind.ToString(),
            ParentRecordId = r.ParentRecordId,
            AvgMode = r.AvgMode,
            Use2DMapping = r.Use2DMapping,
            TracingMode = r.TracingMode,
            UseBtmIndividualMeasure = r.UseBtmIndividualMeasure,
            UseFiducialTracking = r.UseFiducialTracking,
            UseRightFidSimilarity = r.UseRightFidSimilarity,
            ResultX = r.Result?.ResultX ?? 0,
            ResultY = r.Result?.ResultY ?? 0,
            ResultT = r.Result?.ResultT ?? 0,
            VernierOffsetX = r.Result?.Vernier_OffsetX,
            VernierOffsetY = r.Result?.Vernier_OffsetY,
            VernierOffsetT = r.Result?.Vernier_OffsetT,
        };
    }
}