using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HCB.IoC;
using Serilog;

namespace HCB.UI
{
    internal class InterlockService : BackgroundService
    {
        private ILogger _logger;
        private DeviceManager _deviceManager;

        public InterlockService(ILogger logger, DeviceManager deviceManager)
        {
            _logger = logger.ForContext<InterlockService>();
            _deviceManager = deviceManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var motion = _deviceManager.GetDevice<PowerPmacDevice>("PowerPMAC");

            try
            {
                if (motion != null && !motion.IsConnected) 
                    await motion.Connect();

                _logger.Information("InterlockService ExecuteAsync");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InterlockService: PowerPMAC 연결 실패");
            }
        }
    }
}
