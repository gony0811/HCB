using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub07ViewModel : ObservableObject 
    {
        private readonly IoDataRepository ioRepository;
        private readonly DeviceManager deviceManager;
        private readonly IOManager ioManager;

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> analogInput = new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> analogOutput= new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> digitalInput= new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> digitalOutput = new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private string searchText = string.Empty;

        public ICollectionView DigitalInputView { get; private set; }
        public ICollectionView DigitalOutputView { get; private set; }

        public USub07ViewModel(IoDataRepository ioDataRepository, DeviceManager deviceManager, IOManager iOManager)
        {
            this.ioRepository = ioDataRepository;
            this.deviceManager = deviceManager;
            this.ioManager = iOManager;

            DigitalInputView = CollectionViewSource.GetDefaultView(DigitalInput);
            DigitalOutputView = CollectionViewSource.GetDefaultView(DigitalOutput);
            DigitalInputView.Filter = IoFilter;
            DigitalOutputView.Filter = IoFilter;

            _ = LoadIoData();
        }

        partial void OnSearchTextChanged(string value)
        {
            DigitalInputView.Refresh();
            DigitalOutputView.Refresh();
        }

        private bool IoFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (obj is not SensorIoItemViewModel item) return false;
            return (item.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.IoName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public async Task LoadIoData()
        {
            var ioList = await ioRepository.ListAsync(x => x.IsEnabled);

            AnalogInput.Clear();
            AnalogOutput.Clear();
            DigitalInput.Clear();
            DigitalOutput.Clear();

            // 출력 신호의 초기 On/Off 상태 확인용 (하드웨어 캐시 값 기준)
            var ioDevice = deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            foreach (var group in ioList.GroupBy(x => x.IoDataType))
            {
                foreach (var io in group)
                {
                    switch (group.Key)
                    {
                        case IoType.AnalogInput:
                            var ai = ioManager.CreateIoVM(io.Address, io.Name, "", io.Description, true);
                            if(ai != null) AnalogInput.Add(ai);

                            break;
                        case IoType.AnalogOutput:
                            var ao = ioManager.CreateIoVM(io.Address, io.Name, "", io.Description);
                            if (ao != null) AnalogOutput.Add(ao);
                            break;
                        case IoType.DigitalInput:
                            var di = ioManager.CreateIoVM(io.Address, io.Name, "", io.Description, true);
                            if (di != null) DigitalInput.Add(di);
                            break;
                        case IoType.DigitalOutput:
                            var dio = ioManager.CreateIoVM(io.Address, io.Name, "", io.Description);
                            if (dio != null)
                            {
                                // 프로그램 시작 시 출력 신호의 현재 On/Off 상태를 확인 후 토글에 반영
                                if (ioDevice != null)
                                    dio.IsChecked = ioDevice.GetDigital(io.Name);
                                DigitalOutput.Add(dio);
                            }
                            break;
                    }
                }
            }
        }

    }
}
