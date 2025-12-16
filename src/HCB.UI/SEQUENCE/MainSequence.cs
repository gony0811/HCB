using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HCB.Data.Entity.Type;
using Serilog;

namespace HCB.UI
{
    internal class MainSequence : ISequence
    {
        private readonly ILogger _logger;
        private readonly ISequenceHelper _sequenceHelper;
        private readonly DeviceManager _deviceManager;


        public MainSequence(ILogger logger, ISequenceHelper sequenceHelper, DeviceManager deviceManager)
        {
            this._logger = logger.ForContext<MainSequence>();
            this._sequenceHelper = sequenceHelper;
            this._deviceManager = deviceManager;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (EQStatus.Availability == Availability.Down)
                {
                    _logger.Warning("MainSequence: 장비 상태가 Down입니다. 시퀀스를 시작할 수 없습니다.");
                    throw new Exception("장비 상태가 Down입니다. 시퀀스를 시작할 수 없습니다.");
                }
                else if (EQStatus.Operation == OperationMode.Auto)
                {
                    _logger.Warning("초기화 시퀀스는 Manual 모드에서만 수행할 수 있습니다.");
                    throw new Exception("초기화 시퀀스는 Manual 모드에서만 수행할 수 있습니다.");
                }
                else if (EQStatus.Alarm == AlarmLevel.HEAVY)
                {
                    _logger.Warning("MainSequence: Heavy Alarm 상태입니다. 시퀀스를 시작할 수 없습니다.");
                    throw new Exception("Heavy Alarm 상태입니다. 시퀀스를 시작할 수 없습니다.");
                }

                var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                if (device == null)
                {
                    _logger.Error("MainSequence: PowerPmacDevice를 찾을 수 없습니다.");
                    throw new Exception("PowerPmacDevice를 찾을 수 없습니다.");
                }

                /// 모든 축 홈 수행
                foreach (var axis in device.MotionList)
                {
                    // 홈이 안된 축은 홈 수행
                    // 안전상 일단 한번에 하나씩 홈 수행
                    if (!axis.IsHomeDone)
                    {
                        await _sequenceHelper.HomeAsync(axis.Id, cancellationToken);
                    }
                }

                /// 
            }
            catch (OperationCanceledException)
            {
                _logger.Information("MainSequence: InitializeAsync가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MainSequence: InitializeAsync 중 오류 발생");
                throw;
            }
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
