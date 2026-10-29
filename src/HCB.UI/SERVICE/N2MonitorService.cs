using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    /// <summary>
    /// N2 투입(DO_N2_ON, Y028) 상태 감시
    ///  · 메인 화면 상단 N2 램프에 ON/OFF 및 ON 유지 시간을 실시간 표시 (SequenceServiceVM)
    ///  · ON 상태가 N2OnAlarmSeconds 이상 지속되면 알람(E0037) 발생 — ON 구간당 1회
    /// </summary>
    public class N2MonitorService : BackgroundService
    {
        private const int PollingIntervalMs = 100;

        // 정상 동작 시 N2는 BondingPress 가압 구간(ACC+ACC2+CONT+DEC)에만 ON 된다.
        private const int N2OnAlarmSeconds = 60;

        private readonly ILogger _logger;
        private readonly DeviceManager _deviceManager;
        private readonly SequenceServiceVM _sequenceServiceVM;
        private readonly AlarmService _alarmService;

        private readonly Stopwatch _onWatch = new Stopwatch();
        private bool _alarmRaised;

        public N2MonitorService(
            ILogger logger,
            DeviceManager deviceManager,
            SequenceServiceVM sequenceServiceVM,
            AlarmService alarmService)
        {
            _logger = logger.ForContext<N2MonitorService>();
            _deviceManager = deviceManager;
            _sequenceServiceVM = sequenceServiceVM;
            _alarmService = alarmService;
            _alarmService.AlarmCodeReset += OnAlarmCodeReset;
        }

        // N2 장시간 ON 알람(E0037)을 해제하면 N2 Blow(Y028)도 OFF 한다.
        private void OnAlarmCodeReset(string code)
        {
            if (code != PmacErrorCode.N2_ON_OVERTIME) return;

            try
            {
                var device = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
                if (device == null) return;

                device.SetDigital(IoExtensions.DO_N2_BLOW_ON, false);
                _logger.Information("N2 알람({Code}) 해제 — N2 Blow OFF", code);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N2 알람 해제 후 N2 Blow OFF 실패");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _alarmService.EnsureAlarm(new Alarm
                {
                    Code = PmacErrorCode.N2_ON_OVERTIME,
                    Name = "N2 투입 장시간 ON",
                    Level = AlarmLevel.HEAVY,
                    Enable = AlarmEnable.ENABLED,
                    Description = $"N2 투입 신호(DO_N2_ON)가 {N2OnAlarmSeconds}초 이상 ON 상태로 유지되었습니다.",
                    Action = "본딩 프레스 시퀀스 상태와 N2 밸브(Y028)를 확인하십시요."
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N2 알람(E0037) 등록 실패");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Poll();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "N2MonitorService 폴링 오류");
                }

                try { await Task.Delay(PollingIntervalMs, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task Poll()
        {
            var device = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            if (device == null) return;

            bool on = await ReadN2Output(device);

            if (on)
            {
                if (!_onWatch.IsRunning)
                {
                    _onWatch.Restart();
                    _logger.Information("N2 투입 ON");
                }
            }
            else if (_onWatch.IsRunning)
            {
                _onWatch.Stop();
                _alarmRaised = false;
                _logger.Information("N2 투입 OFF (ON 유지 {Elapsed:F1}s)", _onWatch.Elapsed.TotalSeconds);
            }

            double elapsedSec = on ? _onWatch.Elapsed.TotalSeconds : 0;
            bool overtime = on && elapsedSec >= N2OnAlarmSeconds;

            UpdateVM(on, overtime, on ? $"N2 ON {elapsedSec:F1}s" : "N2 OFF");

            if (overtime && !_alarmRaised)
            {
                _alarmRaised = true;
                _logger.Warning("N2 투입 ON 상태가 {Sec}초 이상 지속 — 알람 {Code} 발생",
                    N2OnAlarmSeconds, PmacErrorCode.N2_ON_OVERTIME);
                await _alarmService.SetAlarm(PmacErrorCode.N2_ON_OVERTIME);
            }
        }

        // PmacIoDevice는 초기화 이후 출력(DO)을 폴링하지 않아 GetDigital()은 프로그램이 마지막으로 Set한 값만 돌려준다.
        // PLC가 직접 ON 시킨 경우도 추적하기 위해 PMAC에서 실제 출력값을 읽고, IO 캐시(I/O 화면)도 함께 갱신한다.
        private async Task<bool> ReadN2Output(PmacIoDevice device)
        {
            if (device.FindIoDataByName(IoExtensions.DO_N2_BLOW_ON) is not DigitalOutput io
                || string.IsNullOrWhiteSpace(io.Address))
                return device.GetDigital(IoExtensions.DO_N2_BLOW_ON);

            try
            {
                string response = await device.SendCommand<string>(io.Address);
                if (uint.TryParse(response?.Trim(), out uint raw))
                {
                    io.Value = raw > 0;   // 캐시 갱신만 수행 (DigitalOutput.Value setter는 출력 명령을 보내지 않음)
                    return io.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "N2 출력 상태 읽기 실패 — 캐시값 사용");
            }

            return io.Value;
        }

        private void UpdateVM(bool on, bool overtime, string text)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.InvokeAsync(() =>
            {
                _sequenceServiceVM.N2On = on;
                _sequenceServiceVM.N2Overtime = overtime;
                _sequenceServiceVM.N2OnElapsedText = text;
            });
        }
    }
}
