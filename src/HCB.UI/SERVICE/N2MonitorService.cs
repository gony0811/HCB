using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    /// <summary>
    /// N2 투입(DO_N2_ON, Y028) 상태 감시
    ///  · 메인 화면 상단 N2 램프에 ON/OFF 및 ON 유지 시간을 실시간 표시 (SequenceServiceVM)
    ///  · ON 상태가 알람 시간(EC: N2_ON_ALARM_SEC, 기본 60초) 이상 지속되면 알람(E0037) 발생 — ON 구간당 1회
    /// </summary>
    public class N2MonitorService : BackgroundService
    {
        private const int PollingIntervalMs = 100;

        // 정상 동작 시 N2는 BondingPress 가압 구간(ACC+ACC2+CONT+DEC) + PMAC 리셋(5s) 동안만 ON 된다.
        // 알람 시간은 EC 파라미터 N2_ON_ALARM_SEC로 설정하며, 없거나 0 이하·잘못된 값이면 기본값을 쓴다.
        private const double DefaultN2OnAlarmSeconds = 60;

        private readonly ILogger _logger;
        private readonly DeviceManager _deviceManager;
        private readonly SequenceServiceVM _sequenceServiceVM;
        private readonly AlarmService _alarmService;
        private readonly ECParamService _ecParamService;

        private readonly Stopwatch _onWatch = new Stopwatch();
        private bool _alarmRaised;

        public N2MonitorService(
            ILogger logger,
            DeviceManager deviceManager,
            SequenceServiceVM sequenceServiceVM,
            AlarmService alarmService,
            ECParamService ecParamService)
        {
            _logger = logger.ForContext<N2MonitorService>();
            _deviceManager = deviceManager;
            _sequenceServiceVM = sequenceServiceVM;
            _alarmService = alarmService;
            _ecParamService = ecParamService;
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
                    Description = $"N2 투입 신호(DO_N2_ON)가 설정 시간(EC: {ParamCatalog.N2OnAlarmSecParamName}) 이상 ON 상태로 유지되었습니다.",
                    Action = $"본딩 프레스 시퀀스 상태와 N2 밸브(Y028)를 확인하십시요. 정상 레시피에서 발생하면 {ParamCatalog.N2OnAlarmSecParamName}을(를) 가압 시간 + 리셋 5초보다 크게 설정하십시요."
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
            double alarmSec = on ? GetN2OnAlarmSeconds() : DefaultN2OnAlarmSeconds;
            bool overtime = on && elapsedSec >= alarmSec;

            UpdateVM(on, overtime, on ? $"N2 ON {elapsedSec:F1}s" : "N2 OFF");

            if (overtime && !_alarmRaised)
            {
                _alarmRaised = true;
                _logger.Warning("N2 투입 ON 상태가 {Sec}초 이상 지속 — 알람 {Code} 발생",
                    alarmSec, PmacErrorCode.N2_ON_OVERTIME);
                await _alarmService.SetAlarm(PmacErrorCode.N2_ON_OVERTIME);
            }
        }

        // EC 파라미터 N2_ON_ALARM_SEC를 매 폴링마다 읽어 화면에서 바꾼 값이 재시작 없이 바로 적용되게 한다.
        // 파라미터가 없거나 0 이하·숫자가 아니면 기본값(60초)을 쓴다.
        private double GetN2OnAlarmSeconds()
        {
            try
            {
                var value = _ecParamService.FindByName(ParamCatalog.N2OnAlarmSecParamName)?.Value;
                if (double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double sec) && sec > 0)
                    return sec;
            }
            catch (Exception ex)
            {
                // ParamList는 UI 스레드에서 수정되므로 열거 중 변경 예외가 날 수 있다 — 이번 폴링은 기본값 사용
                _logger.Debug(ex, "{Name} 조회 실패 — 기본값 사용", ParamCatalog.N2OnAlarmSecParamName);
            }
            return DefaultN2OnAlarmSeconds;
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
