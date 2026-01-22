using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using HCB.UI.DEVICE.Core;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

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

        public USub07ViewModel(IoDataRepository ioDataRepository, DeviceManager deviceManager, IOManager iOManager)
        {
            this.ioRepository = ioDataRepository;
            this.deviceManager = deviceManager;
            this.ioManager = iOManager;
            _ = LoadIoData();
                      
        }

        public async Task LoadIoData()
        {
            var ioList = await ioRepository.ListAsync(x => x.IsEnabled);
            
            AnalogInput.Clear();
            AnalogOutput.Clear();
            DigitalInput.Clear();
            DigitalOutput.Clear();

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
                            if (dio != null) DigitalOutput.Add(dio);
                            break;
                    }
                }
            }
        }

    }
}
