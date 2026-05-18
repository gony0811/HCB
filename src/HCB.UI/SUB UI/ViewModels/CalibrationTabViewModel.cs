using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
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
        [ObservableProperty] private double aMove = -0.3;
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
                // 캘리브 전 HC1_T를 0으로 초기화
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HC1_T);
                dto.Value = "0";
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = "Hc1 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "HC1_T_OFFSET", ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "OFFSET_STANBY", ct);

                double theta = await GetAngle(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, ct);
                Theta1Rad = theta;
                Theta1Deg = theta * (180.0 / Math.PI);

                // 기대값 45°와의 차이를 저장
                double correction = 45.0 - Theta1Deg;
                dto = _ecParamService.FindByName(MotionExtensions.HC1_T);
                dto.Value = correction.ToString("F6");
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = $"Hc1 완료  Θ = {Theta1Deg:F4}°, 보정 = {correction:F4}°";
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
                // 캘리브 전 HC2_T를 0으로 초기화
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HC2_T);
                dto.Value = "0";
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = "Hc2 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "HC2_T_OFFSET", ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "OFFSET_STANBY", ct);

                double theta = await GetAngle(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, ct);
                Theta2Rad = theta;
                Theta2Deg = theta * (180.0 / Math.PI);

                // 기대값 45°와의 차이를 저장
                double correction = 45.0 - Theta2Deg;
                dto = _ecParamService.FindByName(MotionExtensions.HC2_T);
                dto.Value = correction.ToString("F6");
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = $"Hc2 완료  Θ = {Theta2Deg:F4}°, 보정 = {correction:F4}°";
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
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.P_Y], "P_LEFT_HIGH", ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "P_LEFT_FIDUCIAL_HIGH", ct);

                double theta = await GetAnglePc(CameraType.PC_HIGH, MarkType.FIDUCIAL, DirectType.LEFT, ct);
                ThetaPRad = theta;
                ThetaPDeg = theta * (180.0 / Math.PI);

                double correction = 45.0 - ThetaPDeg;
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.PC_T);
                dto.Value = correction.ToString("F6");
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = $"Pc 완료  Θ = {ThetaPDeg:F4}°, 보정 = {correction:F4}°";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; throw; }
            catch (Exception e) { _logger.Error(e, "Pc Angle calibration failed"); CalibStatus = $"오류: {e.Message}"; }
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

                // 측정 각도: -1.5° ~ +1.5° 범위에서 5포인트
                double[] angles = { -1.5, -0.75, 0, 0.75, 1.5 };
                var hc1Points = new List<Point2D>();
                var hc2Points = new List<Point2D>();

                for (int i = 0; i < angles.Length; i++)
                {
                    CalibStatus = $"H_T → {angles[i]:F2}° 측정 중... ({i + 1}/{angles.Length})";
                    await _sequenceService.MotionsMove(MotionExtensions.H_T, angles[i], ct);

                    await _communication.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
                    var v1 = await _communication.RequestVisionMarkPosition(
                        MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                    if (v1.Result == Result.NG) throw new Exception($"Hc1 {angles[i]}° 비전 측정 실패");

                    await _communication.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
                    var v2 = await _communication.RequestVisionMarkPosition(
                        MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                    if (v2.Result == Result.NG) throw new Exception($"Hc2 {angles[i]}° 비전 측정 실패");

                    hc1Points.Add(Point2D.of(-v1.X, -v1.Y));
                    hc2Points.Add(Point2D.of(hc2XOffset - v2.X, hc2YOffset - v2.Y));
                }

                // H_T 복귀
                CalibStatus = "H_T 복귀...";
                await _sequenceService.MotionsMove(MotionExtensions.H_T, 0, ct);

                // 최소자승 원 피팅으로 회전 중심 계산
                var allPoints = new List<Point2D>();
                allPoints.AddRange(hc1Points);
                allPoints.AddRange(hc2Points);

                var hcRO = CalibrationMath.FitCircleCenter(allPoints);
                HcROX = hcRO.X;
                HcROY = hcRO.Y;

                _logger.Information("HcRO FitCircle | Points={Count}, Center=({X:F4},{Y:F4})",
                    allPoints.Count, HcROX, HcROY);

                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HCRO_X);
                dto.Value = HcROX.ToString();
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                ECParamDto dto2 = _ecParamService.FindByName(MotionExtensions.HCRO_Y);
                dto2.Value = HcROY.ToString();
                dto2.ValueType = ValueType.Double;
                if (dto2.Id == 0) await _ecParamService.AddParam(dto2);
                else await _ecParamService.UpdateParam(dto2);

                CalibStatus = $"HcRO 완료  X = {HcROX:F4}  Y = {HcROY:F4}";
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
            const double Tolerance = 0.001;
            const int MaxRetry = 10;

            try
            {
                CalibStatus = "카메라 거리측정 시작";
                double shankToWaferOffset = await _sequenceService.GetRecipe("ShankToWaferOffset");
                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");

                await _sequenceService.Init_Head(ct);
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.WAFER_CENTER_POSITION, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, MotionExtensions.WAFER_CENTER_POSITION, ct));

                double zTarget = shankToWaferOffset - topDieThickness - btmDieThickness - SafeGap;
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, zTarget, ct);

                // ── Hc1 센터링: 마크를 카메라 중심에 맞춤 ──
                CalibStatus = "Hc1 센터링 중...";
                for (int i = 0; i < MaxRetry; i++)
                {
                    var v1 = await _sequenceService.VisionResult(
                        CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, MotionExtensions.W_Y, ct);

                    if (Math.Abs(v1.DxCamToMark) <= Tolerance && Math.Abs(v1.DyCamToMark) <= Tolerance)
                        break;

                    await Task.WhenAll(
                        _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, -v1.DxCamToMark, ct),
                        _sequenceService.RelativeMotionsMove(MotionExtensions.W_Y, -v1.DyCamToMark, ct));

                    if (i == MaxRetry - 1)
                        throw new Exception($"Hc1 센터링 실패: DxCam={v1.DxCamToMark:F4}, DyCam={v1.DyCamToMark:F4}");
                }

                // Hc1 센터링 완료 시점의 스테이지 좌표 기록
                double hc1StageX = _hxAxis!.CurrentPosition;
                double hc1StageY = _wyAxis!.CurrentPosition;

                // ── Hc2 위치로 대략 이동 ──
                await Task.WhenAll(
                    _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, MeasureOffsetX, ct),
                    _sequenceService.RelativeMotionsMove(MotionExtensions.W_Y, MeasureOffsetY, ct));

                // ── Hc2 센터링: 같은 마크를 Hc2 카메라 중심에 맞춤 ──
                CalibStatus = "Hc2 센터링 중...";
                for (int i = 0; i < MaxRetry; i++)
                {
                    var v2 = await _sequenceService.VisionResult(
                        CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, MotionExtensions.W_Y, ct);

                    if (Math.Abs(v2.DxCamToMark) <= Tolerance && Math.Abs(v2.DyCamToMark) <= Tolerance)
                        break;

                    await Task.WhenAll(
                        _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, -v2.DxCamToMark, ct),
                        _sequenceService.RelativeMotionsMove(MotionExtensions.W_Y, -v2.DyCamToMark, ct));

                    if (i == MaxRetry - 1)
                        throw new Exception($"Hc2 센터링 실패: DxCam={v2.DxCamToMark:F4}, DyCam={v2.DyCamToMark:F4}");
                }

                // Hc2 센터링 완료 시점의 스테이지 좌표 기록
                double hc2StageX = _hxAxis!.CurrentPosition;
                double hc2StageY = _wyAxis!.CurrentPosition;

                // ── 카메라 오프셋 = 스테이지 이동량 ──
                // 같은 마크를 각 카메라 중심에 놓았을 때의 스테이지 위치 차이
                double offsetX = hc1StageX - hc2StageX;
                double offsetY = hc1StageY - hc2StageY;

                await UpdateCameraOffsets(
                    hc1X: 0,
                    hc1Y: 0,
                    hc2X: offsetX,
                    hc2Y: offsetY);

                CalibStatus = $"카메라 거리측정 완료  ΔX={offsetX:F4}, ΔY={offsetY:F4}";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "카메라 거리 측정 Fail");
                CalibStatus = $"오류: {e.Message}";
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
                // 1. 현재 위치에서 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var beforeVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                if (beforeVision == null) throw new Exception("beforeVision 응답 null");
                if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                // 2. AMove만큼 대각선 이동
                if (Math.Abs(AMove) < 1e-10)
                    throw new Exception("AMove 값이 0입니다");

                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition + AMove, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, _wyAxis!.CurrentPosition + AMove, ct)
                );

                // 3. 이동 후 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                if (afterVision == null) throw new Exception("afterVision 응답 null");
                if (afterVision.Result == Result.NG) throw new Exception("이동 후 비전 측정 실패");

                // 4. 비전 좌표 변화량에서 기대 이동분을 빼서 순수 잔차 추출
                //    스테이지 +AMove → 비전에서 마크는 -AMove 방향으로 관측
                double deltaX = (afterVision.X - beforeVision.X) - (-AMove);
                double deltaY = (afterVision.Y - beforeVision.Y) - (-AMove);

                // ComputeCameraTheta는 원래 센터링 후 (beforeVision ≈ 0) 상태에서
                // afterVision의 잔차를 받도록 설계되었으므로 동일한 형태로 전달
                return CalibrationMath.ComputeCameraTheta(AMove, deltaX, deltaY);
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
                // 1. 현재 위치에서 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var beforeVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                if (beforeVision == null) throw new Exception("beforeVision 응답 null");
                if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                // 2. AMove만큼 대각선 이동
                if (Math.Abs(AMove) < 1e-10)
                    throw new Exception("AMove 값이 0입니다");

                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, _hxAxis!.CurrentPosition - AMove, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, _pyAxis!.CurrentPosition + AMove, ct)
                );

                // 3. 이동 후 비전 좌표 읽기
                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                if (afterVision == null) throw new Exception("afterVision 응답 null");
                if (afterVision.Result == Result.NG) throw new Exception("이동 후 비전 측정 실패");

                // 4. beforeVision 잔차를 빼서 순수 이동분만 추출
                double deltaX = (afterVision.X - beforeVision.X) - (-AMove);
                double deltaY = (afterVision.Y - beforeVision.Y) - (-AMove);

                return CalibrationMath.ComputeCameraTheta(AMove, deltaX, deltaY);
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
