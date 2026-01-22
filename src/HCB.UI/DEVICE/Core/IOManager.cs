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
        private readonly ConcurrentDictionary<string, SensorIoItemViewModel> _ioCache = new();
        private readonly DeviceManager _deviceManager;
        private readonly ILogger _logger;

        public IOManager(ILogger logger, DeviceManager deviceManager)
        {
            _logger = logger;
            _deviceManager = deviceManager;
        }

        public SensorIoItemViewModel GetOrCreateIo(string name, string address, string description = "", bool isChecked = false, bool isReadOnly = false)
        {
            // Address나 Name을 키로 사용하여 중복 생성 방지
            return _ioCache.GetOrAdd(name, (key) =>
            {
                var device = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
                return new SensorIoItemViewModel(_logger, key, device, address, description, isChecked, isReadOnly);
            });
        }
    }
}
