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
        [ObservableProperty] private TracingMode tracingMode = TracingMode.Auto;
        [ObservableProperty] private bool fiducialTracing = false;
        [ObservableProperty] private bool btmIndividualMeasure = false;

        // ── CSV 저장 설정 ─────────────────────────────────────
        [ObservableProperty] private string csvVernierDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "결과 데이터");
        [ObservableProperty] private string csvVernierFileName = "버니어 측정 데이터_{date}.csv";
        [ObservableProperty] private string csvDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "HCB", "데이터");
        [ObservableProperty] private string csvDataFileName = "bonding_hcb_{date}.csv";

        [RelayCommand]
        public void ChangeAvgMode() => AvgMode = !AvgMode;

        [RelayCommand]
        public void Change2DMapping() => Use2DMapping = !Use2DMapping;

        [RelayCommand]
        public void ChangeMeasureVernier() => MeasureVernierAfterBonding = !MeasureVernierAfterBonding;

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

        public string ResolveCsvPath(string dir, string fileNamePattern)
        {
            var resolved = fileNamePattern.Replace("{date}", DateTime.Now.ToString("yyyyMMdd"));
            return Path.Combine(dir, resolved);
        }
    }
}
