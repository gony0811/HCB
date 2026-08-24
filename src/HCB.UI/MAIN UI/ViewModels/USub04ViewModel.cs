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
        private readonly VernierLogRepository vernierLogRepository;
        private readonly SettingsViewModel settings;

        // CSV 출력 폴더·파일명 설정 (XAML 바인딩용). 파일명에 {date}, {time} 토큰 사용 가능.
        public SettingsViewModel Settings => settings;

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

        private bool isBondingLoading;
        // =========================================================

        // ================ 버니어 로그 (페이징·검색·CSV) ==========
        [ObservableProperty]
        private ObservableCollection<VernierLog> vernierList = new();

        [ObservableProperty] private int vernierPageSize = 50;
        [ObservableProperty] private int vernierTotalCount;
        [ObservableProperty] private int vernierCurrentPageIndex = 0;

        [ObservableProperty] private DateTime vernierStartDate = DateTime.Now.AddDays(-7);
        [ObservableProperty] private DateTime vernierEndDate = DateTime.Now;

        private bool isVernierLoading;
        // =========================================================

        // ================ HCRO / 카메라 거리 =====================
        [ObservableProperty] private ObservableCollection<CamDistRow> camDistList = new();
        [ObservableProperty] private ObservableCollection<HcroPointRow> hcroList = new();

        [ObservableProperty] private DateTime camHcroStartDate = DateTime.Now.AddDays(-7);
        [ObservableProperty] private DateTime camHcroEndDate = DateTime.Now;

        private bool isCamHcroLoading;
        // =========================================================

        private bool isLoading;

        public USub04ViewModel(
            AlarmService alarmService,
            AlarmHistoryRepository alarmHistoryRepository,
            BondingRecordRepository bondingRecordRepository,
            VernierLogRepository vernierLogRepository,
            SettingsViewModel settings)
        {
            this.alarmService = alarmService;
            this.alarmHistoryRepository = alarmHistoryRepository;
            this.bondingRecordRepository = bondingRecordRepository;
            this.vernierLogRepository = vernierLogRepository;
            this.settings = settings;

            alarmService.AlarmHistoryAdded += OnAlarmHistoryAdded;
            alarmService.AlarmHistoryReset += OnAlarmHistoryReset;
            GridLogSink.LogReceived += OnLogReceived;

            _ = LoadPageData();
            _ = LoadBondingPageAsync();
            _ = LoadVernierPageAsync();
            _ = LoadCamHcroAsync();
        }

        partial void OnCurrentPageIndexChanged(int value)
        {
            _ = LoadPageData();
        }

        partial void OnBondingCurrentPageIndexChanged(int value)
        {
            _ = LoadBondingPageAsync();
        }

        partial void OnVernierCurrentPageIndexChanged(int value)
        {
            _ = LoadVernierPageAsync();
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
        public async Task ExportBondingCsv()
        {
            try
            {
                // 현재 페이지가 아니라 검색 조건(날짜 범위) 전체를 조회해 출력한다.
                var start = BondingStartDate.Date;
                var end = BondingEndDate.Date.AddDays(1).AddTicks(-1);
                var records = await bondingRecordRepository.ListAllWithDetailsAsync(start, end);

                if (records == null || records.Count == 0)
                {
                    MessageBox.Show("출력할 본딩 데이터가 없습니다.", "CSV 출력",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 설정된 저장 폴더·파일명 사용 ({date}/{time} 토큰 치환, 확장자 자동 보정)
                var dir = string.IsNullOrWhiteSpace(settings.CsvBondingQueryDir)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "본딩 조회")
                    : settings.CsvBondingQueryDir;
                Directory.CreateDirectory(dir);

                var pattern = string.IsNullOrWhiteSpace(settings.CsvBondingQueryFileName)
                    ? "Bonding_{date}_{time}.csv"
                    : settings.CsvBondingQueryFileName;
                var path = settings.ResolveCsvPath(dir, pattern);
                if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    path += ".csv";

                var csv = BondingCsvExporter.BuildCsv(records);
                File.WriteAllText(path, csv, new UTF8Encoding(true)); // BOM: Excel 한글 대응
                MessageBox.Show($"CSV 출력 완료 ({records.Count}건)\n{path}",
                    "CSV 출력", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 출력 실패\n{ex.Message}", "CSV 출력",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /* ============================
         * 버니어 로그 (페이징·검색·CSV)
         * ============================ */
        public async Task LoadVernierPageAsync()
        {
            if (isVernierLoading) return;

            try
            {
                isVernierLoading = true;

                var start = VernierStartDate.Date;
                var end = VernierEndDate.Date.AddDays(1).AddTicks(-1);

                var (items, total) = await vernierLogRepository.ListPageAsync(
                    start, end, VernierCurrentPageIndex * VernierPageSize, VernierPageSize);

                VernierTotalCount = total;
                VernierList = new ObservableCollection<VernierLog>(items);
            }
            catch (Exception)
            {
                // 조회 실패 시 조용히 무시 (다른 조회 패턴과 동일)
            }
            finally
            {
                isVernierLoading = false;
            }
        }

        [RelayCommand]
        public async Task VernierSearch()
        {
            VernierCurrentPageIndex = 0;
            await LoadVernierPageAsync();
        }

        [RelayCommand]
        public async Task ExportVernierCsv()
        {
            try
            {
                // 현재 페이지가 아니라 검색 조건(날짜 범위) 전체를 조회해 출력한다.
                var start = VernierStartDate.Date;
                var end = VernierEndDate.Date.AddDays(1).AddTicks(-1);
                var records = await vernierLogRepository.ListAllAsync(start, end);

                if (records == null || records.Count == 0)
                {
                    MessageBox.Show("출력할 버니어 데이터가 없습니다.", "CSV 출력",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dir = string.IsNullOrWhiteSpace(settings.CsvVernierQueryDir)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "버니어 조회")
                    : settings.CsvVernierQueryDir;
                Directory.CreateDirectory(dir);

                var pattern = string.IsNullOrWhiteSpace(settings.CsvVernierQueryFileName)
                    ? "Vernier_{date}_{time}.csv"
                    : settings.CsvVernierQueryFileName;
                var path = settings.ResolveCsvPath(dir, pattern);
                if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    path += ".csv";

                var csv = VernierCsvExporter.BuildCsv(records);
                File.WriteAllText(path, csv, new UTF8Encoding(true)); // BOM: Excel 한글 대응
                MessageBox.Show($"CSV 출력 완료 ({records.Count}건)\n{path}",
                    "CSV 출력", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 출력 실패\n{ex.Message}", "CSV 출력",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /* ============================
         * HCRO / 카메라 거리 (조회·CSV)
         * ============================ */
        public async Task LoadCamHcroAsync()
        {
            if (isCamHcroLoading) return;

            try
            {
                isCamHcroLoading = true;

                var start = CamHcroStartDate.Date;
                var end = CamHcroEndDate.Date.AddDays(1).AddTicks(-1);
                var records = await bondingRecordRepository.ListWithCamHcroAsync(start, end);

                CamDistList = new ObservableCollection<CamDistRow>(
                    records.Where(r => r.CamDist != null).Select(CamDistRow.From));

                HcroList = new ObservableCollection<HcroPointRow>(
                    records.SelectMany(r => r.HcroPoints
                        .OrderBy(p => p.PointIndex)
                        .Select(p => HcroPointRow.From(r.Time, r.Id, p))));
            }
            catch (Exception)
            {
                // 조회 실패 시 조용히 무시 (다른 조회 패턴과 동일)
            }
            finally
            {
                isCamHcroLoading = false;
            }
        }

        [RelayCommand]
        public async Task CamHcroSearch() => await LoadCamHcroAsync();

        [RelayCommand]
        public async Task ExportCamDistCsv()
        {
            try
            {
                var start = CamHcroStartDate.Date;
                var end = CamHcroEndDate.Date.AddDays(1).AddTicks(-1);
                var records = await bondingRecordRepository.ListWithCamHcroAsync(start, end);
                var rows = records.Where(r => r.CamDist != null).Select(CamDistRow.From).ToList();

                if (rows.Count == 0)
                {
                    MessageBox.Show("출력할 카메라 거리 데이터가 없습니다.", "CSV 출력",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var path = ResolveCamHcroPath(settings.CsvCamDistFileName, "CamDist_{date}_{time}.csv");
                File.WriteAllText(path, CamHcroCsvExporter.BuildCamDistCsv(rows), new UTF8Encoding(true));
                MessageBox.Show($"CSV 출력 완료 ({rows.Count}건)\n{path}",
                    "CSV 출력", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 출력 실패\n{ex.Message}", "CSV 출력",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task ExportHcroCsv()
        {
            try
            {
                var start = CamHcroStartDate.Date;
                var end = CamHcroEndDate.Date.AddDays(1).AddTicks(-1);
                var records = await bondingRecordRepository.ListWithCamHcroAsync(start, end);
                var rows = records.SelectMany(r => r.HcroPoints
                    .OrderBy(p => p.PointIndex)
                    .Select(p => HcroPointRow.From(r.Time, r.Id, p))).ToList();

                if (rows.Count == 0)
                {
                    MessageBox.Show("출력할 회전중심 데이터가 없습니다.", "CSV 출력",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var path = ResolveCamHcroPath(settings.CsvHcroFileName, "Hcro_{date}_{time}.csv");
                File.WriteAllText(path, CamHcroCsvExporter.BuildHcroCsv(rows), new UTF8Encoding(true));
                MessageBox.Show($"CSV 출력 완료 ({rows.Count}건)\n{path}",
                    "CSV 출력", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 출력 실패\n{ex.Message}", "CSV 출력",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ResolveCamHcroPath(string fileNamePattern, string defaultPattern)
        {
            var dir = string.IsNullOrWhiteSpace(settings.CsvCamHcroQueryDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "HCRO 조회")
                : settings.CsvCamHcroQueryDir;
            Directory.CreateDirectory(dir);

            var pattern = string.IsNullOrWhiteSpace(fileNamePattern) ? defaultPattern : fileNamePattern;
            var path = settings.ResolveCsvPath(dir, pattern);
            if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                path += ".csv";
            return path;
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

        // 상세 자식 테이블(측정/설비/설정/분석/통합좌표) — 그리드에서 전체 컬럼을 하위 경로로 바인딩한다.
        // Include 로 이미 로드되어 있어 참조만 전달한다. (null 이면 셀은 빈칸)
        public BondingMeasurement? Measurement { get; set; }
        public BondingEquipment? Equipment { get; set; }
        public BondingSetting? Setting { get; set; }
        public BondingAnalysis? Analysis { get; set; }
        public BondingCoordinate? Coordinate { get; set; }

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
            Measurement = r.Measurement,
            Equipment = r.Equipment,
            Setting = r.Setting,
            Analysis = r.Analysis,
            Coordinate = r.Coordinate,
        };
    }

    /// <summary>카메라 거리 측정 표시 행 (본딩 Time·Id + CamDistMeasurement 필드).</summary>
    public class CamDistRow
    {
        public DateTime Time { get; set; }
        public int BondingId { get; set; }
        public double Hc1_StageX { get; set; }
        public double Hc1_StageY { get; set; }
        public double Hc1_DxCam { get; set; }
        public double Hc1_DyCam { get; set; }
        public double Hc1_CenterX { get; set; }
        public double Hc1_CenterY { get; set; }
        public double Hc2_StageX { get; set; }
        public double Hc2_StageY { get; set; }
        public double Hc2_DxCam { get; set; }
        public double Hc2_DyCam { get; set; }
        public double Hc2_CenterX { get; set; }
        public double Hc2_CenterY { get; set; }
        public double Hc2Offset_X { get; set; }
        public double Hc2Offset_Y { get; set; }

        public static CamDistRow From(BondingRecord r) => new CamDistRow
        {
            Time = r.Time,
            BondingId = r.Id,
            Hc1_StageX = r.CamDist.Hc1_StageX,
            Hc1_StageY = r.CamDist.Hc1_StageY,
            Hc1_DxCam = r.CamDist.Hc1_DxCam,
            Hc1_DyCam = r.CamDist.Hc1_DyCam,
            Hc1_CenterX = r.CamDist.Hc1_CenterX,
            Hc1_CenterY = r.CamDist.Hc1_CenterY,
            Hc2_StageX = r.CamDist.Hc2_StageX,
            Hc2_StageY = r.CamDist.Hc2_StageY,
            Hc2_DxCam = r.CamDist.Hc2_DxCam,
            Hc2_DyCam = r.CamDist.Hc2_DyCam,
            Hc2_CenterX = r.CamDist.Hc2_CenterX,
            Hc2_CenterY = r.CamDist.Hc2_CenterY,
            Hc2Offset_X = r.CamDist.Hc2Offset_X,
            Hc2Offset_Y = r.CamDist.Hc2Offset_Y,
        };
    }

    /// <summary>회전중심 측정점 표시 행 (본딩 Time·Id + HcroMeasurementPoint 필드).</summary>
    public class HcroPointRow
    {
        public DateTime Time { get; set; }
        public int BondingId { get; set; }
        public int PointIndex { get; set; }
        public double Angle { get; set; }
        public double Hc1_X { get; set; }
        public double Hc1_Y { get; set; }
        public double Hc2_X { get; set; }
        public double Hc2_Y { get; set; }

        public static HcroPointRow From(DateTime time, int bondingId, HcroMeasurementPoint p) => new HcroPointRow
        {
            Time = time,
            BondingId = bondingId,
            PointIndex = p.PointIndex,
            Angle = p.Angle,
            Hc1_X = p.Hc1_X,
            Hc1_Y = p.Hc1_Y,
            Hc2_X = p.Hc2_X,
            Hc2_Y = p.Hc2_Y,
        };
    }
}