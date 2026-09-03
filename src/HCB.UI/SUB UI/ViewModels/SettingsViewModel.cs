using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.IO;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] private bool avgMode = true;
        [ObservableProperty] private bool use2DMapping = true;
        [ObservableProperty] private bool measureVernierAfterBonding = false;
        [ObservableProperty] private bool reMeasureAfterCorr = false;   // 보정 후 P-TABLE 재측정 + 재보정
        [ObservableProperty] private TracingMode tracingMode = TracingMode.Manual;
        [ObservableProperty] private bool fiducialTracing = true;
        [ObservableProperty] private bool btmIndividualMeasure = true;
        [ObservableProperty] private bool rightFidSimilarity = false;   // 우측 피듀셜 닮음변환 보정
        [ObservableProperty] private bool fidCenterAlign = false;       // 피듀셜 중심 기준 강체 정렬

        // ── CSV 저장 설정 ─────────────────────────────────────
        [ObservableProperty] private string csvVernierDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "결과 데이터");
        [ObservableProperty] private string csvVernierFileName = "버니어 측정 데이터_{date}.csv";
        [ObservableProperty] private string csvDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "데이터");
        [ObservableProperty] private string csvDataFileName = "bonding_hcb_{date}.csv";

        // 본딩 정보 조회 화면(USub04) CSV 출력 설정
        [ObservableProperty] private string csvBondingQueryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "본딩 조회");
        [ObservableProperty] private string csvBondingQueryFileName = "Bonding_{date}_{time}.csv";

        // 버니어 조회 화면(USub04) CSV 출력 설정
        [ObservableProperty] private string csvVernierQueryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "버니어 조회");
        [ObservableProperty] private string csvVernierQueryFileName = "Vernier_{date}_{time}.csv";

        // HCRO/카메라거리 조회 화면(USub04) CSV 출력 설정
        [ObservableProperty] private string csvCamHcroQueryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "HCRO 조회");
        [ObservableProperty] private string csvCamDistFileName = "CamDist_{date}_{time}.csv";
        [ObservableProperty] private string csvHcroFileName = "Hcro_{date}_{time}.csv";

        [RelayCommand]
        public void ChangeAvgMode() => AvgMode = !AvgMode;

        [RelayCommand]
        public void Change2DMapping() => Use2DMapping = !Use2DMapping;

        [RelayCommand]
        public void ChangeMeasureVernier() => MeasureVernierAfterBonding = !MeasureVernierAfterBonding;

        [RelayCommand]
        public void ChangeReMeasure() => ReMeasureAfterCorr = !ReMeasureAfterCorr;

        [RelayCommand]
        public void CycleTracingMode()
        {
            TracingMode = TracingMode switch
            {
                TracingMode.Auto => TracingMode.Manual,
                TracingMode.Manual => TracingMode.None,
                _ => TracingMode.Auto
            };
        }

        [RelayCommand]
        public void ChangeBtmMeasureMode() => BtmIndividualMeasure = !BtmIndividualMeasure;

        [RelayCommand]
        public void ChangeFiducialTracking() => FiducialTracing = !FiducialTracing;

        [RelayCommand]
        public void ChangeRightFidSimilarity() => RightFidSimilarity = !RightFidSimilarity;

        [RelayCommand]
        public void ChangeFidCenterAlign() => FidCenterAlign = !FidCenterAlign;

        [RelayCommand]
        private void BrowseVernierDir()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Vernier CSV 저장 폴더 선택",
                InitialDirectory = Directory.Exists(CsvVernierDir) ? CsvVernierDir : ""
            };
            if (dlg.ShowDialog() == true)
                CsvVernierDir = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseDataDir()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "본딩 데이터 CSV 저장 폴더 선택",
                InitialDirectory = Directory.Exists(CsvDataDir) ? CsvDataDir : ""
            };
            if (dlg.ShowDialog() == true)
                CsvDataDir = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseBondingQueryDir()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "본딩 조회 CSV 저장 폴더 선택",
                InitialDirectory = Directory.Exists(CsvBondingQueryDir) ? CsvBondingQueryDir : ""
            };
            if (dlg.ShowDialog() == true)
                CsvBondingQueryDir = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseVernierQueryDir()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "버니어 조회 CSV 저장 폴더 선택",
                InitialDirectory = Directory.Exists(CsvVernierQueryDir) ? CsvVernierQueryDir : ""
            };
            if (dlg.ShowDialog() == true)
                CsvVernierQueryDir = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseCamHcroQueryDir()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "HCRO/카메라거리 CSV 저장 폴더 선택",
                InitialDirectory = Directory.Exists(CsvCamHcroQueryDir) ? CsvCamHcroQueryDir : ""
            };
            if (dlg.ShowDialog() == true)
                CsvCamHcroQueryDir = dlg.FolderName;
        }

        public string ResolveCsvPath(string dir, string fileNamePattern)
        {
            var resolved = fileNamePattern
                .Replace("{date}", DateTime.Now.ToString("yyyyMMdd"))
                .Replace("{time}", DateTime.Now.ToString("HHmmss"));
            return Path.Combine(dir, resolved);
        }
    }
}
