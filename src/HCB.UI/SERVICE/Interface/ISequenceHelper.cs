using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HCB.UI
{
    

    public interface ISequenceHelper
    {
        DeviceManager DeviceManager { get; }

        // 공통 기능: 로그, 딜레이
        void Log(LogLevel logLevel, string message);
        Task DelayAsync(int ms, CancellationToken ct);
        // 공통 기능: 조건 대기 (가장 범용적인 함수 하나만)
        Task WaitUntilAsync(Func<bool> condition, int timeoutMs, CancellationToken ct, string errorMsg);

        /*
         // 사용 예시 (MotionExtensions 등에서 호출)
         await helper.WaitUntilAsync(
                                        () => axis.IsFinished, // 이 부분이 계속 반복 실행됨
                                        5000, 
                                        ct, 
                                        "Axis Z Move Timeout"
         );
         */
    }
}
