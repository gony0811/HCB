using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class EqpTcpServer : IDisposable
    {
        private readonly TcpSettings _settings;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private TcpSession? _session;

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
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _settings.Port);
            _listener.Start();
            Log($"EQP 서버 시작 - Port: {_settings.Port}");
            SetState(ConnectionState.Disconnected);
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            DetachSession();
            _session?.Dispose();
            _listener?.Stop();
            SetState(ConnectionState.Disconnected);
            Log("EQP 서버 중지");
        }

        // ─── EQP → Vision 전송 ───────────────────────────────────
        public Task SendAsync(Message message, CancellationToken ct = default)
        {
            EnsureSession();
            return _session!.SendAsync(message, ct);
        }

        public Task<RequestResult> RequestAsync(
            Message request,
            string? responseMessageName = null,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
            EnsureSession();
            return _session!.RequestAsync(request, responseMessageName, timeout, ct);
        }

        // ─── Accept 루프 ─────────────────────────────────────────
        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener!.AcceptTcpClientAsync(ct);
                    Log($"Vision 접속: {client.Client.RemoteEndPoint}");

                    // 기존 세션 이벤트 먼저 제거 후 Dispose
                    DetachSession();
                    _session?.Dispose();

                    _session = new TcpSession(
                        stream: client.GetStream(),
                        unitName: _settings.UnitName,
                        delimiter: _settings.MessageDelimiter,
                        bufferSize: _settings.ReceiveBufferSize
                    );

                    // 메서드로 등록해야 -= 로 제거 가능
                    _session.MessageReceived += OnSessionMessageReceived;
                    _session.Disconnected += OnSessionDisconnected;

                    _session.Start();
                    SetState(ConnectionState.Connected);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Log($"Accept 오류: {ex.Message}"); }
            }
        }

        // ─── 세션 이벤트 핸들러 ──────────────────────────────────
        private void OnSessionMessageReceived(object? sender, Message msg)
            => MessageReceived?.Invoke(this, msg);

        private void OnSessionDisconnected(object? sender, EventArgs e)
        {
            Log("Vision 연결 끊김");
            SetState(ConnectionState.Disconnected);
        }

        // 이벤트 구독 해제 - 재접속 시 중복 등록 방지
        private void DetachSession()
        {
            if (_session is null) return;
            _session.MessageReceived -= OnSessionMessageReceived;
            _session.Disconnected -= OnSessionDisconnected;
        }

        // ─── 헬퍼 ────────────────────────────────────────────────
        private void EnsureSession()
        {
            if (State != ConnectionState.Connected || _session is null)
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