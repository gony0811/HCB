using HCB.Data.Entity;
using HCB.IoC;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
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
                    case "REQUEST-EQP-STATUS":
                        await ReplyEqpStatus();
                        break;
                    case "COMMAND-MOTION-MOVE":
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

        private async Task HandleMotionMove(Message msg, CancellationToken ct = default)
        {
            var xml = XElement.Parse($"<DATA>{msg.Data?.Content}</DATA>");
            var args = new MotionMoveCommand
            {
                Axis = xml.Element("AXIS")?.Value ?? "",
                Direction = xml.Element("DIRECTION")?.Value ?? "",
                Distance = double.TryParse(xml.Element("DISTANCE")?.Value, out var d) ? d : 0,
            };

            int direction = args.Direction.Equals("MINUS") ? -1 : 1;
            double distance = args.Distance * direction;

            bool result = await sequenceHelper.RelativeMoveAsync(args.Axis, 0, distance, ct);

            // ✅ Fix 4: args 값 재사용 (XML 중복 파싱 제거)
            var responseContent = new MotionMoveResult
            {
                Axis = args.Axis,
                Direction = args.Direction,
                Distance = args.Distance,
                Result = result
            };

            var response = MessageFactory.Create(
                messageName: "COMPLETED-MOTION-MOVE",
                unitName: "EQP",
                content: responseContent.ToXml()
            );
            await _server.SendAsync(response);
        }
        #endregion

        public void Dispose() => _server.Dispose();
    }
}