using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    /// <summary>
    /// 연결된 하나의 TCP 세션 (스트림) 에 대한 송수신 처리.
    /// 클라이언트/서버 공통으로 사용.
    /// </summary>
    public class TcpSession : IDisposable
    {
        private readonly NetworkStream _stream;
        private readonly string _unitName;
        private readonly string _delimiter;
        private readonly int _bufferSize;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<Message>> _pending = new();

        public event EventHandler<Message>? MessageReceived;
        public event EventHandler? Disconnected;

        public TcpSession(NetworkStream stream, string unitName, string delimiter = "</MESSAGE>", int bufferSize = 65536)
        {
            _stream = stream;
            _unitName = unitName;
            _delimiter = delimiter;
            _bufferSize = bufferSize;
        }

        public void Start()
        {
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        // ─── 전송 (Fire & Forget) ────────────────────────────────
        public async Task SendAsync(Message message, CancellationToken ct = default)
        {
            message.Header ??= new MessageHeader();
            message.Header.UnitName = _unitName;
            message.Header.Time = DateTime.Now;

            var data = Encoding.UTF8.GetBytes(message.ToXml());

            await _sendLock.WaitAsync(ct);
            try { await _stream.WriteAsync(data, ct); }
            finally { _sendLock.Release(); }
        }

        // ─── 요청-응답 ───────────────────────────────────────────
        public async Task<RequestResult> RequestAsync(
            Message request,
            string? responseMessageName = null,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
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

        // ─── 수신 루프 ───────────────────────────────────────────
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var sb = new StringBuilder();
            var buf = new byte[_bufferSize];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await _stream.ReadAsync(buf, 0, buf.Length, ct);
                    if (read == 0) break;

                    sb.Append(Encoding.UTF8.GetString(buf, 0, read));
                    string raw = sb.ToString();
                    int idx;

                    while ((idx = raw.IndexOf(_delimiter, StringComparison.Ordinal)) >= 0)
                    {
                        int end = idx + _delimiter.Length;
                        string chunk = raw[..end];
                        raw = raw[end..];
                        HandleIncoming(chunk);
                    }
                    sb.Clear();
                    sb.Append(raw);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* 연결 끊김 */ }
            finally
            {
                foreach (var kv in _pending)
                    kv.Value.TrySetCanceled();
                _pending.Clear();
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }

        private void HandleIncoming(string xml)
        {
            try
            {
                var msg = Message.FromXml(xml);
                var msgName = msg.Header?.MessageName ?? string.Empty;

                if (_pending.TryRemove(msgName, out var tcs))
                    tcs.TrySetResult(msg);              // RequestAsync 응답
                else
                    MessageReceived?.Invoke(this, msg); // Push / 상대방 요청
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpSession] 파싱 오류: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _stream.Close();
            _sendLock.Dispose();
            _cts.Dispose();
        }
    }
}