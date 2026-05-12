using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class CalibrationTabViewModel : ObservableObject
    {
        private readonly EqpCommunicationService _communication;
        private readonly ECParamService _ecParamService;
        private readonly SequenceService _sequenceService;
        private readonly ILogger _logger;

        // 스테이지 축
        private IAxis? _hxAxis;   // H_X (Bond Head X)
        private IAxis? _wyAxis;   // W_Y (Wafer Y)
        private IAxis? _pyAxis;   // P_Y (Pc table Y)
        private IAxis? _htAxis;   // H_T (Bond Head Theta — HcRO 회전축)

        // 파라미터
        [ObservableProperty] private double aMove = 0.1;
        [ObservableProperty] private double rotationDeg = 1.5;   // H_T 회전량 (HcRO 계산용)

        // UI 상태
        [ObservableProperty] private bool isNotBusy = true;
        [ObservableProperty] private string calibStatus = "-";

        // 각도 캘리브레이션 결과
        [ObservableProperty] private double theta1Rad;
        [ObservableProperty] private double theta1Deg;
        [ObservableProperty] private double theta2Rad;
        [ObservableProperty] private double theta2Deg;
        [ObservableProperty] private double thetaPRad;
        [ObservableProperty] private double thetaPDeg;

        // HcRO 회전 중심 결과  [HX(C), WY(C)]
        [ObservableProperty] private double hcROX;
        [ObservableProperty] private double hcROY;

        public CalibrationTabViewModel(
            DeviceManager deviceManager,
            EqpCommunicationService communication,
            SequenceService sequenceService,
            ECParamService ecParamService,
            ILogger logger)
        {
            _communication = communication;
            _sequenceService = sequenceService;
            _ecParamService = ecParamService;
            _logger = logger.ForContext<CalibrationTabViewModel>();
            var device = deviceManager.GetDevice<PowerPmacDevice>("PMAC");
            _hxAxis = device.FindMotionByName(MotionExtensions.H_X);
            _wyAxis = device.FindMotionByName(MotionExtensions.W_Y);
            _pyAxis = device.FindMotionByName(MotionExtensions.P_Y);
            _htAxis = device.FindMotionByName(MotionExtensions.H_T);
            
        }

        [RelayCommand]
        public async Task Hc1Angle(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                CalibStatus = "Hc1 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "OFFSET_STANBY", ct);

                double theta = await GetAngle(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, ct);
                Theta1Rad = theta;
                Theta1Deg = theta * (180.0 / Math.PI);
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HC1_T);
                dto.Value = Theta1Deg.ToString();
                dto.ValueType = ValueType.Double;
                if(dto.Id == 0) { await _ecParamService.AddParam(dto); }
                else { await _ecParamService.UpdateParam(dto); }

                CalibStatus = $"Hc1 완료  Θ1 = {Theta1Deg:F4} °";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e) { _logger.Error(e, "Hc1 Angle calibration failed"); CalibStatus = $"오류: {e.Message}"; }
            finally { IsNotBusy = true; }
        }

        [RelayCommand]
        public async Task Hc2Angle(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                CalibStatus = "Hc2 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "OFFSET_STANBY", ct);
                double theta = await GetAngle(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, ct);
                Theta2Rad = theta;
                Theta2Deg = theta * (180.0 / Math.PI);
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HC2_T);
                dto.Value = Theta2Deg.ToString();
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) { await _ecParamService.AddParam(dto); }
                else { await _ecParamService.UpdateParam(dto); }

                CalibStatus = $"Hc2 완료  Θ2 = {Theta2Deg:F4} °";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e) { _logger.Error(e, "Hc2 Angle calibration failed"); CalibStatus = $"오류: {e.Message}"; }
            finally { IsNotBusy = true; }
        }

        [RelayCommand]
        public async Task PcAngle(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                CalibStatus = "Pc 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "OFFSET_STANBY", ct);
                double theta = await GetAnglePc(CameraType.PC_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, ct);
                ThetaPRad = theta;
                ThetaPDeg = theta * (180.0 / Math.PI);
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.PC_T);
                dto.Value = ThetaPDeg.ToString();
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) { await _ecParamService.AddParam(dto); }
                else { await _ecParamService.UpdateParam(dto); }
                CalibStatus = $"Pc 완료  Θp = {ThetaPDeg:F4} °";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; throw; }
            catch (Exception e) { _logger.Error(e, "Pc Angle calibration failed"); CalibStatus = $"오류: {e.Message}";}
            finally { IsNotBusy = true; }
        }

        [RelayCommand]
        public async Task CreateHcRo(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                
                var hc2XParam = _ecParamService.FindByName(MotionExtensions.HC2_X).Value;
                var hc2YParam = _ecParamService.FindByName(MotionExtensions.HC2_Y).Value;
                var hc2XOffset = double.TryParse(hc2XParam, out double xOffset) ? xOffset : 0.0;
                var hc2YOffset = double.TryParse(hc2YParam, out double yOffset) ? yOffset : 0.0;

                // ── 회전 전: Hc1, Hc2 각각 절대 좌표 계산 (Stage 이동 없음) ───
                CalibStatus = "Hc1/Hc2 마크 측정 (회전 전)...";
                await _communication.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
                var v1Before = await _communication.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                if (v1Before.Result == Result.NG) throw new Exception("Hc1 회전 전 비전 측정 실패");

                await _communication.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
                
                var v2Before = await _communication.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                if (v2Before.Result == Result.NG) throw new Exception("Hc2 회전 전 비전 측정 실패");


                // ── H_T 회전 ─────────────────────────────────────────
                CalibStatus = "H_T 회전 중...";
                await _sequenceService.MotionsMove(MotionExtensions.H_T, _htAxis!.CurrentPosition + RotationDeg, ct);

                // ── 회전 후: Hc1, Hc2 각각 절대 좌표 계산 (Stage 이동 없음) ───
                CalibStatus = "Hc1/Hc2 마크 측정 (회전 후)...";
                await _communication.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
                var v1After = await _communication.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                if (v1After.Result == Result.NG) throw new Exception("Hc1 회전 후 비전 측정 실패");

                await _communication.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
                var v2After = await _communication.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                if (v2After.Result == Result.NG) throw new Exception("Hc2 회전 후 비전 측정 실패");

                
                var hc1Before = Point2D.of(v1Before.X, v1Before.Y);
                var hc2Before = Point2D.of(v2Before.X + hc2XOffset, v2Before.Y + hc2YOffset);
                var hc1After = Point2D.of(v1After.X, v1After.Y);
                var hc2After = Point2D.of(v2After.X + hc2XOffset, v2After.Y + hc2YOffset);
                
                // ── H_T 복귀 ─────────────────────────────────────────
                CalibStatus = "H_T 복귀...";
                await _sequenceService.MotionsMove(MotionExtensions.H_T, _htAxis.CurrentPosition - RotationDeg, ct);

                // ── 회전 중심 계산 ────────────────────────────────────
                var hcRO = CalibrationMath.ComputeHcRO(hc1Before, hc1After, hc2Before, hc2After);
                HcROX = hcRO.X;
                HcROY = hcRO.Y;

                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HCRO_X);
                dto.Value = HcROX.ToString();
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) { await _ecParamService.AddParam(dto); }
                else { await _ecParamService.UpdateParam(dto); }

                ECParamDto dto2 = _ecParamService.FindByName(MotionExtensions.HCRO_Y);
                dto2.Value = HcROY.ToString();
                dto2.ValueType = ValueType.Double;
                if (dto2.Id == 0) { await _ecParamService.AddParam(dto2); }
                else { await _ecParamService.UpdateParam(dto2); }

                CalibStatus = $"HcRO 완료  HX(C) = {HcROX:F4}  WY(C) = {HcROY:F4}";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e) { _logger.Error(e, "CreateHcRo failed"); CalibStatus = $"오류: {e.Message}"; }
            finally { IsNotBusy = true; }
        }


        [RelayCommand]
        public async Task CameraDistance(CancellationToken ct = default)
        {
            const double SafeGap = 0.1;
            const double MeasureOffsetX = -12.5;
            const double MeasureOffsetY = 7.0;

            try
            {
                CalibStatus = $"카메라 거리측정 시작";
                double shankToWaferOffset = await _sequenceService.GetRecipe("ShankToWaferOffset");
                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");

                await _sequenceService.Init_Head(ct);

                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.WAFER_CENTER_POSITION, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, MotionExtensions.WAFER_CENTER_POSITION, ct));

                double zTarget = shankToWaferOffset - topDieThickness - btmDieThickness - SafeGap;
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, zTarget, ct);

                var firstMark = await _sequenceService.VisionResult(
                    CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, MotionExtensions.W_Y, ct);

                await Task.WhenAll(
                    _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, MeasureOffsetX, ct),
                    _sequenceService.RelativeMotionsMove(MotionExtensions.W_Y, MeasureOffsetY, ct));

                var secondMark = await _sequenceService.VisionResult(
                    CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, MotionExtensions.W_Y, ct);

                // ── HcRO 중심 기준 카메라 오프셋 산출 ──
                // 두 측정점의 중점 = HcRO → 카메라 간 오프셋
                double halfDeltaX = (secondMark.CenterX - firstMark.CenterX) / 2.0;
                double halfDeltaY = (secondMark.CenterY - firstMark.CenterY) / 2.0;

                // HC1(물리: 좌하단) → Stage 좌표: X-, Y+ (카메라 Y축 반전)
                // HC2(물리: 우상단) → Stage 좌표: X+, Y- (HcRO 점대칭)
                await UpdateCameraOffsets(
                    hc1X: halfDeltaX,   // -값 (좌측)
                    hc1Y: halfDeltaY,   // +값 (Y반전 → 하단이 +)
                    hc2X: -halfDeltaX,   // +값 (우측)
                    hc2Y: -halfDeltaY);  // -값 (Y반전 → 상단이 -)
                CalibStatus = $"카메라 거리측정 완료";
            }
            catch (OperationCanceledException) {  }
            catch (Exception e)
            {
                _logger.Error(e, "카메라 거리 측정 Fail");
            }
        }

        private async Task UpdateCameraOffsets(
            double hc1X, double hc1Y, double hc2X, double hc2Y)
        {
            var updates = new (string Name, double Value)[]
            {
            ("HC1_X", hc1X),
            ("HC1_Y", hc1Y),
            ("HC2_X", hc2X),
            ("HC2_Y", hc2Y),
            };

            foreach (var (name, value) in updates)
            {
                var param = _ecParamService.FindByName(name);
                param.Value = value.ToString();
                await _ecParamService.UpdateParam(param);
            }
        }
        private async Task<double> GetAngle(CameraType cameraType, MarkType markType, DirectType directType, CancellationToken ct = default)
        {
            try
            {
                // 1. 마크 탐색 및 초기 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var beforeVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                if (beforeVision == null) throw new Exception("beforeVision 응답 null");
                if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                // 2. beforeVision X, Y가 0이 될 때까지 센터 이동 반복
                const double Tolerance = 0.003;
                const int MaxRetry = 3;
                int retry = 0;
                bool centered = false;

                // 처음부터 오차 범위 내인 경우
                if (Math.Abs(beforeVision.X) <= Tolerance && Math.Abs(beforeVision.Y) <= Tolerance)
                {
                    centered = true;
                }

                while (!centered && retry < MaxRetry)
                {
                    await Task.WhenAll(
                        _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition - beforeVision.X, ct),
                        _sequenceService.MotionsMove(MotionExtensions.W_Y, _wyAxis!.CurrentPosition - beforeVision.Y, ct)
                    );

                    await _communication.RequestAFStart(cameraType, markType, ct);
                    beforeVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                    if (beforeVision == null) throw new Exception("beforeVision 응답 null");
                    if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                    retry++;

                    if (Math.Abs(beforeVision.X) <= Tolerance && Math.Abs(beforeVision.Y) <= Tolerance)
                    {
                        centered = true;
                        break;
                    }
                }

                if (!centered)
                    throw new Exception($"마크 센터 정렬 실패: {MaxRetry}회 초과");

                // 3. HX = WY = AMove 조건으로 대각선 이동
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition + AMove, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, _wyAxis!.CurrentPosition + AMove, ct)
                );

                // 4. 이동 후 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                if (afterVision == null) throw new Exception("afterVision 응답 null");
                if (afterVision.Result == Result.NG) throw new Exception("이동 후 비전 측정 실패");

                // 5. AMove 이동 후 비전 좌표를 직접 사용 (beforeVision ≈ 0이므로)
                double hcX = afterVision.X;
                double hcY = afterVision.Y;

                if (Math.Abs(AMove) < 1e-10)
                    throw new Exception("AMove 값이 0입니다");

                return CalibrationMath.ComputeCameraTheta(AMove, hcX, hcY);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "GetAngle failed");
                throw;
            }
        }
        private async Task<double> GetAnglePc(CameraType cameraType, MarkType markType, DirectType directType, CancellationToken ct = default)
        {
            try
            {
                // 1. 마크 탐색 및 초기 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var beforeVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                // 2. beforeVision X, Y가 0이 될 때까지 센터 이동 반복
                const double Tolerance = 0.003;
                const int MaxRetry = 3;
                int retry = 0;
                bool centered = false;

                // 처음부터 오차 범위 내인 경우
                if (Math.Abs(beforeVision.X) <= Tolerance && Math.Abs(beforeVision.Y) <= Tolerance)
                {
                    centered = true;
                }

                while (!centered && retry < MaxRetry)
                {
                    await Task.WhenAll(
                        _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition - beforeVision.X, ct),
                        _sequenceService.MotionsMove(MotionExtensions.P_Y, _pyAxis!.CurrentPosition + beforeVision.Y, ct)
                    );

                    await _communication.RequestAFStart(cameraType, markType, ct);
                    beforeVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                    if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                    retry++;

                    if (Math.Abs(beforeVision.X) <= Tolerance && Math.Abs(beforeVision.Y) <= Tolerance)
                    {
                        centered = true;
                        break;
                    }
                }

                if (!centered)
                    throw new Exception($"마크 센터 정렬 실패: {MaxRetry}회 초과");

                // 3. HX = PY = AMove 조건으로 대각선 이동
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition + AMove, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, _pyAxis!.CurrentPosition + AMove, ct)
                );

                // 4. 이동 후 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());

                // 5. 이동 전후 차이값으로 Theta 역산
                double pcX = afterVision.X - beforeVision.X;
                double pcY = afterVision.Y - beforeVision.Y;
                return CalibrationMath.ComputeCameraTheta(AMove, pcX, pcY);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "GetAnglePc failed");
                throw;
            }
        }
    }
}
