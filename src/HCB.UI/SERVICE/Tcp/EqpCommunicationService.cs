using HCB.Data.Entity;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telerik.Licensing.Json;
using Telerik.Windows.Controls;

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

        public ConnectionState State => _server.State;

        public EqpCommunicationService(ILogger logger, SequenceServiceVM sequenceServiceVM, SequenceHelper sequenceHelper)
        {
            _logger = logger;
            this.sequenceServiceVM = sequenceServiceVM;
            this.sequenceHelper = sequenceHelper;

            var settings = new TcpSettings();
            _server = new EqpTcpServer(settings);
            _server.MessageReceived += OnMessageReceived;
            _server.ConnectionStateChanged += (_, state) => _logger.Information($"[EQP] 연결 상태: {state}");
            _server.LogMessage += (_, msg) => _logger.Information($"[EQP] {msg}");
        }

        public void Start() => _server.Start();
        public void Stop() => _server.Stop();

        #region EQP -> VISION
        // ─── EQP → Vision 요청 ───────────────────────────────────
        // HEART BEAT
        public async Task<bool> HeartBeat(CancellationToken ct = default)
        {
            var request = MessageFactory.Create("HEARTBEAT", "EQP");
            var result = await _server.RequestAsync(request, ct: ct);

            return result.Success;
        }

        // 비전 상태 정보 요청
        public async Task RequestVisionStatus(CancellationToken ct = default)
        {
            var request = MessageFactory.Create("REQUEST-VISION-SATATUS", "EQP");
            var result = await _server.RequestAsync(request, ct: ct);

            if (!result.Success)
                _logger.Warning($"[VisionStatus] 요청 실패: {result.ErrorMessage}");
            else
                _logger.Information(result.Response!.Data?.Content ?? "");
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

        public async Task RequestVisionMarkPosition(MarkType markType, CameraType cameraType)
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST-VISIONMARK-POSITION",
                unitName: "EQP",
                content: $"<MARKTYPE>{markType}</MARKTYPE><CAMERATYPE>{cameraType}</CAMERATYPE>"
            );
            var result = await _server.RequestAsync(request);

            if (!result.Success)
            {
                _logger.Warning($"[MarkPosition] 요청 실패: {result.ErrorMessage}");
                return;
            }

            var response = VisionMarkPositionResponse.Parse(result.Response!.Data?.Content);
            if (response.Result == Result.OK)
                _logger.Information($"X={response.X}, Y={response.Y}, Theta={response.Theta}, Score={response.Score}");
            else
                _logger.Warning("[MarkPosition] 결과 NG");
        }

        // AutoFocusing 요청 : AF방식 협의 필요 ( 미완성 )
        public async Task RequestAFStart()
        {
            var request = MessageFactory.Create(
                messageName: "REQUEST-AF-START",
                unitName: "EQP",
                content: null
            );
            var result = await _server.RequestAsync(request);

            if (!result.Success)
            {
                _logger.Warning($"[MarkPosition] 요청 실패: {result.ErrorMessage}");
                return;
            }

            var response = VisionMarkPositionResponse.Parse(result.Response!.Data?.Content);
            if (response.Result == Result.OK)
                _logger.Information($"X={response.X}, Y={response.Y}, Theta={response.Theta}, Score={response.Score}");
            else
                _logger.Warning("[MarkPosition] 결과 NG");
        }

        // AutoFocusing 요청
        public async Task RequestAutoFocusing(CancellationToken ct = default)
        {
            var request = MessageFactory.Create(
                    messageName: "REQUEST-AUTOFOCUS-START",
                    unitName: "EQP",
                    content: null
            );

            await _server.SendAsync(request, ct);
        }
        #endregion

        #region VISION -> EQP
        private async void OnMessageReceived(object? sender, Message msg)
        {
            var msgName = msg.Header?.MessageName ?? "";
            _logger.Information($"[EQP] Vision 명령 수신: {msgName}");
            try
            {
                switch(msgName)
                {
                    // 설비 상태 정보 요청
                    case "REQUEST-UNIT-STATUS":
                        await ReplyEqpStatus();
                        break;
                    // 현재 레시피 확인 요청 "REQUEST-CURRENT-RECIPE"
                    
                    // Z축 이동 
                    case "REQUEST-AF-ZMOVE":
                        break;
                    // Auto Focusing End

                    case "REPLY-AF-END":
                        break;

                    case "REQUEST-MOTION-MOVE":
                        await HandleMotionMove(msg);
                        break;
                    //// X축 제어 + 
                    //case "REQUEST-MOTION-PLUS-X":
                    //    await HandleMotionMove(msg);
                    //    break;
                    //// X축 제어 -
                    //case "REQUEST-MOTION-MINUS-X":
                    //    await HandleMotionMove(msg);
                    //    break;

                    //// Z축 제어 +
                    //case "REQUEST-MOTION-PLUS-Z":
                    //    await HandleMotionMove(msg);
                    //    break;

                    //// Z축 제어 -
                    //case "REQUEST-MOTION-MINUS-Z":
                    //    await HandleMotionMove(msg);
                    //    break;
                }
            }catch(Exception e)
            {
                _logger.Warning($"VISION 통신 중 에러 발생 {e.Message}");
            }
        }

        private async Task ReplyEqpStatus(CancellationToken ct = default)
        {
            var request = MessageFactory.Create(
                messageName: "REPLY-EQP-STATUS",
                unitName: "EQP",
                content: $"<UNITSTATUS>{sequenceServiceVM.Availability.ToString()}</UNITSTATUS>"
            );

            await _server.SendAsync(request, ct);
        }

        private async Task ReplyAFEnd(Message msg, CancellationToken ct = default)
        {
            
        }

        private async Task HandleMotionMove(Message msg, CancellationToken ct = default)
        {
            var msgName = msg.Header?.MessageName ?? "";
            var replyName = msgName.Replace("REQUEST-", "REPLY-");

            bool result = false;
            string axis = "";
            string direction = "";
            double distance = 0;

            try
            {
                // 1. Data.Content 내부의 XML 문자열 파싱 (핵심 수정 부분)
                if (!string.IsNullOrEmpty(msg.Data?.Content))
                {
                    // 루트가 없는 여러 태그를 읽기 위해 <R>로 감싸서 파싱
                    var innerXml = XElement.Parse($"<R>{msg.Data.Content}</R>");

                    axis = innerXml.Element("AXIS")?.Value ?? "";
                    direction = (innerXml.Element("DIRECTION")?.Value ?? "").ToUpperInvariant();
                    distance = double.TryParse(innerXml.Element("DISTANCE")?.Value, out var d) ? d : 0;

                    // 2. 유효성 검증
                    var validAxes = new HashSet<string> { "H_X", "H_Z", "H_T", "D_Y", "P_Y", "W_Y", "W_T" };
                    var validDirections = new HashSet<string> { "PLUS", "MINUS" };

                    if (validAxes.Contains(axis.ToUpperInvariant()) && validDirections.Contains(direction))
                    {
                        // 3. 실제 이동 로직 수행
                        double sign = direction == "PLUS" ? 1.0 : -1.0;
                        double signedDistance = distance * sign;

                        // 시퀀스 헬퍼 호출
                        result = await sequenceHelper.RelativeMoveAsync(axis, 0, signedDistance, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                // 로그 기록 (예: _logger.LogError(ex, "Motion Move Error");)
                result = false;
            }

            // 4. 응답 메시지 생성 및 전송
            var responseContent = new MotionMoveResult { Result = result };

            var response = MessageFactory.Create(
                messageName: replyName,
                unitName: "EQP",
                content: responseContent.ToXml() // MotionMoveResult가 XML 문자열을 반환한다고 가정
            );

            await _server.SendAsync(response);
        }

        //private async Task HandleMotionMove(Message msg, CancellationToken ct = default)
        //{
        //    var msgName = msg.Header?.MessageName ?? "";
        //    var reply = msgName.Replace("REQUEST-", "REPLY-");
        //    var request = msg.Data.ToXml();

        //    string axis = request.Element("AXIS")?.Value ?? "";
        //    string direction = request.Element("DIRECTION")?.Value ?? "";
        //    double distance = double.TryParse(request.Element("DISTANCE")?.Value, out var dist) ? dist : 0;

        //    // 유효한 AXIS / DIRECTION 검증
        //    var validAxes = new HashSet<string> { "H-X", "H-Z", "H-T", "D-Y", "P-Y", "W-Y", "W-T" };
        //    var validDirections = new HashSet<string> { "PLUS", "MINUS" };

        //    bool result = false;

        //    if (request != null
        //        && validAxes.Contains(axis)
        //        && validDirections.Contains(direction.ToUpperInvariant()))
        //    {
        //        double sign = direction.Equals("PLUS", StringComparison.OrdinalIgnoreCase) ? 1.0 : -1.0;
        //        double signedDistance = distance * sign;

        //        result = await sequenceHelper.RelativeMoveAsync(axis, 0, signedDistance, ct);
        //    }

        //    var responseContent = new MotionMoveResult
        //    {
        //        //Axis = axis,
        //        //Direction = direction,
        //        //Distance = distance,
        //        Result = result
        //    };

        //    var response = MessageFactory.Create(
        //        messageName: reply,
        //        unitName: "EQP",
        //        content: responseContent.ToXml()
        //    );
        //    await _server.SendAsync(response);
        //}
        #endregion

        public void Dispose() => _server.Dispose();
    }
}