using HCB.Data.Entity;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class GridLogSink : ILogEventSink
    {
        // UI(ViewModel)로 데이터를 넘겨줄 이벤트
        public static event Action<LogModel>? LogReceived;

        public void Emit(LogEvent logEvent)
        {
            var log = new LogModel
            {
                // 사용자가 원한 포맷 그대로 변환
                Timestamp = logEvent.Timestamp.ToString("yyMMddTHH:mm:ss.fffffffz"),

                Level = logEvent.Level.ToString(),

                // 메시지 템플릿이 아닌 완성된 메시지 가져오기
                Message = logEvent.RenderMessage(),

                // 예외가 있으면 문자열로, 없으면 null
                Exception = logEvent.Exception?.ToString()
            };

            // 이벤트 발생
            LogReceived?.Invoke(log);
        }
    }
}
