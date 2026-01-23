using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class RunInformation : ObservableObject
    {
        [ObservableProperty] private string operatorId;
        [ObservableProperty] private string lotId;
        [ObservableProperty] private int waferSize;
        [ObservableProperty] private int dieCarrier;          // 다이 수
        [ObservableProperty] private int dieTopSize;
        [ObservableProperty] private int dieBottomSize;
        [ObservableProperty] private int dieTopThickness;
        [ObservableProperty] private int dieBottomThickness;

        // 기본값을 설정하는 기본 생성자
        public RunInformation()
        {
            OperatorId = string.Empty;
            LotId = string.Empty;
            WaferSize = 1;
            DieCarrier = 6;
            DieTopSize = 12;
            DieBottomSize = 13;
            DieTopThickness = 250;
            DieBottomThickness = 250;
        }
    }
}
