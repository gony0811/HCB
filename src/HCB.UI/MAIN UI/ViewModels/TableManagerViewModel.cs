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
            "Vac. 1","Vac. 2", "Vac. 3", "Vac. 4", "Vac. 5", "Vac. 6", "Vac. 7", "Vac. 8", "Vac. 9",
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
