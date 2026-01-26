using HCB.Data.Repository;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI.DEVICE.Core
{
    [Service(Lifetime.Singleton)] // 싱글톤으로 관리
    public class IOManager
    {
        private static readonly Dictionary<string, SharedIoState> _sharedIoStates = new();
        private readonly IoDataRepository ioRepository;
        private readonly DeviceManager _deviceManager;
        private readonly ILogger _logger;
        private PmacIoDevice device;

        public IOManager(ILogger logger, DeviceManager deviceManager, IoDataRepository ioDataRepository)
        {
            _logger = logger;
            _deviceManager = deviceManager;
            this.ioRepository = ioDataRepository;
            _ = Load();
        }

        public async Task Load()
        {
            this.device = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            var ioList = await ioRepository.ListAsync(x => x.IsEnabled);
            foreach (var io in ioList) {
                _sharedIoStates.Add(io.Name, new SharedIoState { IsChecked = device.GetDigital(io.Name)});
            }
        }

        public SensorIoItemViewModel? CreateIoVM(string address, string name, string label="", string description="", bool isReadOnly = false)
        {
            try
            {
                var state = _sharedIoStates[name];
                if (label.Equals(""))
                {
                    return new SensorIoItemViewModel(_logger, name, state, device, address, description, isReadOnly);
                }else
                {
                    return new SensorIoItemViewModel(_logger, name, state, device, label, description, isReadOnly);
                }
                
            }catch(Exception ex)
            {
                _logger.Error("Io Address Not Found");
            }
            return null;
        }
    }
}
