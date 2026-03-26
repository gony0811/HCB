using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class RunInformation : ObservableObject
    {
        [ObservableProperty] private string operatorId;
        [ObservableProperty] private string lotId;
        [ObservableProperty] private int waferSize;

        [ObservableProperty] private ObservableCollection<int> topDieList;
        [ObservableProperty] private ObservableCollection<int> bottomDieList;

        // Count 프로퍼티 (읽기 전용)
        public int TopDieCount => TopDieList?.Count ?? 0;
        public int BottomDieCount => BottomDieList?.Count ?? 0;

        public RunInformation()
        {
            OperatorId = string.Empty;
            LotId = string.Empty;
            WaferSize = 1;

            TopDieList = new ObservableCollection<int>();
            BottomDieList = new ObservableCollection<int>();

            // 컬렉션 변경 시 Count 알림
            TopDieList.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TopDieCount));
            BottomDieList.CollectionChanged += (_, _) => OnPropertyChanged(nameof(BottomDieCount));
        }

        // TopDieList 자체가 교체될 때도 대응
        partial void OnTopDieListChanged(ObservableCollection<int> value)
        {
            if (value != null)
                value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TopDieCount));

            OnPropertyChanged(nameof(TopDieCount));
        }

        partial void OnBottomDieListChanged(ObservableCollection<int> value)
        {
            if (value != null)
                value.CollectionChanged += (_, _) => OnPropertyChanged(nameof(BottomDieCount));

            OnPropertyChanged(nameof(BottomDieCount));
        }
    }
}