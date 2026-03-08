using HCB.Data.Entity;
using HCB.IoC;
using Serilog;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telerik.Licensing.Json;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.DataVisualization.Map.BingRest;
using Telerik.Windows.Diagrams.Core;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public class EqpCommunicationService : IDisposable
    {
        private readonly EqpTcpServer _server;
        private readonly ILogger _logger;

        // EQP Service
        private SequenceServiceVM sequenceServiceVM;
        private SequenceHelper sequenceHelper;
        private AlarmService alarmService;

        // HeartBeat
        private Timer? _heartbeatTimer;
        private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(3);
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(5);
        private int _heartbeatRunning = 0; // 중복 실행 방지 (0=idle, 1=running)

        public ConnectionState State => _server.State;

        public EqpCommunicationService(ILogger logger, SequenceServiceVM sequenceServiceVM, SequenceHelper sequenceHelper, AlarmService alarmService)
        {
            _logger = logger;
            this.sequenceServiceVM = sequenceServiceVM;
            this.sequenceHelper = sequenceHelper;
            this.alarmService = alarmService;

            var settings = new TcpSettings();
            _server = new EqpTcpServer(settings);
            _server.MessageReceived += OnMessageReceived;
            _server.ConnectionStateChanged += OnConnectionStateChanged;
            _server.LogMessage += (_, msg) => _logger.Information($"[EQP] {msg}");
        }

        public void Start() => _server.Start();
        public void Stop()
        {
            StopHeartbeat();
            _server.Stop();
        }

        // ─── HeartBeat 타이머 ────────────────────────────────────

        private void OnConnectionStateChanged(object? sender, ConnectionState state)
        {
            _logger.Information($"[EQP] 연결 상태: {state}");

            if (state == ConnectionState.Connected)
                StartHeartbeat();
            else
                StopHeartbeat();
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeatTimer = new Timer(
                callback: _ => _ = SendHeartbeatAsync(),
                state: null,
                dueTime: _heartbeatInterval, // 첫 전송은 연결 후 3초 뒤
                period: _heartbeatInterval
            );
            _logger.Information("[HeartBeat] 타이머 시작 (간격: 3s, 타임아웃: 5s)");
        }

        private void StopHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        private async Task SendHeartbeatAsync()
        {
            // 이전 HeartBeat가 아직 완료되지 않으면 스킵
            if (Interlocked.CompareExchange(ref _heartbeatRunning, 1, 0) != 0)
            {
                _logger.Warning("[HeartBeat] 이전 요청 진행 중 - 스킵");
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(_heartbeatTimeout);
                var success = await HeartBeat(cts.Token);                
                sequenceServiceVM.VisionStatus = success;    
            }
            catch (OperationCanceledException)
            {
                _logger.Warning($"[HeartBeat] 타임아웃 ({_heartbeatTimeout.TotalSeconds}s 초과)");
                sequenceServiceVM.VisionStatus = false;
            }
            catch (Exception ex)
            {
                _logger.Warning($"[HeartBeat] 오류: {ex.Message}");
                sequenceServiceVM.VisionStatus = false;
            }
            finally
            {
                Interlocked.Exchange(ref _heartbeatRunning, 0);
            }
        }

        #region EQP -> VISION
        // ─── EQP → Vision 요청 ───────────────────────────────────
        // HEART BEAT
        public async Task<bool> HeartBeat(CancellationToken ct = default)
        {
            var request = MessageFactory.Create("HEARTBEAT", "EQP");
            var result = await _server.RequestAsync(request, ct: ct);

            return result.Success;
        }

        public async Task<bool> RequestAFStart(CameraType cameraType, CancellationToken ct = default)
        {
            var request = MessageFactory.Create("REQUEST_AF_START", "EQP", $"<CAMERATYPE>{cameraType}</CAMERATYPE>");
            var result = await _server.RequestAsync(request, "REQUEST_AF_END", TimeSpan.FromSeconds(60), ct: ct);

            var afResult = ParseResult(result);
            await NotifyAFEnd(afResult, ct);
            return afResult == Result.OK;
        }

        private Result ParseResult(RequestResult result)
        {
            if (!result.Success) return Result.NG;
            try
            {
                var content = result.Response!.Data?.Content;
                var xml = XElement.Parse($"<DATA>{content}</DATA>");
                return Enum.TryParse(xml.Element("RESULT")?.Value, out Result r) ? r : Result.NG;
            }
            catch
            {
                return Result.NG;
            }
        }

        private async Task NotifyAFEnd(Result afResult, CancellationToken ct)
        {
            var resultStr = afResult == Result.OK ? "OK" : "NG";
            var end = MessageFactory.Create("RESPONSE_AF_END", "EQP", $"<RESULT>{resultStr}</RESULT>");
            await _server.RequestAsync(end, ct: ct);
        }


        // 레시피 변경 요청
        public async Task RequestRecipeChange(string recipeId, CancellationToken ct = default)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST-RECIPE-CHANGE",
                unitName: "EQP",
                content: $"<RECIPE_ID>{recipeId}</RECIPE_ID>"
            );
            var result = await _server.RequestAsync(request, ct: ct);

            if (!result.Success)
                _logger.Warning($"[RecipeChange] 요청 실패: {result.ErrorMessage}");
            else
                _logger.Information(result.Response!.Data?.Content ?? "");
        }

        // Align 요청
        public async Task<VisionMarkPositionResponse> RequestVisionMarkPosition(MarkType markType, CameraType cameraType)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_VISIONMARK_POSITION",
                unitName: "EQP",
                content: $"<MARKTYPE>{markType}</MARKTYPE><CAMERATYPE>{cameraType}</CAMERATYPE>"
            );
            var result = await _server.RequestAsync(request, timeout: TimeSpan.FromSeconds(10));

            if (!result.Success)
            {
                _logger.Warning($"[MarkPosition] 요청 실패: {result.ErrorMessage}");
                return new VisionMarkPositionResponse { Result = Result.NG };
            }

            var response = VisionMarkPositionResponse.Parse(result.Response!.Data?.Content);
            if (response.Result == Result.OK)
                return new VisionMarkPositionResponse { Result = response.Result, X = response.X, Y = response.Y, Theta = response.Theta };
            else
                return new VisionMarkPositionResponse { Result = Result.NG };
        }

        

        #endregion

        #region VISION -> EQP
        private async void OnMessageReceived(object? sender, Message msg)
        {
            var msgName = msg.Header?.MessageName ?? "";
            _logger.Information($"[EQP] Vision 명령 수신: {msgName}");
            try
            {
                switch (msgName)
                {
                    // 비전 상태 정보 보고
                    case "REQUEST_VISION_STATUS":
                        await ReplyEqpStatus(msg);
                        break;
                    // 현재 레시피 확인 요청 "REQUEST-CURRENT-RECIPE"


                    case "REQUEST_MOTION_MOVE":
                        await HandleMotionMove(msg);
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Warning($"VISION 통신 중 에러 발생 {e.Message}");
            }
        }

        private async Task ReplyEqpStatus(Message msg, CancellationToken ct = default)
        {
            var msgName = msg.Header?.MessageName ?? "";
            var replyName = msgName.Replace("REQUEST_", "REPLY_");

            try
            {
                if (!string.IsNullOrEmpty(msg.Data?.Content))
                {
                    var innerXml = XElement.Parse($"<R>{msg.Data.Content}</R>");

                    string alarmStatus = innerXml.Element("ALARM")?.Value ?? "";
                    if (alarmStatus.Equals("UP"))
                    {
                        sequenceServiceVM.VisionAlarm = true;
                    }
                    else if(alarmStatus.Equals("DOWN"))
                    {
                        sequenceServiceVM.VisionAlarm = false;
                        await alarmService.SetAlarm("E0030");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }

            var responseContent = new MotionMoveResult { Result = true };

            var response = MessageFactory.Create(
                messageName: replyName,
                unitName: "EQP",
                content: responseContent.ToXml()
            );

            await _server.SendAsync(response);
        }

        //public async Task<bool> ReplyAFEnd(Message msg, CancellationToken ct = default)
        //{
        //    var msgName = msg.Header?.MessageName ?? "";
        //    var replyName = msgName.Replace("REQUEST_", "REPLY_");
        //    Result result = Result.NG;
        //    try
        //    {
        //        if (!string.IsNullOrEmpty(msg.Data?.Content))
        //        {
        //            var xml = msg.Data?.ToXml();
                    
        //            if (Enum.TryParse(xml?.Element("RESULT")?.Value, out Result r))
        //                result = r;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(ex.Message);
        //    }finally
        //    {
        //        var responseContent = new MotionMoveResult { Result = true };

        //        var response = MessageFactory.Create(
        //            messageName: replyName,
        //            unitName: "EQP",
        //            content: responseContent.ToXml()
        //        );
        //        await _server.SendAsync(response);
        //    }
        //    return result == Result.OK;


        //}

        private async Task HandleMotionMove(Message msg, CancellationToken ct = default)
        {
            var msgName = msg.Header?.MessageName ?? "";
            var replyName = msgName.Replace("REQUEST_", "REPLY_");

            Result result = Result.NG;
            string axis = "";
            double distance = 0;

            try
            {
                if (!string.IsNullOrEmpty(msg.Data?.Content))
                {
                    var innerXml = XElement.Parse($"<R>{msg.Data.Content}</R>");

                    axis = innerXml.Element("AXIS")?.Value ?? "";
                    distance = double.TryParse(innerXml.Element("DISTANCE")?.Value, out var d) ? d : 0;

                    var validAxes = new HashSet<string> { "H_X", "H_Z", "H_T", "D_Y", "P_Y", "W_Y", "W_T" };

                    if (validAxes.Contains(axis.ToUpperInvariant()))
                    {
                        result = await sequenceHelper.RelativeMoveAsync(axis, 0, distance, ct) ? Result.OK : Result.NG;
                    }
                }
            }
            catch (Exception ex)
            {
                result = Result.NG;
            }

            var currentPosition = sequenceHelper.CurrentPosition(axis);
            var r = result == Result.OK ? "OK": "NG";
            var response = MessageFactory.Create(
                messageName: replyName,
                unitName: "EQP",
                content:$"<RESULT>{result}</RESULT><DISTANCE>{currentPosition}</DISTANCE>"
            );

            await _server.SendAsync(response);
        }
        #endregion

        public void Dispose()
        {
            StopHeartbeat();
            _server.Dispose();
        }
    }
}