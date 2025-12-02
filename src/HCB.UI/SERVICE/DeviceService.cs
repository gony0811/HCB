
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace HCB.UI
{
    public class DeviceService : BackgroundService
    {
        private readonly DeviceManager _deviceManager;
        private readonly ILogger _logger;

        public DeviceService(ILogger logger, DeviceManager deviceManager)
        {
            _logger = logger;
            _deviceManager = deviceManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Information("DeviceService is starting.");

            foreach (var device in _deviceManager.Devices)
            {
                await device.Initialize();
                await device.Connect();

                _ = Task.Run(async () =>
                {
                    var refreshInProgress = 0;

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (Interlocked.CompareExchange(ref refreshInProgress, 1, 0) == 0)
                        {
                            if  (device.IsEnabled && device.IsConnected)
                            {
                                _ = device.RefreshStatus().ContinueWith(t =>
                                {
                                    if (t.IsFaulted)
                                    {
                                        _logger.Error(t.Exception, $"Error refreshing status for device {device.Name}.");
                                    }
                                    Interlocked.Exchange(ref refreshInProgress, 0);
                                }, TaskScheduler.Default);
                            }
                            else
                            {
                                Interlocked.Exchange(ref refreshInProgress, 0);
                            }
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                    }

                    await device.Disconnect();
                }, stoppingToken);


            }
        }
    }
}
