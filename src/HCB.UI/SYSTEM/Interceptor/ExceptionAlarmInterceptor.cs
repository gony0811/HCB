using HCB.IoC;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public class ExceptionAlarmInterceptor
    {
        private readonly AlarmService _alarmService;
        private readonly ILogger _logger;

        private const string DefaultSystemAlarmCode = "S001";

        public ExceptionAlarmInterceptor(AlarmService alarmService, ILogger logger)
        {
            _alarmService = alarmService;
            _logger = logger.ForContext<ExceptionAlarmInterceptor>();
        }

        public async Task ExecuteAsync(
            Func<Task> action,
            [CallerMemberName] string caller = "")
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ErrorException ex)
            {
                _logger.Error(ex, "[{Caller}] ErrorException 발생 — Code: {Code}", caller, ex.ErrorCode);
                await _alarmService.SetAlarm(ex.ErrorCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[{Caller}] 예외 발생", caller);
                await _alarmService.SetAlarm(DefaultSystemAlarmCode);
                throw;
            }
        }

        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> action,
            [CallerMemberName] string caller = "")
        {
            try
            {
                return await action();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ErrorException ex)
            {
                _logger.Error(ex, "[{Caller}] ErrorException 발생 — Code: {Code}", caller, ex.ErrorCode);
                await _alarmService.SetAlarm(ex.ErrorCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[{Caller}] 예외 발생", caller);
                await _alarmService.SetAlarm(DefaultSystemAlarmCode);
                throw;
            }
        }

        public void Execute(
            Action action,
            [CallerMemberName] string caller = "")
        {
            try
            {
                action();
            }
            catch (ErrorException ex)
            {
                _logger.Error(ex, "[{Caller}] ErrorException 발생 — Code: {Code}", caller, ex.ErrorCode);
                _ = _alarmService.SetAlarm(ex.ErrorCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[{Caller}] 예외 발생", caller);
                _ = _alarmService.SetAlarm(DefaultSystemAlarmCode);
                throw;
            }
        }

        /// <summary>
        /// 예외 발생 시 알람만 발생시키고, 예외를 삼킨다 (fire-and-forget 패턴).
        /// </summary>
        public async Task ExecuteSafeAsync(
            Func<Task> action,
            [CallerMemberName] string caller = "")
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                // 취소는 정상 흐름이므로 알람 없이 무시
            }
            catch (ErrorException ex)
            {
                _logger.Error(ex, "[{Caller}] ErrorException 발생 — Code: {Code}", caller, ex.ErrorCode);
                await _alarmService.SetAlarm(ex.ErrorCode);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[{Caller}] 예외 발생", caller);
                await _alarmService.SetAlarm(DefaultSystemAlarmCode);
            }
        }
    }
}
