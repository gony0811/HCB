using System;
using System.Diagnostics;
using System.Timers;

namespace HCB.UI.SERVICE.ViewModels
{
    public static class RunningTime
    {
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private static readonly Timer _timeoutTimer = new Timer();

        // 타임아웃 발생 시 외부에서 알 수 있도록 이벤트 정의
        public static event Action OnTimeOut;

        static RunningTime()
        {
            // 타이머 설정
            _timeoutTimer.AutoReset = false; // 한 번만 실행
            _timeoutTimer.Elapsed += Timer_Elapsed;
        }

        private static void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 시간이 다 되면 자동으로 멈춤
            Stop();

            // 외부(UI나 시퀀스)에 타임아웃이 발생했음을 알림
            OnTimeOut?.Invoke();
            Debug.WriteLine("RunningTime: TimeOut 발생하여 자동 정지됨.");
        }

        // 1. 동작 여부
        public static bool IsRunning => _stopwatch.IsRunning;

        // 2. 시작 (타임아웃 초를 넣으면 자동 감시 시작)
        public static void Start(double timeoutSeconds)
        {
            Stop(); // 혹시 실행 중일지 모르니 초기화

            if (timeoutSeconds > 0)
            {
                _timeoutTimer.Interval = timeoutSeconds * 1000; // 초를 밀리초로 변환
                _timeoutTimer.Start();
            }

            _stopwatch.Restart();
        }

        // 3. 정지 (수동 정지 또는 타이머에 의해 호출됨)
        public static void Stop()
        {
            _timeoutTimer.Stop();
            _stopwatch.Stop();
        }

        // 4. 경과 시간 및 남은 시간
        public static double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;
        public static double TimeOutLimit => _timeoutTimer.Interval / 1000;

        public static void Reset()
        {
            Stop();
            _stopwatch.Reset();
        }
    }
}