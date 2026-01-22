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
        private readonly OperationService _operationService;
        private readonly AlarmService _alarmService;
        private readonly SequenceServiceVM _sequenceServiceVM;
        private readonly Timer _timer;
        private readonly bool _simulation;
        private readonly SemaphoreSlim _pollingLock = new SemaphoreSlim(1, 1);

        private CancellationToken _stopToken = CancellationToken.None;

        public SequenceService(ILogger logger, DeviceManager deviceManager, ISequenceHelper sequenceHelper, DataOptions dataOptions, OperationService operationService, AlarmService alarmService, SequenceServiceVM sequenceServiceVM)
        {
            _logger = logger.ForContext<SequenceService>();
            _deviceManager = deviceManager;
            _sequenceHelper = sequenceHelper;
            _operationService = operationService;
            _alarmService = alarmService;
            _sequenceServiceVM = sequenceServiceVM;
            _simulation = dataOptions.Simulation;
            

            // 디바이스 데이터 폴링 타이머 설정 (100ms 주기)
            // 중복 실행 방지 및 종료 시 대기를 위해 SemaphoreSlim 사용
            _timer = new Timer(async _ => 
            {
                // 락 획득 시도 (이미 실행 중이면 스킵)
                if (await _pollingLock.WaitAsync(0))
                {
                    try
                    {
                        await DeviceDataPolling(CancellationToken.None);
                    }
                    finally
                    {
                        _pollingLock.Release();
                    }
                }
            }, null, Timeout.Infinite, Timeout.Infinite);
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

            _timer.Change(Timeout.Infinite, Timeout.Infinite); // 타이머 중지

            // 현재 실행 중인 폴링 작업이 있다면 완료될 때까지 대기
            await _pollingLock.WaitAsync(cancellationToken);
            _pollingLock.Release();

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
                    {
                        await device.Initialize();
                        await device.Connect();
                    }
                        
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
