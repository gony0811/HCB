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
    /// NetMQ ROUTER 소켓 기반 EQP 서버.
    /// Vision(DEALER 클라이언트)이 연결하면 메시지를 양방향으로 교환한다.
    /// 소켓은 NetMQPoller 전용 스레드에서만 조작하며,
    /// 외부 스레드의 전송 요청은 NetMQQueue를 경유한다.
    /// </summary>
    public class EqpTcpServer : IDisposable
    {
        private readonly TcpSettings _settings;
        private RouterSocket? _socket;
        private NetMQPoller? _poller;
        private NetMQQueue<NetMQMessage>? _sendQueue;

        // 마지막으로 메시지를 보낸 Vision 클라이언트의 ZMQ identity 프레임
        private byte[]? _clientId;

        // 응답 대기 중인 RequestAsync 요청들 (responseMessageName → TCS)
        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _pending = new();

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event EventHandler<Message>?          MessageReceived;
        public event EventHandler<ConnectionState>?  ConnectionStateChanged;
        public event EventHandler<string>?           LogMessage;

        public EqpTcpServer(TcpSettings settings)
        {
            _settings = settings;
        }

        // ─── 서버 시작/중지 ──────────────────────────────────────
        public void Start()
        {
            _socket    = new RouterSocket();
            _sendQueue = new NetMQQueue<NetMQMessage>();

            // 전송 큐 → 소켓 (폴러 스레드에서 실행되므로 스레드 안전)
            _sendQueue.ReceiveReady += (_, _) =>
            {
                while (_sendQueue.TryDequeue(out var frames, TimeSpan.Zero))
                    _socket!.SendMultipartMessage(frames);
            };

            _socket.ReceiveReady += OnReceiveReady;
            _socket.Bind($"tcp://*:{_settings.Port}");

            _poller = new NetMQPoller { _socket, _sendQueue };
            _poller.RunAsync();

            Log($"EQP ZMQ 서버 시작 - Port: {_settings.Port}");
        }

        public void Stop()
        {
            foreach (var kv in _pending)
                kv.Value.TrySetCanceled();
            _pending.Clear();

            _poller?.Stop();
            _poller?.Dispose();
            _sendQueue?.Dispose();
            _socket?.Close();
            _socket?.Dispose();

            SetState(ConnectionState.Disconnected);
            Log("EQP ZMQ 서버 중지");
        }

        // ─── EQP → Vision 전송 ───────────────────────────────────
        /// <summary>메시지를 전송 큐에 적재한다 (비동기, 논블로킹).</summary>
        public Task SendAsync(Message message, CancellationToken ct = default)
        {
            EnsureConnected();

            message.Header ??= new MessageHeader();
            message.Header.UnitName = _settings.UnitName;
            message.Header.Time     = DateTime.Now;

            // ROUTER 송신 형태: [identity][content]
            var frames = new NetMQMessage();
            frames.Append(_clientId!);
            frames.Append(message.ToXml(), Encoding.UTF8);

            _sendQueue!.Enqueue(frames);
            return Task.CompletedTask;
        }

        /// <summary>메시지를 전송하고 응답 메시지를 기다린다.</summary>
        public async Task<RequestResult> RequestAsync(
            Message      request,
            string?      responseMessageName = null,
            TimeSpan?    timeout             = null,
            CancellationToken ct             = default)
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

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(timeout ?? TimeSpan.FromSeconds(10));

                var response = await tcs.Task.WaitAsync(linked.Token);
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
                // ROUTER 수신 형태: [identity][content]
                var frames = e.Socket.ReceiveMultipartMessage();
                if (frames.FrameCount < 2) return;

                var id  = frames[0].ToByteArray();
                var xml = frames[frames.FrameCount - 1].ConvertToString(Encoding.UTF8);

                // 첫 메시지 수신 시 Vision 접속으로 간주
                if (_clientId is null)
                {
                    _clientId = id;
                    SetState(ConnectionState.Connected);
                    Log($"Vision 접속: {BitConverter.ToString(id)}");
                }

                HandleIncoming(xml);
            }
            catch (Exception ex)
            {
                Log($"수신 오류: {ex.Message}");
            }
        }

        private void HandleIncoming(string xml)
        {
            try
            {
                var msg     = Message.FromXml(xml);
                var msgName = msg.Header?.MessageName ?? string.Empty;

                // RequestAsync 대기 중인 응답이면 TCS로 전달, 아니면 이벤트 발행
                if (_pending.TryRemove(msgName, out var tcs))
                    tcs.TrySetResult(msg);
                else
                    MessageReceived?.Invoke(this, msg);
            }
            catch (Exception ex)
            {
                Log($"파싱 오류: {ex.Message}");
            }
        }

        // ─── 헬퍼 ────────────────────────────────────────────────
        private void EnsureConnected()
        {
            if (State != ConnectionState.Connected || _clientId is null)
                throw new InvalidOperationException("Vision이 연결되어 있지 않습니다.");
        }

        private void SetState(ConnectionState state)
        {
            if (State == state) return;
            State = state;
            ConnectionStateChanged?.Invoke(this, state);
        }

        private void Log(string msg)
        {
            Console.WriteLine($"[EqpServer {DateTime.Now:HH:mm:ss}] {msg}");
            LogMessage?.Invoke(this, msg);
        }

        public void Dispose() => Stop();
    }
}
