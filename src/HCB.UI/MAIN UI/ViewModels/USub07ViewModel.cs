using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub07ViewModel : ObservableObject 
    {
        private readonly ILogger logger;
        private readonly IoDataRepository ioRepository;
        private readonly DeviceManager deviceManager;

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> analogInput = new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> analogOutput= new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> digitalInput= new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> digitalOutput = new ObservableCollection<SensorIoItemViewModel>();

        public USub07ViewModel(ILogger logger, IoDataRepository ioDataRepository, DeviceManager deviceManager)
        {
            this.logger = logger.ForContext<USub07ViewModel>();
            this.ioRepository = ioDataRepository;
            this.deviceManager = deviceManager;
            _ = LoadIoData();
            
            
        }

        public async Task LoadIoData()
        {
            var device = deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            var ioList = await ioRepository.ListAsync(x => x.IsEnabled);
            
            AnalogInput.Clear();
            AnalogOutput.Clear();
            DigitalInput.Clear();
            DigitalOutput.Clear();

            // Refresh device status to get current output values
            if (device != null && device.IsConnected)
            {
                await device.RefreshStatus();
            }

            foreach (var group in ioList.GroupBy(x => x.IoDataType))
            {
                foreach (var io in group)
                {
                    bool initialValue = false;

                    // Read initial value from device for digital outputs
                    if (group.Key == IoType.DigitalOutput)
                    {
                        try
                        {
                            var ioData = device.FindIoDataByName(io.Name);
                            if (ioData != null && ioData is DigitalOutput digitalOutput)
                            {
                                initialValue = digitalOutput.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            // If reading fails, keep default value and log the error
                            logger.Warning(ex, "Failed to read initial value for digital output: {IoName}", io.Name);
                            initialValue = false;
                        }
                    }

                    switch (group.Key)
                    {
                        case IoType.AnalogInput:
                            AnalogInput.Add(new SensorIoItemViewModel(io.Name, device, io.Address, false, true));
                            break;
                        case IoType.AnalogOutput:
                            AnalogOutput.Add(new SensorIoItemViewModel(io.Name, device, io.Address));
                            break;
                        case IoType.DigitalInput:
                            DigitalInput.Add(new SensorIoItemViewModel(io.Name, device, io.Address, false, true));
                            break;
                        case IoType.DigitalOutput:
                            DigitalOutput.Add(new SensorIoItemViewModel(io.Name, device, io.Address, initialValue, false));
                            break;
                    }
                }
            }
        }

    }
}
