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
        private IOperationService _operationService;

        public InterlockService(ILogger logger, DeviceManager deviceManager, IOperationService operationService)
        {
            _logger = logger.ForContext<InterlockService>();
            _deviceManager = deviceManager;
            _operationService = operationService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                
                _logger.Information(new SysLog("InterlockService", _operationService.Status.Availability.ToString(), _operationService.Status.Run.ToString(), _operationService.Status.Alarm.ToString(), _operationService.Status.Operation.ToString(), "").ToString());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InterlockService: PowerPMAC 연결 실패");
            }
        }
    }
}
