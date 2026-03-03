using System;

namespace HCB.UI
{
    // ──────────────────────────────────────────────
    // 인터락 상태
    // ──────────────────────────────────────────────
    public enum InterlockState
    {
        Normal,     // 정상
        Warning,    // 경고 (한계값 근접)
        Locked      // 락 (한계값 초과)
    }

    // ──────────────────────────────────────────────
    // 인터락 발생 이벤트 데이터
    // ──────────────────────────────────────────────
    public class InterlockEventArgs : EventArgs
    {
        public string AxisName   { get; }
        public int    MotorNo    { get; }
        public double Value      { get; }
        public double Limit      { get; }
        public InterlockState State { get; }
        public DateTime OccurredAt  { get; }
        public string Message    { get; }

        public InterlockEventArgs(
            string axisName, int motorNo,
            double value, double limit,
            InterlockState state)
        {
            AxisName   = axisName;
            MotorNo    = motorNo;
            Value      = value;
            Limit      = limit;
            State      = state;
            OccurredAt = DateTime.Now;
            Message    = state == InterlockState.Locked
                ? $"[INTERLOCK LOCK]  {axisName}(Motor#{motorNo}) 위치={value:F4}, 한계={limit:F4}"
                : $"[INTERLOCK WARN]  {axisName}(Motor#{motorNo}) 위치={value:F4}, 한계={limit:F4}";
        }
    }

    // ──────────────────────────────────────────────
    // 인터락 서비스 인터페이스
    // ──────────────────────────────────────────────
    public interface IInterlockService
    {
        /// <summary>현재 시스템이 락 상태인지 여부</summary>
        bool IsLocked { get; }

        /// <summary>인터락(락/경고) 발생 이벤트</summary>
        event EventHandler<InterlockEventArgs>? InterlockTriggered;

        /// <summary>인터락 해제 이벤트</summary>
        event EventHandler? InterlockReleased;

        /// <summary>
        /// 축의 현재 위치를 받아 인터락 조건을 검사합니다.
        /// 범위 초과 시 원자적으로 락을 설정하고 이벤트를 발행합니다.
        /// </summary>
        InterlockState Check(string axisName, int motorNo,
                             double currentPosition,
                             double minLimit, double maxLimit,
                             double warningMargin = 0.0);

        /// <summary>
        /// 인터락을 수동으로 해제합니다.
        /// 축이 안전 범위 안에 있는지 확인한 후에만 호출하세요.
        /// </summary>
        bool TryRelease();
    }
}
