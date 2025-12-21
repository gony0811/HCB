using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using HCB.IoC;
using HCB.Data.Entity.Type;
using System.Windows.Threading;
using System.Threading;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public partial class SequenceService : BackgroundService
    {
        private ILogger _logger;
        private DeviceManager _deviceManager;
        private readonly ISequenceHelper _sequenceHelper;
        //private readonly SequenceServiceVM _sequenceServiceVM;
        private readonly Timer _timer;
        private readonly bool _simulation;

        private CancellationToken _stopToken = CancellationToken.None;

        public SequenceService(ILogger logger, DeviceManager deviceManager, ISequenceHelper sequenceHelper, DataOptions dataOptions)
        {
            _logger = logger.ForContext<SequenceService>();
            _deviceManager = deviceManager;
            _sequenceHelper = sequenceHelper;
            //_sequenceServiceVM = sequenceServiceVM;
            _simulation = dataOptions.Simulation;
            // 디바이스 데이터 폴링 타이머 설정 (100ms 주기)
            _timer = new Timer(async _ => await DeviceDataPolling(CancellationToken.None), null, Timeout.Infinite, Timeout.Infinite);
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await DeviceAttatch();

            _timer.Change(0, 100); // 100ms 주기로 타이머 시작    s

            //StatusChanged?.Invoke(this, new StatusChangedEventArgs(EQStatus.Operation, EQStatus.Availability, EQStatus.Run, EQStatus.Alarm));
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Information("SequenceService is stopping.");

            await DeviceDetatch();

            await base.StopAsync(cancellationToken);
        }

        private async Task DeviceDataPolling(CancellationToken ct)
        {
            try
            {
                var activeDevices = _deviceManager.Devices.Where(d => d.IsEnabled).ToList();
                foreach (var device in activeDevices)
                {
                    if (device.IsEnabled && device.IsConnected)
                        await device.RefreshStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during DeviceDataPolling");
            }
        }

        private async Task DeviceAttatch()
        {
            try
            {
                var devices = _deviceManager.Devices.Where(d => d.IsEnabled).ToList();
                foreach (var device in devices)
                {
                    if (device.IsEnabled && !device.IsConnected)
                        await device.Connect();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during DeviceAttatch");
            }
        }

        private async Task DeviceDetatch()
        {
            try
            {
                var devices = _deviceManager.Devices.Where(d => d.IsConnected).ToList();
                foreach (var device in devices)
                {
                    if (device.IsConnected)
                        await device.Disconnect();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during DeviceDetatch");
            }
        }
    }
}
