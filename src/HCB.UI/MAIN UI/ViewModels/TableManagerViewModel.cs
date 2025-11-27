using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HCB.UI
{

    /// <summary>
    /// 각 테이블에 필요한 데이터들을 관리하는 뷰모델
    /// </summary>

    [ViewModel(Lifetime.Singleton)]
    public partial class TableManagerViewModel : ObservableObject
    {
        // D-Table 정보
        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new ObservableCollection<SensorIoItemViewModel>();

        private List<string> dTableNameList = new List<string>()
        {
            "Zone1 Vacuum","Zone2 Vacuum", "Zone3 Vacuum", "Zone4 Vacuum", "Zone5 Vacuum", "Zone6 Vacuum", "Zone7 Vacuum", "Zone8 Vacuum", "Zone9 Vacuum",
        };
        public TableManagerViewModel()
        {
            foreach (var item in dTableNameList)
            {
                DTableList.Add(new SensorIoItemViewModel(item));
            }
        }

        public int CountDieCarrier()
        {
            int count = 0; 
            foreach (var item in DTableList)
            {
                count += item.IsChecked ? 1 : 0;
            }

            return count;
        }

    }


}
