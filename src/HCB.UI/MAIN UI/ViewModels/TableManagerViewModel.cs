using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{

    /// <summary>
    /// 각 테이블에 필요한 데이터들을 관리하는 뷰모델
    /// </summary>

    [ViewModel(Lifetime.Singleton)]
    public partial class TableManagerViewModel : ObservableObject
    {
        private CancellationTokenSource _cancellationTokenSource = new();
        private readonly SequenceService _sequenceService;

        // D-Table 정보
        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new ObservableCollection<SensorIoItemViewModel>();

        private List<string> dTableNameList = new List<string>()
        {
            "DIE 1","DIE 2", "DIE 3", "DIE 4", "DIE 5", "DIE 6", "DIE 7", "DIE 8", "DIE 9",
        };
        public TableManagerViewModel(SequenceService sequenceService)
        {
            this._sequenceService = sequenceService;

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

        [RelayCommand]
        public void DTableLoading()
        {
            Task.Run(async () => { await this._sequenceService.DTableLoading(_cancellationTokenSource.Token); });
        }
    }


}
