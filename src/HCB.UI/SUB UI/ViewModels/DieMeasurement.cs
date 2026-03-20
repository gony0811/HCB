using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace HCB.UI
{
    public partial class DieMeasurement : ObservableObject
    {
        [ObservableProperty] private double rightX;
        [ObservableProperty] private double rightXCorrect;
        [ObservableProperty] private double rightXError;
        [ObservableProperty] private double rightY;
        [ObservableProperty] private double rightYCorrect;
        [ObservableProperty] private double rightYError;
        [ObservableProperty] private double leftX;
        [ObservableProperty] private double leftXCorrect;
        [ObservableProperty] private double leftXError;
        [ObservableProperty] private double leftY;
        [ObservableProperty] private double leftYCorrect;
        [ObservableProperty] private double leftYError;
        [ObservableProperty] private double t;

        /// <summary>
        /// Correct 좌표들로부터 계산한 Theta (degree).
        /// Right Correct - Left Correct 벡터의 각도.
        /// </summary>
        public double Theta =>
            Math.Atan2(RightYCorrect - LeftYCorrect,
                       RightXCorrect - LeftXCorrect)
            * (180.0 / Math.PI);

        // Correct 프로퍼티 변경 시 Theta 변경 알림 전파
        partial void OnRightXCorrectChanged(double value) => OnPropertyChanged(nameof(Theta));
        partial void OnRightYCorrectChanged(double value) => OnPropertyChanged(nameof(Theta));
        partial void OnLeftXCorrectChanged(double value) => OnPropertyChanged(nameof(Theta));
        partial void OnLeftYCorrectChanged(double value) => OnPropertyChanged(nameof(Theta));
    }
}
