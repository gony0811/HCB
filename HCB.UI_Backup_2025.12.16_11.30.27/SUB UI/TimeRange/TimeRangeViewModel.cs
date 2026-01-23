using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Timers;

namespace HCB.UI
{
    public partial class TimeRangeViewModel : ObservableObject
    {
        [ObservableProperty] private string startTime;
        [ObservableProperty] private string endTime;
        [ObservableProperty] private string elapsedTime;

        private Timer timer;
        private DateTime internalStartTime;
        private DateTime internalEndTime;

        public TimeRangeViewModel()
        {
            StartTime = "00:00:00";
            EndTime = "00:00:00";
            ElapsedTime = "00:00:00";
        }

        [RelayCommand]
        public void StartTimer()
        {
            if (timer != null && timer.Enabled)
            {
                return; // 타이머가 이미 실행 중이면 아무 작업도 하지 않음
            }

            internalStartTime = DateTime.Now;
            StartTime = internalStartTime.ToString("HH:mm:ss");
            timer = new Timer(1000); // 1초 간격
            timer.Elapsed += (s, e) =>
            {
                internalEndTime = DateTime.Now;
                EndTime = internalEndTime.ToString("HH:mm:ss");
                TimeSpan elapsed = internalEndTime - internalStartTime;
                ElapsedTime = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                OnPropertyChanged(nameof(StartTime));
                OnPropertyChanged(nameof(EndTime));
                OnPropertyChanged(nameof(ElapsedTime));
            };
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        [RelayCommand]
        public void StopTimer()
        {
            timer?.Stop();
            timer = null;
        }

        [RelayCommand]
        public void PauseTimer()
        {
            timer?.Stop();
        }

        [RelayCommand]
        public void ResumeTimer()
        {
            timer?.Start();
        }
    }
}
