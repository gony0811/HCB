using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
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

        private IAxis? _hxAxis;
        private IAxis? _wyAxis;
        private IAxis? _pyAxis;
        private IAxis? _htAxis;

        private CancellationTokenSource? _cts;

        // 파라미터
        [ObservableProperty] private double aMove = -0.3;
        [ObservableProperty] private double rotationDeg = 1.5;

        // UI 상태
        [ObservableProperty] private bool isNotBusy = true;
        [ObservableProperty] private string calibStatus = "-";
        [ObservableProperty] private string calibProgress = "-";

        // 각도 캘리브레이션 결과
        [ObservableProperty] private double theta1Rad;
        [ObservableProperty] private double theta1Deg;
        [ObservableProperty] private double theta2Rad;
        [ObservableProperty] private double theta2Deg;
        [ObservableProperty] private double thetaPRad;
        [ObservableProperty] private double thetaPDeg;

        // HcRO 회전 중심 결과
        [ObservableProperty] private double hcROX;
        [ObservableProperty] private double hcROY;

        // 정밀도 검증
        [ObservableProperty] private CameraType selectedCamera = CameraType.HC1_HIGH;
        [ObservableProperty] private MarkType selectedMark = MarkType.ALIGN_MARK;
        [ObservableProperty] private DirectType selectedDirect = DirectType.LEFT;
        [ObservableProperty] private string verifyResult = "-";

        // 전체 캘리브레이션
        [ObservableProperty] private int calibRepeatCount = 1;

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

        private CancellationToken GetToken()
        {
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        // ══════════════════════════════════════════════
        //  중지
        // ══════════════════════════════════════════════

        [RelayCommand]
        public void StopCalibration()
        {
            _cts?.Cancel();
            CalibStatus = "중지 요청됨...";
        }

        // ══════════════════════════════════════════════
        //  전체 캘리브레이션
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task RunFullCalibration()
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            var ct = GetToken();

            try
            {
                for (int i = 0; i < CalibRepeatCount; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    string prefix = CalibRepeatCount > 1 ? $"[{i + 1}/{CalibRepeatCount}] " : "";

                    CalibProgress = $"{prefix}HC1 각도 캘리브레이션";
                    await Hc1Angle(ct);
                    ct.ThrowIfCancellationRequested();

                    CalibProgress = $"{prefix}HC2 각도 캘리브레이션";
                    await Hc2Angle(ct);
                    ct.ThrowIfCancellationRequested();

                    CalibProgress = $"{prefix}카메라 거리 계산";
                    await CameraDistance(ct);
                    ct.ThrowIfCancellationRequested();

                    CalibProgress = $"{prefix}PC 각도 캘리브레이션";
                    await PcAngle(ct);
                    ct.ThrowIfCancellationRequested();

                    CalibProgress = $"{prefix}HcRO 회전 중심 계산";
                    await CreateHcRo(ct);

                    // ── 1사이클 완료 → CSV 저장 ──
                    await SaveCalibrationResult(i + 1, ct);
                }

                CalibProgress = $"전체 캘리브레이션 완료 ({CalibRepeatCount}회)";
                CalibStatus = "전체 완료";
            }
            catch (OperationCanceledException)
            {
                CalibProgress = "중지됨";
                CalibStatus = "사용자 중지";
            }
            catch (Exception e)
            {
                _logger.Error(e, "RunFullCalibration failed");
                CalibProgress = $"오류: {e.Message}";
                CalibStatus = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        private async Task SaveCalibrationResult(int cycle, CancellationToken ct)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "캘리브레이션 데이터");
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder,
                    $"Calibration_{DateTime.Now:yyyyMMdd}.csv");

                bool exists = File.Exists(path);
                var line = string.Join(",",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    cycle,
                    Theta1Rad.ToString("F6"),
                    Theta1Deg.ToString("F4"),
                    Theta2Rad.ToString("F6"),
                    Theta2Deg.ToString("F4"),
                    ThetaPRad.ToString("F6"),
                    ThetaPDeg.ToString("F4"),
                    HcROX.ToString("F4"),
                    HcROY.ToString("F4"));

                if (!exists)
                {
                    string header = "Timestamp,Cycle,HC1_Rad,HC1_Deg,HC2_Rad,HC2_Deg,PC_Rad,PC_Deg,HcRO_X,HcRO_Y";
                    await File.WriteAllTextAsync(path, header + "\n" + line + "\n", ct);
                }
                else
                {
                    await File.AppendAllTextAsync(path, line + "\n", ct);
                }

                _logger.Information("캘리브레이션 결과 저장 (Cycle {Cycle}): {Path}", cycle, path);
            }
            catch (Exception e)
            {
                _logger.Warning(e, "캘리브레이션 CSV 저장 실패");
            }
        }

        // ══════════════════════════════════════════════
        //  HC1 각도 캘리브레이션
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task Hc1Angle(CancellationToken ct = default)
        {
            bool standalone = IsNotBusy;
            if (standalone) { IsNotBusy = false; ct = GetToken(); }
            try
            {
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HC1_T);
                dto.Value = "0";
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = "Hc1 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "HC1_T_OFFSET", ct);
                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
                double shankToWaferOffset = await _sequenceService.GetRecipe("ShankToWaferOffset");
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                double theta = await GetAngle(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, ct);
                Theta1Rad = theta;
                Theta1Deg = theta * (180.0 / Math.PI);

                double correction = -theta;
                dto = _ecParamService.FindByName(MotionExtensions.HC1_T);
                dto.Value = correction.ToString("F6");
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = $"Hc1 완료  Θ = {Theta1Deg:F4}°, 보정 = {correction:F6} Rad";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨";}
            catch (Exception e)
            {
                _logger.Error(e, "Hc1 Angle calibration failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { if (standalone) IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  HC2 각도 캘리브레이션
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task Hc2Angle(CancellationToken ct = default)
        {
            bool standalone = IsNotBusy;
            if (standalone) { IsNotBusy = false; ct = GetToken(); }
            try
            {
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.HC2_T);
                dto.Value = "0";
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = "Hc2 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "HC2_T_OFFSET", ct);
                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
                double shankToWaferOffset = await _sequenceService.GetRecipe("ShankToWaferOffset");
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                double theta = await GetAngle(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, ct);
                Theta2Rad = theta;
                Theta2Deg = theta * (180.0 / Math.PI);

                double correction = -theta;
                dto = _ecParamService.FindByName(MotionExtensions.HC2_T);
                dto.Value = correction.ToString("F6");
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = $"Hc2 완료  Θ = {Theta2Deg:F4}°, 보정 = {correction:F6} Rad";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨";}
            catch (Exception e)
            {
                _logger.Error(e, "Hc2 Angle calibration failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { if (standalone) IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  카메라 거리 측정
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task CameraDistance(CancellationToken ct = default)
        {
            const double MeasureOffsetX = -12.5;
            const double MeasureOffsetY = 7.0;
            const double Tolerance = 0.001;
            const int MaxRetry = 10;
            bool standalone = IsNotBusy;
            if (standalone) { IsNotBusy = false; ct = GetToken(); }
            try
            {
                CalibStatus = "카메라 거리측정 시작";
                await _sequenceService.Init_Head(ct);
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.WAFER_CENTER_POSITION, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, MotionExtensions.WAFER_CENTER_POSITION, ct));

                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
                double shankToWaferOffset = await _sequenceService.GetRecipe("ShankToWaferOffset");
                await _sequenceService.MotionsMove(MotionExtensions.H_Z,
                    shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                // Hc1 센터링
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
                double hc1StageX = _hxAxis!.CurrentPosition;
                double hc1StageY = _wyAxis!.CurrentPosition;

                // Hc2 위치로 이동
                await Task.WhenAll(
                    _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, MeasureOffsetX, ct),
                    _sequenceService.RelativeMotionsMove(MotionExtensions.W_Y, MeasureOffsetY, ct));

                // Hc2 센터링
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
                double hc2StageX = _hxAxis!.CurrentPosition;
                double hc2StageY = _wyAxis!.CurrentPosition;

                double offsetX = hc1StageX - hc2StageX;
                double offsetY = hc1StageY - hc2StageY;
                await UpdateCameraOffsets(hc1X: 0, hc1Y: 0, hc2X: offsetX, hc2Y: offsetY);

                // ── 피듀셜 기준값 저장 (트래킹 영점) ──
                CalibStatus = "피듀셜 기준값 측정 중...";
                await _communication.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
                var fid1 = await _communication.RequestVisionMarkPosition(
                    MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                if (fid1 == null || fid1.Result == Result.NG)
                    throw new Exception("Hc1 피듀셜 측정 실패");

                await _communication.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
                var fid2 = await _communication.RequestVisionMarkPosition(
                    MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                if (fid2 == null || fid2.Result == Result.NG)
                    throw new Exception("Hc2 피듀셜 측정 실패");

                await _ecParamService.SetOrUpdate("Hc1FidRefDx", fid1.X, "Hc1 피듀셜 기준 DxCam");
                await _ecParamService.SetOrUpdate("Hc1FidRefDy", fid1.Y, "Hc1 피듀셜 기준 DyCam");
                await _ecParamService.SetOrUpdate("Hc2FidRefDx", fid2.X, "Hc2 피듀셜 기준 DxCam");
                await _ecParamService.SetOrUpdate("Hc2FidRefDy", fid2.Y, "Hc2 피듀셜 기준 DyCam");

                _logger.Information(
                    "피듀셜 기준값 저장 — Hc1({Hc1Dx:F6}, {Hc1Dy:F6}), Hc2({Hc2Dx:F6}, {Hc2Dy:F6})",
                    fid1.X, fid1.Y, fid2.X, fid2.Y);

                CalibStatus = $"완료  ΔX={offsetX:F4}, ΔY={offsetY:F4} | 피듀셜 기준 저장됨";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "카메라 거리 측정 Fail");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { if (standalone) IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  Pc 각도 캘리브레이션
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task PcAngle(CancellationToken ct = default)
        {
            bool standalone = IsNotBusy;
            if (standalone) { IsNotBusy = false; ct = GetToken(); }
            try
            {
                ECParamDto dto = _ecParamService.FindByName(MotionExtensions.PC_T);
                dto.Value = "0";
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = "Pc 캘리브레이션 중...";
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.P_Y], "T축 보정", ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "P_LEFT_FIDUCIAL_HIGH", ct);

                double theta = await GetAnglePc(CameraType.PC_HIGH, MarkType.FIDUCIAL, DirectType.LEFT, ct);
                ThetaPRad = theta;
                ThetaPDeg = theta * (180.0 / Math.PI);

                double correction = -theta;
                dto = _ecParamService.FindByName(MotionExtensions.PC_T);
                dto.Value = correction.ToString("F6");
                dto.ValueType = ValueType.Double;
                if (dto.Id == 0) await _ecParamService.AddParam(dto);
                else await _ecParamService.UpdateParam(dto);

                CalibStatus = $"Pc 완료  Θ = {ThetaPDeg:F4}°, 보정 = {correction:F6} Rad";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨";}
            catch (Exception e)
            {
                _logger.Error(e, "Pc Angle calibration failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { if (standalone) IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  HcRO 회전 중심 계산
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task CreateHcRo(CancellationToken ct = default)
        {
            bool standalone = IsNotBusy;
            if (standalone) { IsNotBusy = false; ct = GetToken(); }
            try
            {
                var hc2XParam = _ecParamService.FindByName(MotionExtensions.HC2_X).Value;
                var hc2YParam = _ecParamService.FindByName(MotionExtensions.HC2_Y).Value;
                var hc2XOffset = double.TryParse(hc2XParam, out double xOffset) ? xOffset : 0.0;
                var hc2YOffset = double.TryParse(hc2YParam, out double yOffset) ? yOffset : 0.0;

                double[] angles = { -1.5, -0.75, 0, 0.75, 1.5 };
                var hc1Points = new List<Point2D>();
                var hc2Points = new List<Point2D>();

                for (int i = 0; i < angles.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
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

                CalibStatus = "H_T 복귀...";
                await _sequenceService.MotionsMove(MotionExtensions.H_T, 0, ct);

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
            catch (Exception e)
            {
                _logger.Error(e, "CreateHcRo failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { if (standalone) IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  정밀도 검증
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task VerifyAlignmentAccuracy(CancellationToken ct = default)
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            ct = GetToken();
            try
            {
                CalibStatus = "정밀도 검증 중...";

                string yAxisName = SelectedCamera == CameraType.PC_HIGH || SelectedCamera == CameraType.PC_LOW
                    ? MotionExtensions.P_Y
                    : MotionExtensions.W_Y;

                // 1. 비전 측정
                await _communication.RequestAFStart(SelectedCamera, SelectedMark, ct);
                var measured = await _communication.RequestVisionMarkPosition(
                    SelectedMark, SelectedCamera, SelectedDirect.ToString());

                if (measured == null) throw new Exception("비전 응답 null");
                if (measured.Result == Result.NG) throw new Exception("비전 측정 실패");

                double measuredX = measured.X;
                double measuredY = measured.Y;

                // 2. 카메라별 이동 부호 결정
                double moveX, moveY;
                if (SelectedCamera == CameraType.PC_HIGH || SelectedCamera == CameraType.PC_LOW)
                {
                    moveX = -measuredX;
                    moveY = +measuredY;
                }
                else
                {
                    moveX = -measuredX;
                    moveY = -measuredY;
                }

                await Task.WhenAll(
                    _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, moveX, ct),
                    _sequenceService.RelativeMotionsMove(yAxisName, moveY, ct)
                );

                // 3. 재측정
                await _communication.RequestAFStart(SelectedCamera, SelectedMark, ct);
                var verify = await _communication.RequestVisionMarkPosition(
                    SelectedMark, SelectedCamera, SelectedDirect.ToString());

                if (verify == null) throw new Exception("재측정 응답 null");
                if (verify.Result == Result.NG) throw new Exception("재측정 실패");

                double errorX = verify.X;
                double errorY = verify.Y;
                double errorDist = Math.Sqrt(errorX * errorX + errorY * errorY);

                VerifyResult = $"측정({measuredX * 1000:F1}, {measuredY * 1000:F1})μm → " +
                               $"잔차({errorX * 1000:F1}, {errorY * 1000:F1})μm  dist={errorDist * 1000:F1}μm";

                // 4. CSV 누적 저장
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "정밀도 데이터");
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, $"VerifyAccuracy_{SelectedCamera}.csv");
                bool exists = File.Exists(path);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{SelectedCamera}," +
                           $"{measuredX:F6},{measuredY:F6},{errorX:F6},{errorY:F6},{errorDist:F6}";

                if (!exists)
                    await File.WriteAllTextAsync(path,
                        "Timestamp,Camera,MeasuredX,MeasuredY,ErrorX,ErrorY,ErrorDist\n" + line + "\n", ct);
                else
                    await File.AppendAllTextAsync(path, line + "\n", ct);

                CalibStatus = "검증 완료";
                _logger.Information("검증 | 측정({MX:F4},{MY:F4}) → 잔차({EX:F4},{EY:F4}) dist={D:F4}mm",
                    measuredX, measuredY, errorX, errorY, errorDist);
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "VerifyAlignmentAccuracy failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }


        // CalibrationTabViewModel 에 추가

        #region ── 2D Mapping ──

        [ObservableProperty] private CameraType mappingCamera = CameraType.HC1_HIGH;
        [ObservableProperty] private MarkType mappingMark = MarkType.FIDUCIAL;
        [ObservableProperty] private DirectType mappingDirect = DirectType.LEFT;
        [ObservableProperty] private double mappingStepMm = 2.0;
        [ObservableProperty] private int mappingGridSize = 8;
        [ObservableProperty] private string mappingProgress = "-";

        [RelayCommand]
        public async Task RunMapping2D(CancellationToken ct = default)
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            ct = GetToken();
            try
            {
                int g = MappingGridSize;
                double step = MappingStepMm;
                bool isPc = MappingCamera is CameraType.PC_HIGH or CameraType.PC_LOW;
                string yAxis = isPc ? MotionExtensions.P_Y : MotionExtensions.W_Y;
                double ox = _hxAxis!.CurrentPosition;
                double oy = yAxis == MotionExtensions.P_Y
                    ? _pyAxis!.CurrentPosition : _wyAxis!.CurrentPosition;

                // 이동 부호: P-Table (-X, -Y), W-Table (+X, +Y)
                double xMoveSign = isPc ? -1.0 : 1.0;
                double yMoveSign = isPc ? -1.0 : 1.0;

                // 비전 결과 부호: P-Table (-X, +Y), W-Table (-X, -Y)
                double xVisionSign = -1.0;
                double yVisionSign = isPc ? 1.0 : -1.0;

                var dx = new double[g, g];
                var dy = new double[g, g];
                var stageX = new double[g, g];
                var stageY = new double[g, g];
                int total = g * g;
                CalibStatus = "2D Mapping 시작";
                await _communication.RequestAFStart(MappingCamera, MappingMark, ct);
                for (int row = 0; row < g; row++)
                {
                    for (int col = 0; col < g; col++)
                    {
                        ct.ThrowIfCancellationRequested();
                        int ac = (row % 2 == 0) ? col : (g - 1 - col);
                        MappingProgress = $"{row * g + col + 1}/{total}";
                        await Task.WhenAll(
                            _sequenceService.MotionsMove(MotionExtensions.H_X, ox + ac * step * xMoveSign, ct),
                            _sequenceService.MotionsMove(yAxis, oy + row * step * yMoveSign, ct));
                        
                        var v = await _communication.RequestVisionMarkPosition(
                            MappingMark, MappingCamera, MappingDirect.ToString());
                        if (v == null || v.Result == Result.NG)
                            throw new Exception($"비전 실패 @ R{row} C{ac}");
                        dx[row, ac] = v.X * xVisionSign * 1000.0;
                        dy[row, ac] = v.Y * yVisionSign * 1000.0;
                        stageX[row, ac] = _hxAxis!.CurrentPosition;
                        stageY[row, ac] = yAxis == MotionExtensions.P_Y
                            ? _pyAxis!.CurrentPosition : _wyAxis!.CurrentPosition;
                    }
                }
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, ox, ct),
                    _sequenceService.MotionsMove(yAxis, oy, ct));

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "2D Mapping");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder,
                    $"Mapping2D_{MappingCamera}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);

                // ── 비전 오차 (μm) ──
                sw.WriteLine("=== Vision Offset (μm) ===");
                sw.Write("μm,");
                for (int c = 0; c < g; c++)
                    sw.Write($"COL{c}{(c < g - 1 ? "," : "")}");
                sw.WriteLine();
                for (int r = 0; r < g; r++)
                {
                    sw.Write($"ROW{r},");
                    for (int c = 0; c < g; c++)
                        sw.Write($"\"({dx[r, c]:F1}  {dy[r, c]:F1})\"{(c < g - 1 ? "," : "")}");
                    sw.WriteLine();
                }

                // ── 스테이지 좌표 (mm) ──
                sw.WriteLine();
                sw.WriteLine("=== Stage Position (mm) ===");
                sw.Write("mm,");
                for (int c = 0; c < g; c++)
                    sw.Write($"COL{c}{(c < g - 1 ? "," : "")}");
                sw.WriteLine();
                for (int r = 0; r < g; r++)
                {
                    sw.Write($"ROW{r},");
                    for (int c = 0; c < g; c++)
                        sw.Write($"\"({stageX[r, c]:F4}  {stageY[r, c]:F4})\"{(c < g - 1 ? "," : "")}");
                    sw.WriteLine();
                }

                MappingProgress = $"완료 {total}pt";
                CalibStatus = $"완료 → {Path.GetFileName(path)}";
                _logger.Information("2D Mapping 완료: {Path}", path);
            }
            catch (OperationCanceledException)
            {
                MappingProgress = "중지됨";
                CalibStatus = "사용자 중지";
            }
            catch (Exception e)
            {
                _logger.Error(e, "RunMapping2D failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        #endregion
        // ══════════════════════════════════════════════
        //  Private 헬퍼
        // ══════════════════════════════════════════════

        private async Task UpdateCameraOffsets(double hc1X, double hc1Y, double hc2X, double hc2Y)
        {
            var updates = new (string Name, double Value)[]
            {
                ("HC1_X", hc1X), ("HC1_Y", hc1Y),
                ("HC2_X", hc2X), ("HC2_Y", hc2Y),
            };

            foreach (var (name, value) in updates)
            {
                var param = _ecParamService.FindByName(name);
                param.Value = value.ToString();
                await _ecParamService.UpdateParam(param);
            }
        }

        private async Task<double> GetAngle(CameraType cameraType, MarkType markType,
            DirectType directType, CancellationToken ct = default)
        {
            try
            {
                await _communication.RequestAFStart(cameraType, markType, ct);
                var beforeVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());
                if (beforeVision == null) throw new Exception("beforeVision 응답 null");
                if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                await _sequenceService.MotionsMove(MotionExtensions.H_X,
                    _hxAxis!.CurrentPosition + 0.55, ct);

                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());
                if (afterVision == null) throw new Exception("afterVision 응답 null");
                if (afterVision.Result == Result.NG) throw new Exception("이동 후 비전 측정 실패");

                double fullDx = afterVision.X - beforeVision.X;
                double fullDy = afterVision.Y - beforeVision.Y;

                double theta = Math.Atan2(-fullDy, -fullDx);
                if (theta > Math.PI / 2) theta -= Math.PI;
                else if (theta < -Math.PI / 2) theta += Math.PI;

                return theta;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Error(e, "GetAngle failed");
                throw;
            }
        }

        private async Task<double> GetAnglePc(CameraType cameraType, MarkType markType,
            DirectType directType, CancellationToken ct = default)
        {
            try
            {
                await _communication.RequestAFStart(cameraType, markType, ct);
                var beforeVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());
                if (beforeVision == null) throw new Exception("beforeVision 응답 null");
                if (beforeVision.Result == Result.NG) throw new Exception("비전 측정 실패");

                if (Math.Abs(AMove) < 1e-10)
                    throw new Exception("AMove 값이 0입니다");

                await _sequenceService.MotionsMove(MotionExtensions.H_X,
                    _hxAxis!.CurrentPosition + 0.55, ct);

                await _communication.RequestAFStart(cameraType, markType, ct);
                var afterVision = await _communication.RequestVisionMarkPosition(
                    markType, cameraType, directType.ToString());
                if (afterVision == null) throw new Exception("afterVision 응답 null");
                if (afterVision.Result == Result.NG) throw new Exception("이동 후 비전 측정 실패");

                double fullDx = afterVision.X - beforeVision.X;
                double fullDy = afterVision.Y - beforeVision.Y;

                double theta = Math.Atan2(fullDy, fullDx);
                if (theta > Math.PI / 2) theta -= Math.PI;
                else if (theta < -Math.PI / 2) theta += Math.PI;
                return theta;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Error(e, "GetAnglePc failed");
                throw;
            }
        }

    }
}