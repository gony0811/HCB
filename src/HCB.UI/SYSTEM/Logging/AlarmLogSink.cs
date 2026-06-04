using Serilog.Core;
using Serilog.Events;
using System;
using System.Threading;

namespace HCB.UI
{
    public class AlarmLogSink : ILogEventSink
    {
        private static AlarmService? _alarmService;
        private static int _processing;

        public static void Bind(AlarmService alarmService)
        {
            _alarmService = alarmService;
        }

        public void Emit(LogEvent logEvent)
        {
            if (_alarmService == null) return;
            if (logEvent.Level < LogEventLevel.Error) return;
            if (logEvent.Exception == null) return;

            // 재진입 방지 — AlarmService 내부 로그가 다시 이 Sink을 호출하는 것을 차단
            if (Interlocked.CompareExchange(ref _processing, 1, 0) != 0) return;

            try
            {
                if (logEvent.Exception is ErrorException errorEx)
                    _ = _alarmService.SetAlarm(errorEx.ErrorCode);
                else
                    _ = _alarmService.SetAlarm("S001");
            }
            catch
            {
                // Sink 내부 예외가 로깅 파이프라인을 깨트리지 않도록 방지
            }
            finally
            {
                Interlocked.Exchange(ref _processing, 0);
            }
        }
    }
}
