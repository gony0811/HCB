using HCB.IoC;
using Serilog;
using System;
using System.Threading;

namespace HCB.UI
{
    /// <summary>
    /// 축 위치 기반 인터락 서비스.
    /// Interlocked.CompareExchange 를 사용하여 멀티스레드 환경에서도
    /// 락 상태가 단 한 번만 설정되도록 보장합니다.
    /// </summary>
    [Service(Lifetime.Singleton)]   
    
    public class AxisInterlockService : IInterlockService
    {
        // 0 = Normal / 1 = Locked  (Interlocked 연산 전용)
        private int _lockFlag = 0;

        private volatile InterlockEventArgs? _lastEvent;
        private readonly ILogger _logger;

        public bool IsLocked => _lockFlag == 1;

        public event EventHandler<InterlockEventArgs>? InterlockTriggered;
        public event EventHandler? InterlockReleased;

        public AxisInterlockService(ILogger logger)
        {
            _logger = logger.ForContext<AxisInterlockService>();
        }

        // ──────────────────────────────────────────────────────────────
        // Check
        // ──────────────────────────────────────────────────────────────
        public InterlockState Check(
            string axisName, int motorNo,
            double currentPosition,
            double minLimit, double maxLimit,
            double warningMargin = 0.0)
        {
            // ① 범위 초과 → 락
            if (currentPosition < minLimit || currentPosition > maxLimit)
            {
                double violatedLimit = currentPosition < minLimit ? minLimit : maxLimit;
                TriggerLock(axisName, motorNo, currentPosition, violatedLimit);
                return InterlockState.Locked;
            }

            // ② 경고 마진 안쪽
            if (warningMargin > 0.0)
            {
                bool nearMin = currentPosition < minLimit + warningMargin;
                bool nearMax = currentPosition > maxLimit - warningMargin;

                if (nearMin || nearMax)
                {
                    double warnLimit = nearMin ? minLimit : maxLimit;
                    var warnEvt = new InterlockEventArgs(
                        axisName, motorNo, currentPosition, warnLimit, InterlockState.Warning);

                    _logger.Warning(warnEvt.Message);
                    InterlockTriggered?.Invoke(this, warnEvt);
                    return InterlockState.Warning;
                }
            }

            return InterlockState.Normal;
        }

        // ──────────────────────────────────────────────────────────────
        // TryRelease
        // ──────────────────────────────────────────────────────────────
        public bool TryRelease()
        {
            // 1 → 0 교환이 성공(이전 값이 1)해야만 해제
            if (Interlocked.CompareExchange(ref _lockFlag, 0, 1) == 1)
            {
                _logger.Information("[INTERLOCK RELEASED");
                InterlockReleased?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        // ──────────────────────────────────────────────────────────────
        // Private
        // ──────────────────────────────────────────────────────────────
        private void TriggerLock(
            string axisName, int motorNo,
            double value, double limit)
        {
            // CompareExchange: _lockFlag 가 0 일 때만 1 로 변경 → 중복 락 방지
            if (Interlocked.CompareExchange(ref _lockFlag, 1, 0) != 0)
                return; // 이미 락 상태

            var evt = new InterlockEventArgs(
                axisName, motorNo, value, limit, InterlockState.Locked);

            _lastEvent = evt;

            _logger.Error(evt.Message);
            InterlockTriggered?.Invoke(this, evt);
        }
    }
}
