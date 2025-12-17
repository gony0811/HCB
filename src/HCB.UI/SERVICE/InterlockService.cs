using HCB.Data.Entity.Type;
using HCB.IoC;
using HCB.UI.SERVICE.Extenstions;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


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


                // 1. 알람 발생시 운전 정지 및 장비 다운 처리
                if (EQStatus.Alarm == AlarmLevel.HEAVY)
                {
                    EQStatus.Run = RunStop.Stop;
                    EQStatus.Operation = OperationMode.Manual;
                    EQStatus.Availability = Availability.Down;

                    /**** 모든 모션 축 정지 ****/
                    await _sequenceHelper?.StopAllAsync(stoppingToken);
                    _sequenceHelper?.SetTowerLamp(green: false, red: true, yellow: false, buzzer: true);

                    _logger.Warning(new SysLog("OperationService", EQStatus.Availability.ToString(), EQStatus.Run.ToString(), EQStatus.Alarm.ToString(), EQStatus.Operation.ToString(), "Heavy Alarm Detected - Stopping Operation").ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InterlockService: PowerPMAC 연결 실패");
            }
        }
    }
}
