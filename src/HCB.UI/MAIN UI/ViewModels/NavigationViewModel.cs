using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class NavigationViewModel : ObservableObject
    {


        // 화면 활성화 여부 뷰 모델 
        [ObservableProperty] private bool mainEnabled = true;

        [ObservableProperty] private bool parameterEnabled = true;

        [ObservableProperty] private bool userEnabled = true;

        [ObservableProperty] private bool logEnabled = true;

        [ObservableProperty] private bool alarmEnabled = true;

        [ObservableProperty] private bool motionEnabled = true;

        [ObservableProperty] private bool iOEnabled = true;

        [ObservableProperty] private Visibility deviceEnabled = Visibility.Collapsed;

    }
}
