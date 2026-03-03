using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    /// <summary>
    /// NetMQ DEALER 소켓 기반 클라이언트.
    /// ROUTER 서버(Vision 또는 외부 시스템)에 연결하여 메시지를 양방향으로 교환한다.
    /// </summary>
    public class TcpCommunicator : ITcpCommunicator
    {
        private readonly TcpSettings _settings;
        private DealerSocket? _socket;
        private NetMQPoller?  _poller;
        private NetMQQueue<NetMQMessage>? _sendQueue;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _pending = new();

        private ConnectionState _state = ConnectionState.Disconnected;

        public string UnitName => _settings.UnitName;

        public ConnectionState State
        {
            get => _state;
            private set
            {
                if (_state == value) return;
                _state = value;
                ConnectionStateChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<Message>?         MessageReceived;
        public event EventHandler<ConnectionState>? ConnectionStateChanged;

        public TcpCommunicator(TcpSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        // ─── 연결 ────────────────────────────────────────────────
        public Task ConnectAsync(CancellationToken ct = default)
        {
            if (State == ConnectionState.Connected) return Task.CompletedTask;

            State   = ConnectionState.Connecting;
            _socket = new DealerSocket();
            _socket.Connect($"tcp://{_settings.Host}:{_settings.Port}");

            _sendQueue = new NetMQQueue<NetMQMessage>();
            _sendQueue.ReceiveReady += (_, _) =>
            {
                while (_sendQueue.TryDequeue(out var msg, TimeSpan.Zero))
                    _socket!.SendMultipartMessage(msg);
            };

            _socket.ReceiveReady += OnReceiveReady;

            _poller = new NetMQPoller { _socket, _sendQueue };
            _poller.RunAsync();

            State = ConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            foreach (var kv in _pending)
                kv.Value.TrySetCanceled();
            _pending.Clear();

            _poller?.Stop();
            _poller?.Dispose();
            _sendQueue?.Dispose();
            _socket?.Close();
            _socket?.Dispose();

            State = ConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        // ─── 전송 ────────────────────────────────────────────────
        public Task SendAsync(Message message, CancellationToken ct = default)
        {
            EnsureConnected();

            message.Header ??= new MessageHeader();
            message.Header.UnitName = _settings.UnitName;
            message.Header.Time     = DateTime.Now;

            // DEALER 송신 형태: [content] (identity 프레임 없음)
            var frames = new NetMQMessage();
            frames.Append(message.ToXml(), Encoding.UTF8);

            _sendQueue!.Enqueue(frames);
            return Task.CompletedTask;
        }

        // ─── 요청-응답 ───────────────────────────────────────────
        public async Task<RequestResult> RequestAsync(
            Message   request,
            string?   responseMessageName = null,
            TimeSpan? timeout             = null,
            CancellationToken ct          = default)
        {
            EnsureConnected();

            var reqName     = request.Header?.MessageName
                              ?? throw new ArgumentException("MessageName이 설정되지 않았습니다.");
            var responseKey = responseMessageName
                              ?? reqName.Replace("REQUEST", "REPLY", StringComparison.OrdinalIgnoreCase);

            var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pending.TryAdd(responseKey, tcs))
                throw new InvalidOperationException($"동일 responseKey({responseKey})의 요청이 이미 진행 중입니다.");

            try
            {
                await SendAsync(request, ct);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout ?? _settings.RequestTimeout);

                var response = await tcs.Task.WaitAsync(cts.Token);
                return RequestResult.Ok(response);
            }
            catch (OperationCanceledException)
            {
                return RequestResult.Fail($"타임아웃: {reqName} → {responseKey}");
            }
            catch (Exception ex)
            {
                return RequestResult.Fail(ex.Message);
            }
            finally
            {
                _pending.TryRemove(responseKey, out _);
            }
        }

        // ─── 수신 (폴러 스레드에서 호출됨) ─────────────────────
        private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
        {
            try
            {
                // DEALER 수신 형태: [content] (identity 없음)
                var frames = e.Socket.ReceiveMultipartMessage();
                var xml    = frames[frames.FrameCount - 1].ConvertToString(Encoding.UTF8);
                HandleIncoming(xml);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpCommunicator] 수신 오류: {ex.Message}");
            }
        }

        private void HandleIncoming(string xml)
        {
            try
            {
                var msg     = Message.FromXml(xml);
                var msgName = msg.Header?.MessageName ?? string.Empty;

                if (_pending.TryRemove(msgName, out var tcs))
                    tcs.TrySetResult(msg);
                else
                    MessageReceived?.Invoke(this, msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpCommunicator] 파싱 오류: {ex.Message}");
            }
        }

        private void EnsureConnected()
        {
            if (State != ConnectionState.Connected)
                throw new InvalidOperationException("ZMQ 연결이 되어 있지 않습니다.");
        }

        public void Dispose() => _ = DisconnectAsync();
    }
}
