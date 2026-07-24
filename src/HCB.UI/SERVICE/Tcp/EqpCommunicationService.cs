
using HCB.Data.Entity;
using HCB.IoC;
using Serilog;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        private ECParamService ecParamService;

        // HeartBeat
        private Timer? _heartbeatTimer;
        private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(20);
        private int _heartbeatRunning = 0; // 중복 실행 방지 (0=idle, 1=running)

        public ConnectionState State => _server.State;

        public EqpCommunicationService(ILogger logger, SequenceServiceVM sequenceServiceVM, SequenceHelper sequenceHelper, AlarmService alarmService, ECParamService eCParamService)
        {
            _logger = logger;
            this.sequenceServiceVM = sequenceServiceVM;
            this.sequenceHelper = sequenceHelper;
            this.alarmService = alarmService;
            this.ecParamService = eCParamService;

            var settings = new TcpSettings();
            _server = new EqpTcpServer(settings);
            _server.MessageReceived += OnMessageReceived;
            _server.ConnectionStateChanged += OnConnectionStateChanged;
            //_server.LogMessage += (_, msg) => _logger.Information($"[EQP] {msg}");
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
                //_logger.Warning("[HeartBeat] 이전 요청 진행 중 - 스킵");
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

        public async Task<bool> RequestAFStart(CameraType cameraType, MarkType markType , CancellationToken ct = default)
        {
            var request = MessageFactory.Create("REQUEST_AF_START", "EQP", $"<CAMERATYPE>{cameraType}</CAMERATYPE><MARKTYPE>{markType}</MARKTYPE>");
            var result = await _server.RequestAsync(request, "REQUEST_AF_END", TimeSpan.FromSeconds(60), ct: ct);

            var afResult = ParseResult(result);
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


        // 레시피 변경 요청  1: 글래스, 2: 실리콘
        public async Task RequestRecipeChange(string recipeId, CancellationToken ct = default)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_RECIPE_CHANGE",
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
        public async Task<VisionMarkPositionResponse> RequestVisionMarkPosition(MarkType markType, CameraType cameraType, string direct, bool avgMode = true)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_VISIONMARK_POSITION",
                unitName: "EQP",
                content: $"<MARKTYPE>{markType}</MARKTYPE><CAMERATYPE>{cameraType}</CAMERATYPE><DIRECT>{direct}</DIRECT><AVGMODE>{avgMode}</AVGMODE>"
            );

            double pcWT = Double.Parse(ecParamService.FindByName(MotionExtensions.PC_W_T).Value);
            double t = cameraType switch
            {
                CameraType.HC1_HIGH => Double.Parse(ecParamService.FindByName(MotionExtensions.HC1_T).Value) + pcWT,
                CameraType.HC2_HIGH => Double.Parse(ecParamService.FindByName(MotionExtensions.HC2_T).Value) + pcWT,
                CameraType.PC_HIGH => Double.Parse(ecParamService.FindByName(MotionExtensions.PC_T).Value),
                _ => 0
            };

            double fov = cameraType switch
            {
                CameraType.HC1_HIGH => 7.2,
                CameraType.HC2_HIGH => 7.2,
                CameraType.PC_HIGH => 10.0,
                CameraType.HC_LOW => 110.0,
                _ => 0
            };

            var result = await _server.RequestAsync(request, timeout: TimeSpan.FromSeconds(10));

            if (!result.Success)
            {
                _logger.Warning($"[MarkPosition] 요청 실패: {result.ErrorMessage}");
                return new VisionMarkPositionResponse { Result = Result.NG };
            }

            var response = VisionMarkPositionResponse.Parse(result.Response!.Data?.Content);

            if (response.Result != Result.OK)
                return new VisionMarkPositionResponse { Result = Result.NG };

            // FOV 범위 체크 (FOV의 절반이 유효 범위)
            if (markType != MarkType.VERNIER)
            {
                double half = fov / 2.0;
                if (Math.Abs(response.X) > half || Math.Abs(response.Y) > half)
                {
                    _logger.Warning($"[MarkPosition] FOV 범위 초과 - X:{response.X:F3}, Y:{response.Y:F3}, FOV:{fov}");
                    return new VisionMarkPositionResponse { Result = Result.NG };
                }
            }

            var xy = CalibrationMath.ApplyRotation(Point2D.of(response.X, response.Y), t);

            return new VisionMarkPositionResponse
            {
                Result = response.Result,
                X = xy.X,
                Y = xy.Y,
                Theta = response.Theta
            };
        }


        // BtmDie의 모든 마크들을 측정
        public async Task<BtmMarkResponse> RequestHeadAlign(DirectType directType = DirectType.BOTH, bool avgMode = true)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_HEAD_ALIGN_SEQUENCE",
                unitName: "EQP",
                content: $"<SIDES>{directType}</SIDES><AVGMODE>{avgMode}</AVGMODE>"
            );

            double pcWT = Double.Parse(ecParamService.FindByName(MotionExtensions.PC_W_T).Value);
            double hc1T = Double.Parse(ecParamService.FindByName(MotionExtensions.HC1_T).Value) + pcWT;
            double hc2T = Double.Parse(ecParamService.FindByName(MotionExtensions.HC2_T).Value) + pcWT;
            double fov = 7.2;

            var result = await _server.RequestAsync(request, timeout: TimeSpan.FromMinutes(5));

            if (!result.Success)
            {
                _logger.Warning($"[MarkPosition] 요청 실패: {result.ErrorMessage}");
                return new BtmMarkResponse { Result = Result.NG };
            }

            var response = BtmMarkResponse.Parse(result.Response!.Data?.Content);

            if (response.Result != Result.OK)
                return new BtmMarkResponse { Result = Result.NG };

           
            response.LeftFid = CalibrationMath.ApplyRotation(response.LeftFid, hc1T);
            response.LeftAlign = CalibrationMath.ApplyRotation(response.LeftAlign, hc1T);
            response.RightFid = CalibrationMath.ApplyRotation(response.RightFid, hc2T);
            response.RightAlign = CalibrationMath.ApplyRotation(response.RightAlign, hc2T);
            //// 임시 스케일 적용
            //response.LeftFid = Point2D.of(response.LeftFid.X * 0.87944, response.LeftFid.Y * 1.00238);
            //response.RightFid = Point2D.of(response.RightFid.X * 0.87944, response.RightFid.Y * 1.00238);
            return response;
        }


        public async Task<VernierResponse> RequestVernier(CameraType cameraType, DirectType direct)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_VISIONMARK_POSITION_V",
                unitName: "EQP",
                content: $"<CAMERATYPE>{cameraType}</CAMERATYPE><DIRECT>{direct}</DIRECT>"
            );

            var result = await _server.RequestAsync(request, timeout: TimeSpan.FromSeconds(10));

            if (!result.Success)
            {
                _logger.Warning($"[MarkPosition] 요청 실패: {result.ErrorMessage}");
                return new VernierResponse { Result = Result.NG };
            }
            var response = VernierResponse.Parse(result.Response!.Data?.Content);
            if (response.Result != Result.OK)
                return new VernierResponse { Result = Result.NG };
            return response;
        }

        // ─── Wafer Edge 검출 (HC 저배율, 시계 위치 12/4/7시) ───────────────
        // 현재 카메라 FOV 안의 웨이퍼 엣지를 찾아 카메라 중심 대비 오프셋(mm)을 반환.
        // 저배율은 FOV(≈110mm) 안에 3점 동시 촬상이 불가하므로 EQP가 12→4→7시로
        // 이동하며 각 위치마다 1회 요청한다. clock은 검출 ROI/엣지 방향 힌트.
        public async Task<VisionMarkPositionResponse> RequestWaferEdge(WaferClock clock, CancellationToken ct = default)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_WAFER_EDGE",
                unitName: "EQP",
                content: $"<CAMERATYPE>{CameraType.HC_LOW}</CAMERATYPE><CLOCK>{(int)clock}</CLOCK>"
            );

            var result = await _server.RequestAsync(request, timeout: TimeSpan.FromSeconds(10), ct: ct);

            if (!result.Success)
            {
                _logger.Warning($"[WaferEdge] 요청 실패: {result.ErrorMessage}");
                return new VisionMarkPositionResponse { Result = Result.NG };
            }

            var response = VisionMarkPositionResponse.Parse(result.Response!.Data?.Content);
            if (response.Result != Result.OK)
                return new VisionMarkPositionResponse { Result = Result.NG };

            // HC_LOW FOV(110mm) 범위 체크 (절반이 유효 범위)
            const double fov = 110.0;
            double half = fov / 2.0;
            if (Math.Abs(response.X) > half || Math.Abs(response.Y) > half)
            {
                _logger.Warning($"[WaferEdge] FOV 범위 초과 - X:{response.X:F3}, Y:{response.Y:F3}, FOV:{fov}");
                return new VisionMarkPositionResponse { Result = Result.NG };
            }

            return response;
        }

        // ─── Scribe Line 검출 (HC 저배율 / HC1 / HC2 통합) ───────────────
        // cameraType으로 저배율(HC_LOW)·고배율(HC1_HIGH/HC2_HIGH)을 모두 처리.
        // direct = 검출할 스크라이브 방향(Horizontal/Vertical).
        // 반환: 라인 기준점 오프셋(X/Y, mm) + 라인 기울기(Theta, 도).
        public async Task<ScribeLineResponse> RequestScribeLine(CameraType cameraType, DirectType direct, CancellationToken ct = default)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_SCRIBE_LINE",
                unitName: "EQP",
                content: $"<CAMERATYPE>{cameraType}</CAMERATYPE><DIRECT>{direct}</DIRECT>"
            );

            double pcWT = Double.Parse(ecParamService.FindByName(MotionExtensions.PC_W_T).Value);
            double t = cameraType switch
            {
                CameraType.HC1_HIGH => Double.Parse(ecParamService.FindByName(MotionExtensions.HC1_T).Value) + pcWT,
                CameraType.HC2_HIGH => Double.Parse(ecParamService.FindByName(MotionExtensions.HC2_T).Value) + pcWT,
                _ => 0
            };

            double fov = cameraType switch
            {
                CameraType.HC1_HIGH => 7.2,
                CameraType.HC2_HIGH => 7.2,
                CameraType.HC_LOW => 110.0,
                _ => 0
            };

            var result = await _server.RequestAsync(request, timeout: TimeSpan.FromSeconds(10), ct: ct);

            if (!result.Success)
            {
                _logger.Warning($"[ScribeLine] 요청 실패: {result.ErrorMessage}");
                return new ScribeLineResponse { Result = Result.NG };
            }

            var response = ScribeLineResponse.Parse(result.Response!.Data?.Content);
            if (response.Result != Result.OK)
                return new ScribeLineResponse { Result = Result.NG };

            // FOV 범위 체크 (회전 보정 전 원시 좌표 기준 — RequestVisionMarkPosition과 동일 순서)
            if (fov > 0)
            {
                double half = fov / 2.0;
                if (Math.Abs(response.X) > half || Math.Abs(response.Y) > half)
                {
                    _logger.Warning($"[ScribeLine] FOV 범위 초과 - X:{response.X:F3}, Y:{response.Y:F3}, FOV:{fov}");
                    return new ScribeLineResponse { Result = Result.NG };
                }
            }

            // 고배율(HC1/HC2)은 카메라 장착각(t)만큼 좌표 회전 보정
            if (t != 0)
            {
                var xy = CalibrationMath.ApplyRotation(Point2D.of(response.X, response.Y), t);
                response.X = xy.X;
                response.Y = xy.Y;
            }

            return response;
        }

        public async Task<Result> PiezoHome(CancellationToken ct = default)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST_PIEZO_HOME",
                unitName: "EQP",
                content: null
            );
            var result = await _server.RequestAsync(request, timeout: TimeSpan.FromMinutes(1), ct: ct);

            var content = result.Response!.Data?.Content;
            var xml = XElement.Parse($"<DATA>{content}</DATA>");
            return Enum.TryParse(xml.Element("RESULT")?.Value, out Result r) ? r : Result.NG;
        }

        #endregion

        #region VISION -> EQP
        private async void OnMessageReceived(object? sender, Message msg)
        {
            var msgName = msg.Header?.MessageName ?? "";
            try
            {
                switch (msgName)
                {
                    // 비전 상태 정보 보고
                    case "REQUEST_VISION_STATUS":
                        _logger.Information($"[EQP] Vision 명령 수신: {msgName}");
                        await ReplyEqpStatus(msg);
                        break;
                    // 현재 레시피 확인 요청 "REQUEST-CURRENT-RECIPE"


                    case "REQUEST_MOTION_MOVE":
                        _logger.Information($"[EQP] Vision 명령 수신: {msgName}");
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
                        result = await sequenceHelper.RelativeMoveAsync(axis, 100, distance, ct) ? Result.OK : Result.NG;
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
                content:$"<RESULT>{result}</RESULT><AXIS>{axis.ToUpper()}</AXIS><DISTANCE>{currentPosition}</DISTANCE>"
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