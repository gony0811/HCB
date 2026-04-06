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
        [ObservableProperty] private double aMove;
        [ObservableProperty] private double rotationDeg;   // H_T 회전량 (HcRO 계산용)

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
            EqpCommunicationService communication,
            SequenceService sequenceService,
            ECParamService ecParamService,
            ILogger logger)
        {
            _communication = communication;
            _sequenceService = sequenceService;
            _ecParamService = ecParamService;
            _logger = logger.ForContext<CalibrationTabViewModel>();
        }

        [RelayCommand]
        public async Task Hc1Angle(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                CalibStatus = "Hc1 캘리브레이션 중...";
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
            catch (OperationCanceledException) { CalibStatus = "취소됨"; throw; }
            catch (Exception e) { _logger.Error(e, "Hc1 Angle calibration failed"); CalibStatus = $"오류: {e.Message}"; throw; }
            finally { IsNotBusy = true; }
        }

        [RelayCommand]
        public async Task Hc2Angle(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                CalibStatus = "Hc2 캘리브레이션 중...";
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
            catch (OperationCanceledException) { CalibStatus = "취소됨"; throw; }
            catch (Exception e) { _logger.Error(e, "Hc2 Angle calibration failed"); CalibStatus = $"오류: {e.Message}"; throw; }
            finally { IsNotBusy = true; }
        }

        [RelayCommand]
        public async Task PcAngle(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                CalibStatus = "Pc 캘리브레이션 중...";
                double theta = await GetAnglePc(CameraType.PC_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, ct);
                ThetaPRad = theta;
                ThetaPDeg = theta * (180.0 / Math.PI);
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HC1_T);
                dto.Value = ThetaPDeg.ToString();
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) { await _ecParamService.AddParam(dto); }
                else { await _ecParamService.UpdateParam(dto); }
                CalibStatus = $"Pc 완료  Θp = {ThetaPDeg:F4} °";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; throw; }
            catch (Exception e) { _logger.Error(e, "Pc Angle calibration failed"); CalibStatus = $"오류: {e.Message}"; throw; }
            finally { IsNotBusy = true; }
        }

        [RelayCommand]
        public async Task CreateHcRo(CancellationToken ct = default)
        {
            IsNotBusy = false;
            try
            {
                // ── 회전 전: Hc1, Hc2 동시 센터링 ───────────────────
                CalibStatus = "Hc1/Hc2 마크 센터링 (회전 전)...";

                await _communication.RequestAFStart(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, ct);
                var v1Before = await _communication.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                await _communication.RequestAFStart(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, ct);
                var v2Before = await _communication.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                    

                // Hc1 기준으로 Stage 센터링 (HX, WY 공유축이므로 Hc1 오프셋만 보정)
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition - v1Before.X, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, _wyAxis!.CurrentPosition - v1Before.Y, ct));

                var hc1Before = Point2D.of(_hxAxis.CurrentPosition, _wyAxis.CurrentPosition);
                var hc2Before = Point2D.of(_hxAxis.CurrentPosition, _wyAxis.CurrentPosition);

                // ── H_T 회전 ─────────────────────────────────────────
                CalibStatus = "H_T 회전 중...";
                await _sequenceService.MotionsMove(MotionExtensions.H_T, _htAxis!.CurrentPosition + RotationDeg, ct);

                // ── 회전 후: Hc1, Hc2 동시 측정 ─────────────────────
                CalibStatus = "Hc1/Hc2 마크 측정 (회전 후)...";

                await _communication.RequestAFStart(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, ct);
                var v1After = await _communication.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                await _communication.RequestAFStart(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, ct);
                var v2After = await _communication.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());

                // 회전 후 절대 좌표 = 현재 Stage 위치 + 비전 오프셋
                var hc1After = Point2D.of(_hxAxis.CurrentPosition + v1After.X, _wyAxis.CurrentPosition + v1After.Y);
                var hc2After = Point2D.of(_hxAxis.CurrentPosition + v2After.X, _wyAxis.CurrentPosition + v2After.Y);

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
            catch (OperationCanceledException) { CalibStatus = "취소됨"; throw; }
            catch (Exception e) { _logger.Error(e, "CreateHcRo failed"); CalibStatus = $"오류: {e.Message}"; throw; }
            finally { IsNotBusy = true; }
        }

        private async Task<double> GetAngle(CameraType cameraType, MarkType markType, DirectType directType, CancellationToken ct = default)
        {
            try
            {
                // 1. 마크 탐색 및 초기 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var beforeVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());

                // 2. 마크를 카메라 센터로 이동
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition - beforeVision.X, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, _wyAxis!.CurrentPosition - beforeVision.Y, ct)
                );

                // 3. HX = WY = AMove 조건으로 대각선 이동
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition + AMove, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, _wyAxis!.CurrentPosition + AMove, ct)
                );

                // 4. 이동 후 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());

                // 5. 이동 전후 차이값으로 Theta 역산
                double hcX = afterVision.X - beforeVision.X;
                double hcY = afterVision.Y - beforeVision.Y;

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
                var beforeVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());

                // 2. 마크를 카메라 센터로 이동 (PY축 사용)
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition + beforeVision.X, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, _pyAxis!.CurrentPosition - beforeVision.Y, ct)
                );

                // 3. HX = PY = AMove 조건으로 대각선 이동
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition + AMove, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, _pyAxis!.CurrentPosition + AMove, ct)
                );

                // 4. 이동 후 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());

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
