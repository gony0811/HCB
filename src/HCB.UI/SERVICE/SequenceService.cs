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
            


        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //await DeviceAttatch();

            //_timer.Change(0, 100); // 100ms 주기로 타이머 시작    s

            //StatusChanged?.Invoke(this, new StatusChangedEventArgs(EQStatus.Operation, EQStatus.Availability, EQStatus.Run, EQStatus.Alarm));
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            //_logger.Information("SequenceService is stopping.");

            //_timer.Change(Timeout.Infinite, Timeout.Infinite); // 타이머 중지

            //// 현재 실행 중인 폴링 작업이 있다면 완료될 때까지 대기
            //await _pollingLock.WaitAsync(cancellationToken);
            //_pollingLock.Release();

            //await DeviceDetatch();

            //await base.StopAsync(cancellationToken);
        }

        
    }
}
