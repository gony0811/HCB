using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using HCB.IoC;
using System.Threading;
using HCB.Data.Entity.Type;

namespace HCB.UI
{
    public class OperationService : BackgroundService
    {
        private ILogger _logger;
        private DeviceManager _deviceManager;
        private readonly ISequenceHelper _sequenceHelper;


        public OperationService(ILogger logger, DeviceManager deviceManager, ISequenceHelper sequenceHelper)
        {
            _logger = logger.ForContext<OperationService>();
            _deviceManager = deviceManager;
            _sequenceHelper = sequenceHelper;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. 모든 활성화된 디바이스 상태 갱신
                    // (DeviceManager.Devices는 UI 스레드와 공유될 수 있으므로 주의가 필요하나, 
                    //  일반적으로 읽기 작업은 문제되지 않습니다. 필요시 ToList()로 복사하여 사용)
                    var activeDevices = _deviceManager.Devices.Where(d => d.IsEnabled).ToList();

                    foreach (var device in activeDevices)
                    {
                        if (device.IsEnabled && device.IsConnected)
                            await device.RefreshStatus();
                    }


                    // 2. 장비 상태에 따른 로직 처리
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
                    _logger.Error(ex, "Error in OperationService ExecuteAsync loop");
                }

                // 100ms 주기로 반복 (시스템 요구사항에 따라 조절)
                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
