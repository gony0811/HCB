using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class EqpTcpServer : IDisposable
    {
        private readonly TcpSettings _settings;
        private RouterSocket? _socket;
        private NetMQPoller? _poller;
        private NetMQQueue<NetMQMessage>? _sendQueue;

        private byte[]? _clientId;

        // ★ 마지막 메시지 수신 시각 — 클라이언트 재연결 감지에 사용
        private DateTime _lastReceivedAt = DateTime.MinValue;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _pending = new();

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event EventHandler<Message>? MessageReceived;
        public event EventHandler<ConnectionState>? ConnectionStateChanged;
        public event EventHandler<string>? LogMessage;

        public EqpTcpServer(TcpSettings settings)
        {
            _settings = settings;
        }

        // ─── 서버 시작/중지 ──────────────────────────────────────
        public void Start()
        {
            _socket = new RouterSocket();
            _sendQueue = new NetMQQueue<NetMQMessage>();

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

        // ─── 전송 ────────────────────────────────────────────────
        public Task SendAsync(Message message, CancellationToken ct = default)
        {
            EnsureConnected();

            message.Header ??= new MessageHeader();
            message.Header.UnitName = _settings.UnitName;
            message.Header.Time = DateTime.Now;

            var frames = new NetMQMessage();
            frames.Append(_clientId!);
            frames.Append(message.ToXml(), Encoding.UTF8);

            _sendQueue!.Enqueue(frames);
            return Task.CompletedTask;
        }

        public async Task<RequestResult> RequestAsync(
            Message request,
            string? responseMessageName = null,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
            EnsureConnected();

            var reqName = request.Header?.MessageName
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
                var frames = e.Socket.ReceiveMultipartMessage();
                if (frames.FrameCount < 2) return;

                var id = frames[0].ToByteArray();
                var xml = frames[frames.FrameCount - 1].ConvertToString(Encoding.UTF8);

                _lastReceivedAt = DateTime.Now;

                // ★ 핵심 수정: 최초 연결 OR 재연결(identity 변경 포함) 모두 감지
                bool isNewClient = _clientId is null;
                bool isReconnect = _clientId is not null && !_clientId.SequenceEqual(id);

                if (isNewClient || isReconnect)
                {
                    if (isReconnect)
                    {
                        Log($"Vision 재연결 감지 (이전: {BitConverter.ToString(_clientId!)}" +
                            $" → 신규: {BitConverter.ToString(id)})");

                        // 재연결 시 대기 중인 이전 요청 전부 취소
                        foreach (var kv in _pending)
                            kv.Value.TrySetCanceled();
                        _pending.Clear();

                        // ★ Disconnected → Connected 순서로 전환해야
                        //   EqpCommunicationService에서 StopHeartbeat → StartHeartbeat 재호출됨
                        SetState(ConnectionState.Disconnected);
                    }
                    else
                    {
                        Log($"Vision 최초 접속: {BitConverter.ToString(id)}");
                    }

                    _clientId = id;
                    SetState(ConnectionState.Connected);
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
                var msg = Message.FromXml(xml);
                var msgName = msg.Header?.MessageName ?? string.Empty;

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