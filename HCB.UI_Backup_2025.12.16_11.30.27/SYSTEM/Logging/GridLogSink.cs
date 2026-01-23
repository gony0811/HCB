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
        public static event Action<LogModel>? LogReceived;

        public void Emit(LogEvent logEvent)
        {
            // 1. Properties 딕셔너리에서 값을 안전하게 꺼내는 로컬 함수
            string GetProperty(string key)
            {
                // 키가 존재하고, 값이 있으면 문자열로 변환하여 반환
                if (logEvent.Properties.TryGetValue(key, out var value))
                {
                    return value.ToString().Trim('"'); // 따옴표 제거
                }
                return ""; // 없으면 빈 문자열
            }

            

            // 2. 로그 모델 생성
            var log = new LogModel
            {
                // [기존] 기본 정보
                Timestamp = logEvent.Timestamp, // DateTimeOffset 그대로 사용 (DB 저장용)
                Level = logEvent.Level.ToString(),
                Message = logEvent.RenderMessage(),
                Exception = logEvent.Exception?.ToString(),

                // [추가] 딕셔너리에서 꺼내온 정보들
                // "ThreadId", "SourceContext"는 Enricher가 추가해준 키 이름입니다.
                ThreadId = int.TryParse(GetProperty("ThreadId"), out int tid) ? tid : 0,
                SourceContext = GetProperty("SourceContext")
            };

            LogReceived?.Invoke(log);
        }
    }
}
