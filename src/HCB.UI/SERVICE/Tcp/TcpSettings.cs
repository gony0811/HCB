using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class TcpSettings
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 9000;
        public string UnitName { get; set; } = "EQP";
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(3);
        public bool AutoReconnect { get; set; } = true;
        /// <summary>메시지 구분자 (XML 전문 끝을 감지하는 태그)</summary>
        public string MessageDelimiter { get; set; } = "</MESSAGE>";
        public int ReceiveBufferSize { get; set; } = 65536;
    }
}
