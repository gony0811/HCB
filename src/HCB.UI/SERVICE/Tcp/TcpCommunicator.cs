using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{ 
    public class TcpCommunicator : ITcpCommunicator
    {
        private readonly TcpSettings _settings;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        // responseMessageName → 대기 중인 TCS
        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _pending = new();

        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly SemaphoreSlim _connectLock = new(1, 1);

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

        public event EventHandler<Message>? MessageReceived;
        public event EventHandler<ConnectionState>? ConnectionStateChanged;

        public TcpCommunicator(TcpSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        // ─── 연결 ────────────────────────────────────────────────
        public async Task ConnectAsync(CancellationToken ct = default)
        {
            await _connectLock.WaitAsync(ct);
            try
            {
                if (State == ConnectionState.Connected) return;
                State = ConnectionState.Connecting;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _client = new TcpClient();

                using var timeout = new CancellationTokenSource(_settings.ConnectTimeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct);

                await _client.ConnectAsync(_settings.Host, _settings.Port, linked.Token);
                _stream = _client.GetStream();
                State = ConnectionState.Connected;

                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
            }
            catch (Exception ex)
            {
                State = ConnectionState.Disconnected;
                throw new Exception($"연결 실패 [{_settings.Host}:{_settings.Port}]: {ex.Message}", ex);
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            _cts?.Cancel();
            State = ConnectionState.Disconnected;
            _stream?.Close();
            _client?.Close();

            foreach (var kv in _pending)
                kv.Value.TrySetCanceled();
            _pending.Clear();

            await Task.CompletedTask;
        }

        // ─── 전송 ────────────────────────────────────────────────
        public async Task SendAsync(Message message, CancellationToken ct = default)
        {
            EnsureConnected();

            message.Header ??= new MessageHeader();
            message.Header.UnitName = _settings.UnitName;
            message.Header.Time = DateTime.Now;

            var data = Encoding.UTF8.GetBytes(message.ToXml());

            await _sendLock.WaitAsync(ct);
            try { await _stream!.WriteAsync(data, 0, data.Length, ct); }
            finally { _sendLock.Release(); }
        }

        // ─── 요청-응답 ───────────────────────────────────────────
        public async Task<RequestResult> RequestAsync(
            Message request,
            string? responseMessageName = null,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
            EnsureConnected();

            var reqName = request.Header?.MessageName
                              ?? throw new ArgumentException("MessageName이 설정되지 않았습니다.");
        var responseKey = responseMessageName ?? reqName.Replace("REQUEST", "REPLY", StringComparison.OrdinalIgnoreCase);

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

        // ─── 수신 루프 ───────────────────────────────────────────
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            var buf = new byte[_settings.ReceiveBufferSize];
            var delimiter = _settings.MessageDelimiter;

            try
            {
                while (!ct.IsCancellationRequested && _stream is not null)
                {
                    int read = await _stream.ReadAsync(buf, 0, buf.Length, ct);
                    if (read == 0) break;

                    sb.Append(Encoding.UTF8.GetString(buf, 0, read));

                    string raw = sb.ToString();
                    int idx;
                    while ((idx = raw.IndexOf(delimiter, StringComparison.Ordinal)) >= 0)
                    {
                        int end = idx + delimiter.Length;
                        string chunk = raw[..end];
                        raw = raw[end..];
                        HandleIncoming(chunk);
                    }
                    sb.Clear();
                    sb.Append(raw);
                }
            }
            catch (OperationCanceledException) { }
            catch
            {
                State = ConnectionState.Disconnected;
                if (_settings.AutoReconnect)
                    _ = Task.Run(ReconnectLoopAsync, CancellationToken.None);
            }
        }

        private void HandleIncoming(string xml)
        {
            try
            {
                var msg = Message.FromXml(xml);
                var msgName = msg.Header?.MessageName ?? string.Empty;

                if (_pending.TryRemove(msgName, out var tcs))
                    tcs.TrySetResult(msg);          // 대기 중인 Request에 응답
                else
                    MessageReceived?.Invoke(this, msg); // Push 메시지
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpCommunicator] 파싱 오류: {ex.Message}");
            }
        }

        // ─── 재연결 ──────────────────────────────────────────────
        private async Task ReconnectLoopAsync()
        {
            while (State != ConnectionState.Connected)
            {
                State = ConnectionState.Reconnecting;
                await Task.Delay(_settings.ReconnectInterval);
                try
                {
                    _client?.Dispose();
                    _client = new TcpClient();
                    await _client.ConnectAsync(_settings.Host, _settings.Port);
                    _stream = _client.GetStream();
                    State = ConnectionState.Connected;
                    _cts = new CancellationTokenSource();
                    _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                }
                catch { /* 재시도 */ }
            }
        }

        private void EnsureConnected()
        {
            if (State != ConnectionState.Connected)
                throw new InvalidOperationException("TCP 연결이 되어 있지 않습니다.");
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
            _sendLock.Dispose();
            _connectLock.Dispose();
            _cts?.Dispose();
        }
    }
}