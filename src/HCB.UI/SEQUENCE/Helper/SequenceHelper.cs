using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace HCB.UI
{
    public class SequenceHelper : ISequenceHelper
    {
        private ILogger logger;
        private DeviceManager deviceManager;
        public DeviceManager DeviceManager
        {
            get { return deviceManager; }
        }

        public SequenceHelper(ILogger logger, DeviceManager deviceManager)
        {
            this.logger = logger.ForContext<SequenceHelper>();
            this.deviceManager = deviceManager;
        }

        /// <summary>
        /// 지정된 시간만큼 대기합니다. CancellationToken으로 취소 가능합니다.
        /// </summary>
        /// <param name="ms">대기 시간 (밀리초)</param>
        /// <param name="ct">취소 토큰</param>
        public async Task DelayAsync(int ms, CancellationToken ct)
        {
            try
            {
                await Task.Delay(ms, ct);
            }
            catch (OperationCanceledException)
            {
                logger.Debug($"DelayAsync({ms}ms) 취소됨");
                throw;
            }
        }

        /// <summary>
        /// 시퀀스 실행 중 로그를 기록합니다.
        /// </summary>
        /// <param name="message">로그 메시지</param>
        public void Log(LogLevel logLevel, string message)
        {
            switch(logLevel)
            {
                case LogLevel.Debug:
                    logger.Debug($"[Sequence] {message}");
                    return;
                case LogLevel.Information:
                    logger.Information($"[Sequence] {message}");
                    return;
                case LogLevel.Warning:
                    logger.Warning($"[Sequence] {message}");
                    return;
                case LogLevel.Error:
                    logger.Error($"[Sequence] {message}");
                    return;
                case LogLevel.Critical:
                    logger.Fatal($"[Sequence] {message}");
                    return;
                case LogLevel.Trace:
                    logger.Verbose($"[Sequence] {message}");
                    return;
                case LogLevel.None:
                    return;
            }
        }

        /// <summary>
        /// 조건이 참이 될 때까지 대기합니다. 타임아웃 시 예외를 발생시킵니다.
        /// </summary>
        /// <param name="condition">확인할 조건 true이면 탈출</param>
        /// <param name="timeoutMs">타임아웃 시간 (밀리초)</param>
        /// <param name="ct">취소 토큰</param>
        /// <param name="errorMsg">타임아웃 시 표시할 에러 메시지</param>
        public async Task WaitUntilAsync(Func<bool> condition, int timeoutMs, CancellationToken ct, string errorMsg)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            var stopwatch = Stopwatch.StartNew();
            const int checkIntervalMs = 50; // 50ms마다 조건 확인

            while (!condition())
            {
                // 취소 확인
                ct.ThrowIfCancellationRequested();

                // 타임아웃 확인
                if (stopwatch.ElapsedMilliseconds >= timeoutMs)
                {
                    var message = string.IsNullOrEmpty(errorMsg) 
                        ? $"조건 대기 시간 초과 ({timeoutMs}ms)" 
                        : errorMsg;
                    
                    logger.Error($"[Sequence] WaitUntilAsync 타임아웃: {message}");
                    break;
                }

                // 짧은 대기 후 다시 확인
                await Task.Delay(checkIntervalMs, ct);
            }

            stopwatch.Stop();
            logger.Debug($"[Sequence] WaitUntilAsync 완료 (소요시간: {stopwatch.ElapsedMilliseconds}ms)");
        }
    }
}
