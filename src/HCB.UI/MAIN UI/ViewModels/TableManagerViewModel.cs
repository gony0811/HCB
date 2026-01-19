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
        private readonly DeviceManager _deviceManager;

        [ObservableProperty] private bool isDTableLoading;
        [ObservableProperty] private bool isDTableStandby;

        [ObservableProperty] private bool isWTableLoading;
        [ObservableProperty] private bool isWTableStandby;
        // D-Table 정보
        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> dTableList = new ObservableCollection<SensorIoItemViewModel>();

        private List<string> dTableNameList = new List<string>()
        {
            "DIE 1","DIE 2", "DIE 3", "DIE 4", "DIE 5", "DIE 6", "DIE 7", "DIE 8", "DIE 9",
        };

        private List<string> dIoNameList = new List<string>()
        {
            IoExtensions.DO_DTABLE_VAC_1_ON, IoExtensions.DO_DTABLE_VAC_2_ON, IoExtensions.DO_DTABLE_VAC_3_ON, IoExtensions.DO_DTABLE_VAC_4_ON, IoExtensions.DO_DTABLE_VAC_5_ON, IoExtensions.DO_DTABLE_VAC_6_ON, IoExtensions.DO_DTABLE_VAC_7_ON, IoExtensions.DO_DTABLE_VAC_8_ON, IoExtensions.DO_DTABLE_VAC_9_ON,
        };

        public TableManagerViewModel(SequenceService sequenceService, DeviceManager deviceManager)
        {
            this._sequenceService = sequenceService;
            this._deviceManager = deviceManager;

            var ioDevice = this._deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            for (var i = 0; i < dTableNameList.Count; i++)
            {
                DTableList.Add(new SensorIoItemViewModel(dIoNameList[i], ioDevice, dTableNameList[i]));
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
