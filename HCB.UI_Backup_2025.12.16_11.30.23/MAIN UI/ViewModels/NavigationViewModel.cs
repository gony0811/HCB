using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        [ObservableProperty] private bool deviceEnabled = true;

        private readonly Dictionary<string, Action<bool>> _permSetters;

        public NavigationViewModel()
        {

            _permSetters = new Dictionary<string, Action<bool>>
            {
                ["MAIN"] = v => MainEnabled = v,
                ["PARAMETER"] = v => ParameterEnabled = v,
                ["USER"] = v => UserEnabled = v,
                ["LOG"] = v => LogEnabled = v,
                ["AlARM"] = v => AlarmEnabled = v,
                ["MOTION"] = v => MotionEnabled = v,
                ["IO"] = v => IOEnabled = v,
                ["Device"] = v => deviceEnabled = v,
            };
        }

        public async Task LoadNavigation(CancellationToken ct = default)
        {

            SetEnabled(false);
        }

        public void ApplyScreens(IReadOnlyCollection<string> allowed)
        {
            // 일단 모두 false
            SetEnabled(false);

            // 허용된 것만 true
            foreach (var kv in _permSetters)
                kv.Value(allowed.Contains(kv.Key));
        }

        public void SetEnabled(bool onOff)
        {
            MainEnabled = onOff;
            ParameterEnabled = onOff;
            UserEnabled = onOff;
            LogEnabled = onOff;
            AlarmEnabled = onOff;
            MotionEnabled = onOff;
            IOEnabled = onOff;
            DeviceEnabled = onOff;
        }
    }
}
