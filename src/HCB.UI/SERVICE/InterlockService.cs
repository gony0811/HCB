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
        private readonly ISequenceHelper _sequenceHelper;

        public InterlockService(ILogger logger, ISequenceHelper sequenceHelper, DeviceManager deviceManager)
        {
            _logger = logger.ForContext<InterlockService>();
            _deviceManager = deviceManager;
            _sequenceHelper = sequenceHelper;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.Information(new SysLog("InterlockService", EQStatus.Availability.ToString(), EQStatus.Run.ToString(), EQStatus.Alarm.ToString(), EQStatus.Operation.ToString(), "").ToString());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InterlockService: PowerPMAC 연결 실패");
            }
        }
    }
}
