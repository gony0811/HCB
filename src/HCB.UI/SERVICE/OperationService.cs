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
    public class OperationService : BackgroundService, IOperationService
    {
        private ILogger _logger;
        private DeviceManager _deviceManager;


        public OperationService(ILogger logger, DeviceManager deviceManager)
        {
            _logger = logger.ForContext<OperationService>();
            _deviceManager = deviceManager;
        }

        private EQStatus _eqStatus = new EQStatus();
        public EQStatus Status => _eqStatus;

        public event Action<Availability> AvailabilityChanged;
        public event Action<AlarmLevel> AlarmChanged;
        public event Action<RunStop> RunChanged;
        public event Action<OperationMode> OperationModeChanged;

        public void SetAvailability(Availability availability)
        {
            if (_eqStatus.Availability != availability)
            {
                _eqStatus.Availability = availability;
                AvailabilityChanged?.Invoke(availability);
            }
        }

        public void SetAlarm(AlarmLevel alarm)
        {
            if (_eqStatus.Alarm != alarm)
            {
                _eqStatus.Alarm = alarm;
                AlarmChanged?.Invoke(alarm);
            }
        }

        public void SetRun(RunStop run)
        {
            if (_eqStatus.Run != run)
            {
                _eqStatus.Run = run;
                RunChanged?.Invoke(run);
            }
        }

        public void SetOperationMode(OperationMode operation)
        {
            if (_eqStatus.Operation != operation)
            {
                _eqStatus.Operation = operation;
                OperationModeChanged?.Invoke(operation);
            }
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
                        await device.RefreshStatus();
                    }

                    // 2. Availability 체크: 모든 활성 디바이스가 연결되어 있어야 함
                    bool allConnected = activeDevices.All(d => d.IsConnected);
                    SetAvailability(allConnected ? Availability.Up : Availability.Down);

                    // 3. Alarm 체크: 모션 디바이스의 축 에러 확인
                    bool hasError = false;
                    foreach (var device in activeDevices.OfType<IMotionDevice>())
                    {
                        // 각 축(Axis) 중 하나라도 에러가 있으면 시스템 에러로 간주
                        if (device.MotionList.Any(axis => axis.IsError))
                        {
                            hasError = true;
                            break;
                        }
                    }

                    // 에러 발생 시 HEAVY, 아니면 Normal (필요에 따라 Light 등 세분화 가능)
                    SetAlarm(hasError ? AlarmLevel.HEAVY : AlarmLevel.Normal);

                    // 4. 안전 인터락: 장비가 Down이거나 중대 알람 발생 시 Stop으로 강제 전환
                    if (Status.Availability == Availability.Down || Status.Alarm == AlarmLevel.HEAVY)
                    {
                        // 이미 Stop 상태가 아니라면 Stop으로 변경
                        if (Status.Run != RunStop.Stop)
                        {
                            SetRun(RunStop.Stop);
                            _logger.Warning("System forced to STOP due to Availability: {Availability}, Alarm: {Alarm}", 
                                Status.Availability, Status.Alarm);
                        }
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
