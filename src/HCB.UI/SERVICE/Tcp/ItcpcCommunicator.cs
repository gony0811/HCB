using System;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    public class RequestResult
    {
        public bool Success { get; set; }
        public Message? Response { get; set; }
        public string? ErrorMessage { get; set; }

        public static RequestResult Ok(Message response) => new() { Success = true, Response = response };
        public static RequestResult Fail(string error) => new() { Success = false, ErrorMessage = error };
    }

    public interface ITcpCommunicator : IDisposable
    {
        ConnectionState State { get; }
        string UnitName { get; }

        event EventHandler<Message>? MessageReceived;
        event EventHandler<ConnectionState>? ConnectionStateChanged;

        Task ConnectAsync(CancellationToken ct = default);
        Task DisconnectAsync();

        /// <summary>메시지 전송 (응답 없음)</summary>
        Task SendAsync(Message message, CancellationToken ct = default);

        /// <summary>
        /// 메시지 전송 후 응답 대기.
        /// responseMessageName: 응답으로 올 MessageName이 요청과 다를 때 명시.
        /// 예) request: "REQUEST-VISION-STATUS", responseMessageName: "REPLY-VISION-STATUS"
        /// </summary>
        Task<RequestResult> RequestAsync(Message request, string? responseMessageName = null, TimeSpan? timeout = null, CancellationToken ct = default);
    }
}