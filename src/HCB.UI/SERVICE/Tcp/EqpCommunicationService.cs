using HCB.Data.Entity;
using HCB.IoC;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
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

                    // X축 제어 + 
                    case "REQUEST-MOTION-PLUS-X":
                        await HandleMotionMove(msg);
                        break;
                    // X축 제어 -
                    case "REQUEST-MOTION-MINUS-X":
                        await HandleMotionMove(msg);
                        break;

                    // Z축 제어 +
                    case "REQUEST-MOTION-PLUS-Z":
                        await HandleMotionMove(msg);
                        break;

                    // Z축 제어 -
                    case "REQUEST-MOTION-MINUS-Z":
                        await HandleMotionMove(msg);
                        break;
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
            var reply = msgName.Replace("REQUEST-", "REPLY-");
            // 메시지명에서 축 이름과 방향 파싱
            // 예: REQUEST-MOTION-PLUS-X   → H_X,  +1
            //     REQUEST-MOTION-MINUS-Z  → H_Z,  -1
            //     REQUEST-MOTION-PLUS-DY  → H_DY, +1
            //     REQUEST-MOTION-MINUS-PY → H_PY, -1
            //     REQUEST-MOTION-PLUS-WY  → H_WY, +1
            string axisName = msgName switch
            {
                _ when msgName.EndsWith("-X")  => MotionExtensions.H_X,
                _ when msgName.EndsWith("-DY") => MotionExtensions.D_Y,
                _ when msgName.EndsWith("-PY") => MotionExtensions.P_Y,
                _ when msgName.EndsWith("-WY") => MotionExtensions.W_Y,
                _ when msgName.EndsWith("-Z")  => MotionExtensions.H_Z,
                _ => throw new ArgumentException($"알 수 없는 축: {msgName}")
            };
           
            int sign = msgName.Contains("-PLUS-") ? 1 : -1;
            string direction = sign > 0 ? "PLUS" : "MINUS";

            // Data에는 이동 거리(double) 값만 전달됨
            double distance = double.TryParse(msg.Data?.Content, out var d) ? d : 0;
            double signedDistance = distance * sign;

            bool result = await sequenceHelper.RelativeMoveAsync(axisName, 0, signedDistance, ct);

            var responseContent = new MotionMoveResult
            {
                Axis = axisName,
                Direction = direction,
                Distance = distance,
                Result = result
            };

            var response = MessageFactory.Create(
                messageName: reply,
                unitName: "EQP",
                content: responseContent.ToXml()
            );
            await _server.SendAsync(response);
        }
        #endregion

        public void Dispose() => _server.Dispose();
    }
}