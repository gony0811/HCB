using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using System;
using System.IO;

namespace HCB.UI
{
    public class TextBoxSink : ILogEventSink
    {
        private readonly ITextFormatter _formatter;

        // UI에서 구독할 정적 이벤트 (ViewModel과 연결 고리)
        public static event Action<string>? LogEmitted;

        public TextBoxSink(string outputTemplate)
        {
            // 사용자가 원한 포맷터 설정
            _formatter = new MessageTemplateTextFormatter(outputTemplate, null);
        }

        public void Emit(LogEvent logEvent)
        {
            var buffer = new StringWriter();
            _formatter.Format(logEvent, buffer);

            // 포맷팅된 문자열을 이벤트로 전송
            LogEmitted?.Invoke(buffer.ToString());
        }
    }
}