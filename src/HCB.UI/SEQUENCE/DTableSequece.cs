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
    [Service(Lifetime.Singleton)]
    public class DTableSequece : ISequence
    {
        private readonly ILogger _logger;
        private readonly ISequenceHelper _sequenceHelper;
        private readonly DeviceManager _deviceManager;
        private readonly AlarmService _alarmService;
        private readonly TableManagerViewModel _tableManagerVm;

        public const string READY_POSITION = "READY";
        public const string LOAD_POSITION = "LOAD";

        public DTableSequece(ILogger logger, ISequenceHelper sequenceHelper, DeviceManager deviceManager, AlarmService alarmService, TableManagerViewModel tableManagerVm)
        {
            this._logger = logger.ForContext<DTableSequece>();
            this._sequenceHelper = sequenceHelper;
            this._deviceManager = deviceManager;
            this._alarmService = alarmService;
            this._tableManagerVm = tableManagerVm;
        }


        // Die Table 초기화 시퀀스 구현
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                this._logger.Debug("InitializeAsync 시작");

                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

                // Die Table 초기화 로직 구현
                var d_y = motionDevice.FindMotionByName(MotionExtensions.D_Y); // Die Table Y축 (예시)

                // 서보 온
                if (!d_y.IsEnabled)
                    await _sequenceHelper.Servo(d_y.MotorNo, true, cancellationToken);

                // Die Table 홈 위치로 이동 (예시 위치명: "InitPosition")
                if (!d_y.IsHomeDone && d_y.InPosition)
                {
                    await _sequenceHelper.HomeAsync(d_y.MotorNo, cancellationToken);
                }

                // 대기 위치로 이동

                if (d_y.InPosition)
                {
                    await _sequenceHelper.MoveAsync(d_y.MotorNo, READY_POSITION, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("DTableSequece: InitializeAsync가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DTableSequece: InitializeAsync 중 오류 발생");
                throw;
            }
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Implement the main run sequence for DTable here
                this._logger.Debug("RunAsync 시작");

                // Loading Completed check
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

                // DTable Vacuum On 확인 -> Die가 하나 이상 로딩 되었는지 확인
                if (!ioDevice.GetDigital(IoExtensions.DI_DTABLE_VAC_PRESSURE_SWITCH))
                {
                    this._logger.Debug("DTable에 Die가 로딩되지 않았습니다.");
                    await this._alarmService.SetAlarm("E001");
                }

                for (int i = 0; i < 9; i++)
                {
                    ioDevice.GetDigital(IoExtensions.DO_DTABLE_VAC_1_ON)
                }


            }
            catch (OperationCanceledException)
            {
                _logger.Information("DTableSequece: RunAsync가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DTableSequece: RunAsync 중 오류 발생");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        
    }
}
