using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class USub07ViewModel : ObservableObject 
    {
        private readonly IoDataRepository ioRepository;
        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> analogInput = new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> analogOutput= new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> digitalInput= new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> digitalOutput = new ObservableCollection<SensorIoItemViewModel>();

        public USub07ViewModel(IoDataRepository ioDataRepository)
        {
            this.ioRepository = ioDataRepository;
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
                            AnalogInput.Add(new SensorIoItemViewModel(io.Name, false, true));
                            break;
                        case IoType.AnalogOutput:
                            AnalogOutput.Add(new SensorIoItemViewModel(io.Name));
                            break;
                        case IoType.DigitalInput:
                            DigitalInput.Add(new SensorIoItemViewModel(io.Name, false, true));
                            break;
                        case IoType.DigitalOutput:
                            DigitalOutput.Add(new SensorIoItemViewModel(io.Name));
                            break;
                    }
                }
            }
        }

    }
}
