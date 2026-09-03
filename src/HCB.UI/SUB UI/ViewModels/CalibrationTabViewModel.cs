using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.Data.Repository;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly RecipeService _recipeService;
        private readonly MotionPositionRepository _positionRepository;
        private readonly SequenceService _sequenceService;
        private readonly ILogger _logger;

        private IAxis? _hxAxis;
        private IAxis? _hzAxis;
        private IAxis? _wyAxis;
        private IAxis? _pyAxis;
        private IAxis? _htAxis;
        private IAxis? _dyAxis;

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

        // 카메라 거리(오프셋) 결과
        [ObservableProperty] private double cameraOffsetX;
        [ObservableProperty] private double cameraOffsetY;

        // HcRO 회전 중심 결과
        [ObservableProperty] private double hcROX;
        [ObservableProperty] private double hcROY;


        // 전체 캘리브레이션
        [ObservableProperty] private int calibRepeatCount = 1;

        // WarmUp
        [ObservableProperty] private int warmUpCycle = 0;

        public CalibrationTabViewModel(
            DeviceManager deviceManager,
            EqpCommunicationService communication,
            SequenceService sequenceService,
            ECParamService ecParamService,
            RecipeService recipeService,
            MotionPositionRepository positionRepository,
            ILogger logger)
        {
            _communication = communication;
            _sequenceService = sequenceService;
            _ecParamService = ecParamService;
            _recipeService = recipeService;
            _positionRepository = positionRepository;
            _logger = logger.ForContext<CalibrationTabViewModel>();
            var device = deviceManager.GetDevice<PowerPmacDevice>("PMAC");
            _hxAxis = device.FindMotionByName(MotionExtensions.H_X);
            _hzAxis = device.FindMotionByName(MotionExtensions.H_Z);
            _wyAxis = device.FindMotionByName(MotionExtensions.W_Y);
            _pyAxis = device.FindMotionByName(MotionExtensions.P_Y);
            _htAxis = device.FindMotionByName(MotionExtensions.H_T);
            _dyAxis = device.FindMotionByName(MotionExtensions.D_Y);

            _sequenceService.InterlockActivated += OnInterlockActivated;
        }

        private void OnInterlockActivated()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        private CancellationToken GetToken()
        {
            _cts?.Cancel();
            _cts?.Dispose();
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

                    // ── 에러 발생 시 현재 사이클을 처음부터 재시도 ──
                    int attempt = 0;
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();
                        attempt++;

                        if (attempt > 3)
                        {
                            CalibProgress = $"캘리브레이션 실패";
                            break;
                        }

                        string retryTag = attempt > 1 ? $"(재시도 {attempt}) " : "";

                        
                        try
                        {
                            CalibProgress = $"{prefix}{retryTag}HC1 각도 캘리브레이션";
                            await Hc1Angle(ct);
                            ct.ThrowIfCancellationRequested();

                            CalibProgress = $"{prefix}{retryTag}HC2 각도 캘리브레이션";
                            await Hc2Angle(ct);
                            ct.ThrowIfCancellationRequested();

                            CalibProgress = $"{prefix}{retryTag}카메라 거리 계산";
                            await CameraDistance(ct);
                            ct.ThrowIfCancellationRequested();

                            CalibProgress = $"{prefix}{retryTag}HcRO 회전 중심 계산";
                            await CreateHcRo(ct);
                            ct.ThrowIfCancellationRequested();

                            CalibProgress = $"{prefix}{retryTag}PC 각도 캘리브레이션";
                            await PcAngle(ct);
                            ct.ThrowIfCancellationRequested();

                            // ── 1사이클 완료 → CSV 저장 ──
                            await SaveCalibrationResult(i + 1, ct);

                            break; // 성공 → 다음 사이클
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception e)
                        {
                            _logger.Warning(e, "캘리브레이션 사이클 오류 — 처음부터 재시작 (Cycle {Cycle}, Attempt {Attempt})", i + 1, attempt);
                            CalibStatus = $"오류 발생, 처음부터 재시작: {e.Message}";
                            CalibProgress = $"{prefix}오류 — 처음부터 재시도";
                        }
                    }
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

        // ══════════════════════════════════════════════
        //  WarmUp — 중지할 때까지 각 축을 Min ↔ Max 범위로 왕복
        //           (Z축은 먼저 0(안전 높이)으로 올린 뒤 제외)
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task WarmUp()
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            WarmUpCycle = 0;
            var ct = GetToken();
            try
            {
                CalibStatus = "WarmUp — Z축 상승 중...";
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.HEAD_SAFETY, ct);

                var axes = new (string Name, IAxis Axis)[]
                {
                    (MotionExtensions.H_X, _hxAxis!),
                    (MotionExtensions.W_Y, _wyAxis!),
                    (MotionExtensions.P_Y, _pyAxis!),
                    (MotionExtensions.D_Y, _dyAxis!),
                };

                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    WarmUpCycle++;

                    CalibStatus = $"WarmUp #{WarmUpCycle} — Max 이동";
                    await Task.WhenAll(Array.ConvertAll(axes,
                        a => _sequenceService.MotionsMove(a.Name, a.Axis.LimitMaxPosition - 5,ct)));

                    ct.ThrowIfCancellationRequested();

                    CalibStatus = $"WarmUp #{WarmUpCycle} — Min 이동";
                    await Task.WhenAll(Array.ConvertAll(axes,
                        a => _sequenceService.MotionsMove(a.Name, 0, ct)));

                    await _sequenceService.MotionsMove(MotionExtensions.H_Z, 95, ct);
                    await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.HEAD_SAFETY, ct);

                    await _sequenceService.MotionsMove(MotionExtensions.h_z, 1.7, ct);
                    await _sequenceService.MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, ct);

                    await _sequenceService.MotionsMove(MotionExtensions.H_T, 1.5, 20, ct);
                    await _sequenceService.MotionsMove(MotionExtensions.H_T, -1.5, 20, ct);

                }
            }
            catch (OperationCanceledException)
            {
                CalibStatus = $"WarmUp 중지 ({WarmUpCycle}회 완료)";
            }
            catch (Exception e)
            {
                _logger.Error(e, "WarmUp failed");
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
                await _sequenceService.WTable2DMappingOn();
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "HC1_T_OFFSET", ct);

                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _ecParamService.GetDouble("ShankToWaferOffset");

                await _sequenceService.MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                // 실제 측정 위치와 동일하게 h_z/H_Z를 FID_ALIGN_GAP만큼 이동 (align/fid 측정 갭)
                double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);

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
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Hc1 Angle calibration failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally {
                await _sequenceService.MappingOff();
                if (standalone) IsNotBusy = true;
            }
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
                await _sequenceService.WTable2DMappingOn();
                await _sequenceService.Init_Head(ct);
                await _sequenceService.MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "HC2_T_OFFSET", ct);
                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _ecParamService.GetDouble("ShankToWaferOffset");
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                // 실제 측정 위치와 동일하게 h_z/H_Z를 FID_ALIGN_GAP만큼 이동 (align/fid 측정 갭)
                double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);

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
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Hc2 Angle calibration failed");
                CalibStatus = $"오류: {e.Message}";

            }
            finally {
                await _sequenceService.MappingOff();
                if (standalone) IsNotBusy = true;
            }
        }

        // ══════════════════════════════════════════════
        //  카메라 거리 측정
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task CameraDistance(CancellationToken ct = default)
        {
            const double MeasureOffsetX = -12.5;
            const double MeasureOffsetY = 7.0;
            const double Tolerance = 0.0003;
            const int MaxRetry = 10;
            bool standalone = IsNotBusy;
            if (standalone) { IsNotBusy = false; ct = GetToken(); }
            try
            {
                CalibStatus = "카메라 거리측정 시작";
                await _sequenceService.WTable2DMappingOn();
                await _sequenceService.Init_Head(ct);
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.WAFER_CENTER_POSITION, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, MotionExtensions.WAFER_CENTER_POSITION, ct));
                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _ecParamService.GetDouble("ShankToWaferOffset");

                await _sequenceService.MotionsMove(MotionExtensions.H_Z,
                    shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                // 실제 측정 위치와 동일하게 h_z/H_Z를 FID_ALIGN_GAP만큼 이동 (align/fid 측정 갭)
                double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);

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
                CameraOffsetX = offsetX;
                CameraOffsetY = offsetY;
                await UpdateCameraOffsets(hc1X: 0, hc1Y: 0, hc2X: offsetX, hc2Y: offsetY);

                
                // ── 피듀셜 기준값 저장 (트래킹 영점) ──
                CalibStatus = "피듀셜 기준값 측정 중...";
                await _sequenceService.Init_Head(ct);
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
            finally {
                await _sequenceService.MappingOff();
                if (standalone) IsNotBusy = true;
            }
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
                await _sequenceService.PTable2DMappingOn();
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
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Pc Angle calibration failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { 
                if (standalone) IsNotBusy = true;
                await _sequenceService.MappingOff();
            }
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
                await _sequenceService.Init_Head(ct);
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.WAFER_CENTER_POSITION, ct),
                    _sequenceService.MotionsMove(MotionExtensions.W_Y, MotionExtensions.WAFER_CENTER_POSITION, ct));

                double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
                double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _ecParamService.GetDouble("ShankToWaferOffset");
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                var hc2XParam = _ecParamService.FindByName(MotionExtensions.HC2_X).Value;
                var hc2YParam = _ecParamService.FindByName(MotionExtensions.HC2_Y).Value;
                var hc2XOffset = double.TryParse(hc2XParam, out double xOffset) ? xOffset : 0.0;
                var hc2YOffset = double.TryParse(hc2YParam, out double yOffset) ? yOffset : 0.0;

                double[] angles = { -0.75, 0, 0.75 };
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


        [RelayCommand]
        public async Task CreateHcroPc(CancellationToken ct = default)
        {
            bool standalone = IsNotBusy;
            if (standalone) { IsNotBusy = false; ct = GetToken(); }
            try
            {
                await _sequenceService.Init_Head(ct);

                // Left 위치로 이동 → 스테이지 좌표 기록
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct));
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);
                await _sequenceService.PTable2DMappingOn();

                double leftHX = _hxAxis!.CurrentPosition;
                double leftPY = _pyAxis!.CurrentPosition;

                // Right 위치로 이동 → 스테이지 좌표 기록 → Offset 계산
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct));

                double rightHX = _hxAxis!.CurrentPosition;
                double rightPY = _pyAxis!.CurrentPosition;

                double pcOffsetX = leftHX - rightHX;
                double pcOffsetY = rightPY - leftPY;

                double[] angles = { -1.5, 0, 1.5 };
                var leftPoints = new List<Point2D>();
                var rightPoints = new List<Point2D>();

                for (int i = 0; i < angles.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    CalibStatus = $"H_T → {angles[i]:F2}° 측정 중... ({i + 1}/{angles.Length})";
                    await _sequenceService.MotionsMove(MotionExtensions.H_T, angles[i], ct);

                    // Left 피듀셜 측정
                    await Task.WhenAll(
                        _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct),
                        _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct));
                    await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);

                    await _communication.RequestAFStart(CameraType.PC_HIGH, MarkType.FIDUCIAL, ct);
                    var vL = await _communication.RequestVisionMarkPosition(
                        MarkType.FIDUCIAL, CameraType.PC_HIGH, DirectType.LEFT.ToString());
                    if (vL.Result == Result.NG) throw new Exception($"PC Left {angles[i]}° 비전 측정 실패");
                    leftPoints.Add(Point2D.of(-vL.X, -vL.Y));

                    // Right 피듀셜 측정 (PC 카메라 1개 → 이동 후 측정)
                    await Task.WhenAll(
                        _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct),
                        _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct));
                    await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);

                    await _communication.RequestAFStart(CameraType.PC_HIGH, MarkType.FIDUCIAL, ct);
                    var vR = await _communication.RequestVisionMarkPosition(
                        MarkType.FIDUCIAL, CameraType.PC_HIGH, DirectType.RIGHT.ToString());
                    if (vR.Result == Result.NG) throw new Exception($"PC Right {angles[i]}° 비전 측정 실패");
                    rightPoints.Add(Point2D.of(pcOffsetX - vR.X, pcOffsetY - vR.Y));
                }

                CalibStatus = "H_T 복귀...";
                await _sequenceService.MotionsMove(MotionExtensions.H_T, 0, ct);

                var allPoints = new List<Point2D>();
                allPoints.AddRange(leftPoints);
                allPoints.AddRange(rightPoints);

                var hcRO = CalibrationMath.FitCircleCenter(allPoints);
                HcROX = hcRO.X;
                HcROY = hcRO.Y;

                _logger.Information("HcRO(PC) FitCircle | Points={Count}, Center=({X:F4},{Y:F4}), Offset=({OX:F4},{OY:F4})",
                    allPoints.Count, HcROX, HcROY, pcOffsetX, pcOffsetY);

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

                CalibStatus = $"HcRO(PC) 완료  X = {HcROX:F4}  Y = {HcROY:F4}";
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "CreateHcroPc failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally
            {
                await _sequenceService.MappingOff();
                if (standalone) IsNotBusy = true;
            }
        }


        // PC AF 측정 결과
        [ObservableProperty] private double rightFidHeight;
        [ObservableProperty] private double rightAlignHeight;
        [ObservableProperty] private double leftFidHeight;
        [ObservableProperty] private double leftAlignHeight;

        [RelayCommand]
        public async Task PcAF(CancellationToken ct = default)
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            ct = GetToken();
            try
            {

                //double fidAlignGap = await _sequenceService.GetRecipe(MotionExtensions.FID_ALIGN_GAP);
                await _sequenceService.Init_Head(ct);
                //await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, fidAlignGap, ct);

                // 1. Right Fiducial
                CalibStatus = "PC AF — Right Fiducial...";
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct));
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);
                await _communication.RequestAFStart(CameraType.PC_HIGH, MarkType.FIDUCIAL, ct);
                RightFidHeight = _hzAxis!.CurrentPosition;

                // 2. Right Align
                CalibStatus = "PC AF — Right Align...";
                double thickness = _recipeService.FindByParamDouble("TopDieThickness");
                var size = _recipeService.FindByParam("TOP_DIE_SIZE");
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_RIGHT_ALIGN_HIGH + size.Value, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_ALIGN_HIGH + size.Value, ct));

                await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, -thickness, ct);
                await _communication.RequestAFStart(CameraType.PC_HIGH, MarkType.ALIGN_MARK, ct);
                RightAlignHeight = _hzAxis!.CurrentPosition;

                // 3. Left Fiducial
                CalibStatus = "PC AF — Left Fiducial...";
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct));
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);
                await _communication.RequestAFStart(CameraType.PC_HIGH, MarkType.FIDUCIAL, ct);
                LeftFidHeight = _hzAxis!.CurrentPosition;

                // 4. Left Align
                CalibStatus = "PC AF — Left Align...";
                await Task.WhenAll(
                    _sequenceService.MotionsMove(MotionExtensions.H_X, MotionExtensions.P_LEFT_ALIGN_HIGH + size.Value, ct),
                    _sequenceService.MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_LEFT_ALIGN_HIGH + size.Value, ct));
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, RightAlignHeight, ct);
                await _communication.RequestAFStart(CameraType.PC_HIGH, MarkType.ALIGN_MARK, ct);
                LeftAlignHeight = _hzAxis!.CurrentPosition;

                CalibStatus = $"PC AF 완료 — RF:{RightFidHeight:F4} RA:{RightAlignHeight:F4} LF:{LeftFidHeight:F4} LA:{LeftAlignHeight:F4}";
                _logger.Information("PC AF 완료 — RightFid:{RF:F4} RightAlign:{RA:F4} LeftFid:{LF:F4} LeftAlign:{LA:F4}",
                    RightFidHeight, RightAlignHeight, LeftFidHeight, LeftAlignHeight);


                // Fid → H_Z PositionList에 저장
                await SavePositionHeight(MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, RightFidHeight);
                await SavePositionHeight(MotionExtensions.P_LEFT_FIDUCIAL_HIGH, LeftFidHeight);

                // Align → 레시피 파라미터에 저장
                await SaveRecipeParam("RightAlignHeight", RightAlignHeight);
                await SaveRecipeParam("LeftAlignHeight", LeftAlignHeight);

                CalibStatus = "PC AF 높이 저장 완료";
                _logger.Information("PC AF 높이 저장 — RF:{RF:F4} RA:{RA:F4} LF:{LF:F4} LA:{LA:F4} (Recipe: {Recipe})",
                RightFidHeight, RightAlignHeight, LeftFidHeight, LeftAlignHeight, _recipeService.UseRecipe?.Name);
                
            }
            catch (OperationCanceledException) { CalibStatus = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "PcAF failed");
                CalibStatus = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        [RelayCommand]
        public async Task CalculatePiezoPitch(CancellationToken ct= default)
        {
        }
        private async Task SavePositionHeight(string positionName, double height)
        {
            var pos = _hzAxis!.PositionList.FirstOrDefault(p => p.Name == positionName);
            if (pos == null)
                throw new DBException(DBErrorCode.NOT_FOUND, $"H_Z PositionList에 '{positionName}' 위치가 없습니다");

            pos.Position = height;
            await _positionRepository.Update(pos.ToEntity());
        }

        private async Task SaveRecipeParam(string paramName, double value)
        {
            var param = _recipeService.UseRecipe?.ParamList
                .FirstOrDefault(p => p.Name == paramName);

            if (param == null)
                throw new DBException(DBErrorCode.NOT_FOUND, $"사용하는 레시피에 {paramName} 이 없습니다.");

            param.Value = value.ToString("F6");
            await _recipeService.UpdateRecipeParam(param);
        }
        // CalibrationTabViewModel 에 추가

        #region ── 2D Mapping ──

        [ObservableProperty] private CameraType mappingCamera = CameraType.HC1_HIGH;
        [ObservableProperty] private MarkType mappingMark = MarkType.ALIGN_MARK;
        [ObservableProperty] private DirectType mappingDirect = DirectType.LEFT;
        [ObservableProperty] private double mappingStepMm = 2.0;
        [ObservableProperty] private int mappingGridSize = 8;
        [ObservableProperty] private string mappingProgress = "-";

        // 열려있는 2D Mapping 창(중복 오픈 방지 — 이미 열려있으면 앞으로 가져오기)
        private Mapping2DWindow? _mappingWindow;

        /// <summary>2D Mapping 전용 창을 연다(Grid / Wafer 타입 선택). 이 VM을 그대로 공유한다.</summary>
        [RelayCommand]
        public void OpenMappingWindow()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (_mappingWindow is not null && _mappingWindow.IsOpen)
                {
                    _mappingWindow.BringToFront();
                    return;
                }

                _mappingWindow = new Mapping2DWindow(this)
                {
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };
                _mappingWindow.Show();
            });
        }

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

        // ══════════════════════════════════════════════
        //  2D Mapping — Wafer 타입 (웨이퍼 맵 기반 측정 포인트)
        //   행별 개수 배열(도면 사양 그대로)로 원판 형상을 정의하고 사각형 그리드(셀)를 배열한다.
        //     · 행별 개수  : 각 행에 놓이는 셀 수(위→아래). 각 행은 수평 중앙 정렬된다.
        //     · 그리드 사이즈 a : 셀 한 변 길이(mm)
        //     · 그리드 간격 x  : 셀 사이 Gap(mm) → 셀 피치 = a + x
        //     · 마크 피치 n   : 셀 내부 마크 간 간격(mm)
        //   생성 시에는 그리드(셀)만 그리고 마크는 그리지 않는다(셀 클릭 시 마크 정보 표시).
        //   각 셀은 고유 ID를 가진다.
        // ══════════════════════════════════════════════
        #region ── 2D Mapping (Wafer) ──

        // 도면 사양(위→아래 행별 칩 개수). 총 121, notch 하단.
        private static readonly int[] DrawingRowCounts =
            { 3, 7, 9, 11, 11, 13, 13, 13, 11, 11, 9, 7, 3 };

        // 행별 개수 배열(쉼표 구분). 기본값 = 도면 사양 그대로.
        [ObservableProperty] private string waferRowCounts = string.Join(",", DrawingRowCounts);
        [ObservableProperty] private double waferCellSize = 1.4;  // 그리드 사이즈 a (mm)
        [ObservableProperty] private double waferCellGap = 1.1;   // 그리드 간격(Gap) x (mm)
        [ObservableProperty] private double waferMarkPitch = 0.001;// 마크 피치 n (mm)
        [ObservableProperty] private string waferMapStatus = "-";
        [ObservableProperty] private int waferCellCount;           // 생성된 셀 수
        [ObservableProperty] private int waferMarkCount;           // 생성된 측정 포인트(마크) 수

        /// <summary>생성된 셀(사각형) 목록 — 원판 중심(0,0) 기준 mm 좌표. 각 셀이 마크를 보유.</summary>
        public List<WaferMapCell> WaferCells { get; } = new();

        /// <summary>맵이 재생성되었을 때 발생(창이 다시 그리도록 알림).</summary>
        public event Action? WaferMapChanged;

        /// <summary>행별 개수 문자열("3,7,9,...")을 정수 목록으로 파싱. 양수만 취한다.</summary>
        private static List<int> ParseRowCounts(string text)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (var tok in text.Split(new[] { ',', ' ', '\t', '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(tok.Trim(), out int v) && v > 0) list.Add(v);
            return list;
        }

        /// <summary>도면 사양 행별 개수로 초기화한다(입력 리셋).</summary>
        [RelayCommand]
        private void LoadDrawingRowCounts() => WaferRowCounts = string.Join(",", DrawingRowCounts);

        /// <summary>행별 개수 배열·그리드 사양에 따라 웨이퍼 맵(셀)을 생성한다(마크는 셀 내부에 보관).</summary>
        [RelayCommand]
        private void GenerateWaferMap()
        {
            WaferCells.Clear();

            var counts = ParseRowCounts(WaferRowCounts);
            double a = WaferCellSize, gap = WaferCellGap, n = WaferMarkPitch;
            if (counts.Count == 0 || a <= 0)
            {
                WaferCellCount = WaferMarkCount = 0;
                WaferMapStatus = "행별 개수/그리드 사이즈 값을 확인하세요.";
                WaferMapChanged?.Invoke();
                return;
            }

            double pitch = a + gap;                 // 셀 간 피치(mm) — 실제 좌표 계산용
            double halfCell = a / 2.0;
            int rows = counts.Count;
            double halfRow = (rows - 1) / 2.0;
            int mHalf = n > 0 ? (int)Math.Floor(halfCell / n) : 0;   // 셀 중심 기준 마크 확장 수
            int markGrid = mHalf * 2 + 1;                            // 축당 마크 개수

            int id = 0;
            int markTotal = 0;
            for (int r = 0; r < rows; r++)
            {
                int k = counts[r];
                double halfCol = (k - 1) / 2.0;
                double gridY = -(r - halfRow);            // [그리기] 단위 격자 Y(행 0 = 상단)
                double ccy = gridY * pitch;               // 실제 중심 Y(mm)
                for (int c = 0; c < k; c++)
                {
                    double gridX = c - halfCol;           // [그리기] 단위 격자 X(수평 중앙 정렬)
                    double ccx = gridX * pitch;           // 실제 중심 X(mm)
                    var cell = new WaferMapCell(++id, r, c, ccx, ccy, a, gridX, gridY, markGrid);

                    // 셀 내부 마크(측정 포인트) — 실제 좌표는 피치 n, 그리기 인덱스는 격자.
                    for (int mi = -mHalf; mi <= mHalf; mi++)
                        for (int mj = -mHalf; mj <= mHalf; mj++)
                            cell.Marks.Add(new WaferMapMark(
                                ccx + mj * n, ccy + mi * n,
                                mj + mHalf, mi + mHalf));

                    markTotal += cell.Marks.Count;
                    WaferCells.Add(cell);
                }
            }

            WaferCellCount = WaferCells.Count;
            WaferMarkCount = markTotal;
            WaferMapStatus = $"{rows}행 · 셀 {WaferCellCount}개 · 셀당 마크 {markGrid * markGrid}점 (총 {markTotal})";
            _logger.Information("Wafer 맵 생성 — 행 {R}, a={A}mm gap={G}mm pitch={P}mm markPitch={N}mm → 셀 {C}, 마크 {M}",
                rows, a, gap, pitch, n, WaferCellCount, markTotal);

            WaferMapChanged?.Invoke();
        }

        #endregion

        // ══════════════════════════════════════════════
        //  2D Mapping — Wafer 센터·Theta 찾기/보정
        //   docs/wafer-center-theta-sequence.md(WaferSeq) 방식을 그대로 적용한다.
        //    · 센터 : WAFER_ALIGN_1/2/3(≈120°)으로 이동하며 엣지 3점 측정 → 원 피팅으로 중심 산출
        //             (WaferSeq.FindCenterStep1 미러링, CalibrationMath.FitCircleCenter)
        //    · Theta: 중심 행에서 대칭 쌍(좌 −m·우 +m)을 안쪽→바깥으로 확장, 검출되는
        //             "가장 바깥(최장 baseline)" 쌍의 기울기로 W_T를 1회 회전 보정
        //             (WaferSeq.CorrectThetaBySymmetricSweepAsync 미러링). 임계 이내까지 반복.
        //   ※ 좌표 부호 규칙은 RunMapping2D / VisionMarkResult.CenterX·Y 규약을 따른다.
        // ══════════════════════════════════════════════
        #region ── 2D Mapping (Wafer) 센터·Theta ──

        private const string WaferXAxis = MotionExtensions.H_X;      // 스테이지 X
        private const string WaferYAxis = MotionExtensions.W_Y;      // 웨이퍼 테이블 Y
        private const string WaferThetaAxis = MotionExtensions.W_T;  // 웨이퍼 테이블 θ
        // 좌표 부호(W-Table 기준, RunMapping2D 규약): 이동 +X/+Y, 비전 절대 = stage − Dx/Dy
        private const double WaferXMoveSign = 1.0;
        private const double WaferYMoveSign = 1.0;
        private const double WaferXVisionSign = -1.0;
        private const double WaferYVisionSign = -1.0;

        // 현재 Z가 고배율 위치인지 추적(저배↔고배 전환 순서 결정)
        private bool _zAtHighMag;

        // ── Z축 이동 규칙 (WaferSeq 미러) ──
        //  · 기본(저배/신규): h_z(SAFETY) 먼저 → H_Z(저배확인)
        //  · 고배 → 저배 전환: H_Z 먼저 → h_z
        /// <summary>저배율 측정 Z 위치로 이동.</summary>
        private async Task MoveZForLowMagAsync(CancellationToken ct)
        {
            if (_zAtHighMag)
            {
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "저배확인", ct);
                await _sequenceService.MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, ct);
            }
            else
            {
                await _sequenceService.MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, ct);
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, "저배확인", ct);
            }
            _zAtHighMag = false;
        }

        /// <summary>고배율 측정 Z 위치로 이동. h_z → H_Z 순(레시피/파라미터 기반 절대 이동).</summary>
        private async Task MoveZForHighMagAsync(CancellationToken ct)
        {
            double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
            await _sequenceService.MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, -fidAlignGap, ct);

            double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
            double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
            double shankToWaferOffset = _ecParamService.GetDouble("ShankToWaferOffset");
            await _sequenceService.MotionsMove(MotionExtensions.H_Z,
                shankToWaferOffset - topDieThickness - btmDieThickness + fidAlignGap - 0.1, ct);
            _zAtHighMag = true;
        }

        [ObservableProperty] private double waferFoundCenterX;      // 산출된 웨이퍼 중심 X(스테이지, mm)
        [ObservableProperty] private double waferFoundCenterY;      // 산출된 웨이퍼 중심 Y(스테이지, mm)
        [ObservableProperty] private double waferFoundThetaDeg;     // 산출된 웨이퍼 회전(°)
        [ObservableProperty] private string waferCenterStatus = "-";
        [ObservableProperty] private double waferThetaTolDeg = 0.01;// θ 보정 임계각(° 미만이면 종료)
        [ObservableProperty] private int waferCenterMaxIter = 3;    // 센터·θ 측정·보정 반복 상한
        [ObservableProperty] private int waferThetaStepCells = 1;   // 대칭 쌍 확장 단위(셀 수)
        [ObservableProperty] private double waferThetaSign = -1.0;  // θ 부호(WaferSeq ThetaSign; 반대면 +1)
        [ObservableProperty] private CameraType waferHighCamera = CameraType.HC1_HIGH; // θ 측정용 고배 카메라

        /// <summary>±90° 정규화(행 기울기).</summary>
        private static double NormalizeAngle(double deg)
        {
            if (deg > 90) deg -= 180;
            else if (deg < -90) deg += 180;
            return deg;
        }

        /// <summary>중심 행에서 GridX가 gx에 가장 근접한 셀(없으면 null).</summary>
        private static WaferMapCell? RowCellAt(List<WaferMapCell> row, double gx)
            => row.FirstOrDefault(c => Math.Abs(c.GridX - gx) < 0.25);

        /// <summary>원점 기준 셀 nominal 위치로 이동 후 지정 카메라로 마크를 1회 측정 → 절대(스테이지) 좌표(실패 시 null).</summary>
        private async Task<(double X, double Y)?> MeasureCellAbsAsync(
            WaferMapCell cell, double originX, double originY, CameraType camera, CancellationToken ct)
        {
            await Task.WhenAll(
                _sequenceService.MotionsMove(WaferXAxis, originX + WaferXMoveSign * cell.CenterX, ct),
                _sequenceService.MotionsMove(WaferYAxis, originY + WaferYMoveSign * cell.CenterY, ct));

            var v = await _communication.RequestVisionMarkPosition(
                MappingMark, camera, MappingDirect.ToString());
            if (v == null || v.Result == Result.NG) return null;

            double cx = await _sequenceService.GetCurrentPosition(WaferXAxis, ct);
            double cy = await _sequenceService.GetCurrentPosition(WaferYAxis, ct);
            return (cx + WaferXVisionSign * v.X, cy + WaferYVisionSign * v.Y);
        }

        // 저배 3점 측정 Position(WAFER_ALIGN_1/2/3)과 엣지 검출 시계 위치(11/4/7시) 매핑.
        //  ※ 11시는 비전 통신 시 코드 12로 전송된다(WaferClock.H11 = 12). (WaferSeq와 동일)
        private static readonly (string pos, WaferClock clock)[] EdgeStations =
        {
            (MotionExtensions.WAFER_ALIGN_1, WaferClock.H11),
            (MotionExtensions.WAFER_ALIGN_2, WaferClock.H04),
            (MotionExtensions.WAFER_ALIGN_3, WaferClock.H07),
        };

        /// <summary>현재 위치에서 웨이퍼 엣지 1점 측정 → 절대좌표(현재 스테이지 − 카메라→엣지 오프셋). 실패 시 null.</summary>
        private async Task<Point2D?> MeasureEdgeAbsAsync(WaferClock clock, CancellationToken ct)
        {
            var r = await _communication.RequestWaferEdge(clock, ct);
            if (r == null || r.Result == Result.NG) return null;

            double curHX = await _sequenceService.GetCurrentPosition(WaferXAxis, ct);
            double curWY = await _sequenceService.GetCurrentPosition(WaferYAxis, ct);
            return Point2D.of(curHX - r.X, curWY - r.Y);
        }

        /// <summary>
        /// 센터 찾기 핵심 — WAFER_ALIGN_1/2/3(≈120° 간격)으로 이동하며 엣지 3점을 측정하고,
        /// 최소자승 원 피팅(CalibrationMath.FitCircleCenter)으로 웨이퍼 중심을 산출한다.
        /// (WaferSeq.FindCenterStep1 미러링) 엣지 미검출 시 null.
        /// </summary>
        private async Task<Point2D?> FindEdgeCenterAsync(CancellationToken ct)
        {
            // 규칙1: 저배 측정 전 Z 이동(h_z → H_Z)
            WaferCenterStatus = "저배 Z 이동...";
            await MoveZForLowMagAsync(ct);

            var pts = new List<Point2D>();
            for (int i = 0; i < EdgeStations.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (pos, clock) = EdgeStations[i];

                WaferCenterStatus = $"{pos} 이동 후 엣지 측정... ({i + 1}/3)";
                await _sequenceService.MotionsMove(new[] { WaferXAxis, WaferYAxis }, pos, ct);

                var p = await MeasureEdgeAbsAsync(clock, ct);
                if (p == null)
                {
                    WaferCenterStatus = $"엣지 측정 실패(NG) — {pos}";
                    _logger.Warning("Wafer 센터 — 엣지 측정 NG ({Pos})", pos);
                    return null;
                }
                pts.Add(p);
                _logger.Information("Wafer 엣지 측정 ({Pos}) — abs=({X:F4},{Y:F4})", pos, p.X, p.Y);
            }

            return CalibrationMath.FitCircleCenter(pts);   // 일직선이면 예외 → 상위에서 안내
        }

        /// <summary>레시피 double 값을 안전 조회(미설정·오류 시 0).</summary>
        private async Task<double> GetRecipeSafe(string name)
        {
            try { return await _sequenceService.GetRecipe(name); }
            catch (Exception e)
            {
                _logger.Warning("레시피 {Name} 조회 실패 — 0 사용: {Msg}", name, e.Message);
                return 0;
            }
        }

        /// <summary>
        /// 저배(엣지) 센터 → 고배 센터로 전환한다(WaferSeq.FindCenterStep3 미러링).
        /// Z를 고배 측정 높이로 먼저 이동(MoveZForHighMagAsync)한 뒤,
        /// 고배 센터 = 저배 센터 + ShankLowOffset(EC) + HcCenterError(Recipe) 위치로 XY 이동.
        /// </summary>
        private async Task<(double X, double Y)> SwitchToHighMagAsync(Point2D lowCenter, CancellationToken ct)
        {
            // 규칙1: 고배 Z 먼저 이동(h_z → H_Z)
            await MoveZForHighMagAsync(ct);

            double shankLowX = _ecParamService.GetDouble("ShankLowOffsetX");
            double shankLowY = _ecParamService.GetDouble("ShankLowOffsetY");
            double hcErrX = await GetRecipeSafe("HcCenterErrorX");
            double hcErrY = await GetRecipeSafe("HcCenterErrorY");

            double hx = lowCenter.X + shankLowX + hcErrX;
            double wy = lowCenter.Y + shankLowY + hcErrY;
            await Task.WhenAll(
                _sequenceService.MotionsMove(WaferXAxis, hx, ct),
                _sequenceService.MotionsMove(WaferYAxis, wy, ct));
            return (hx, wy);
        }

        /// <summary>웨이퍼 센터 찾기 — 엣지 3점 원 피팅으로 중심을 산출하고 그 위치로 이동한다.</summary>
        [RelayCommand]
        public async Task FindWaferCenter()
        {
            if (!IsNotBusy) return;

            IsNotBusy = false;
            var ct = GetToken();
            try
            {
                var center = await FindEdgeCenterAsync(ct);
                if (center == null) return;   // 상태 메시지는 FindEdgeCenterAsync에서 설정

                WaferFoundCenterX = center.X;
                WaferFoundCenterY = center.Y;

                WaferCenterStatus = "산출된 센터로 이동 중...";
                await Task.WhenAll(
                    _sequenceService.MotionsMove(WaferXAxis, center.X, ct),
                    _sequenceService.MotionsMove(WaferYAxis, center.Y, ct));

                WaferCenterStatus = $"센터 찾기 완료 — 센터=({center.X:F4},{center.Y:F4})";
                _logger.Information("Wafer 센터 찾기 완료(엣지 3점) — ({X:F4},{Y:F4})", center.X, center.Y);
            }
            catch (OperationCanceledException) { WaferCenterStatus = "중지됨"; }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                _logger.Warning(e, "Wafer 센터 — 원 피팅 실패(점 일직선/부족)");
                WaferCenterStatus = "센터 실패 — 엣지 3점이 일직선/부족. WAFER_ALIGN_1/2/3 위치 확인.";
            }
            catch (Exception e)
            {
                _logger.Error(e, "FindWaferCenter failed");
                WaferCenterStatus = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        /// <summary>
        /// Wafer 전체 시퀀스:
        ///  1) Wafer Edge 3곳 측정 → 원 피팅으로 센터 찾기 (저배)
        ///  2) 센터로 이동 후 고배 전환 (저배 센터 + ShankLowOffset + HcCenterError)
        ///  3) 좌우로 이동하며 Theta 보정 (고배 마크, 대칭 쌍 최장 baseline, W_T 회전)
        /// </summary>
        [RelayCommand]
        public async Task RunWaferSequence()
        {
            if (!IsNotBusy) return;
            if (WaferCells.Count == 0) { WaferCenterStatus = "먼저 Wafer를 생성하세요."; return; }

            IsNotBusy = false;
            var ct = GetToken();
            try
            {
                // 그리드 센터 셀·중심 행(θ 대칭 쌍 측정용)
                var centerCell = WaferCells.OrderBy(c => c.GridX * c.GridX + c.GridY * c.GridY).First();
                var rowCells = WaferCells.Where(c => Math.Abs(c.GridY - centerCell.GridY) < 0.25)
                                         .OrderBy(c => c.GridX).ToList();
                int step = Math.Max(1, WaferThetaStepCells);

                // ── 1) 저배 엣지 3점 → 센터 ──
                WaferCenterStatus = "[1/3] Wafer Edge 3점 센터 찾기...";
                var lowCenter = await FindEdgeCenterAsync(ct);
                if (lowCenter == null) return;   // 상태 메시지는 FindEdgeCenterAsync에서 설정
                WaferFoundCenterX = lowCenter.X;
                WaferFoundCenterY = lowCenter.Y;

                // ── 2) 센터 이동 + 고배 전환 ──
                WaferCenterStatus = "[2/3] 센터 이동 후 고배 전환...";
                var (ox, oy) = await SwitchToHighMagAsync(lowCenter, ct);
                _logger.Information("Wafer 고배 전환 — 저배센터=({LX:F4},{LY:F4}) → 고배센터=({HX:F4},{HY:F4})",
                    lowCenter.X, lowCenter.Y, ox, oy);

                // ── 3) 좌우 이동 Theta 보정 (고배 마크, 대칭 쌍) ──
                await _communication.RequestAFStart(WaferHighCamera, MappingMark, ct);
                double thetaDeg = 0;
                for (int iter = 0; iter < Math.Max(1, WaferCenterMaxIter); iter++)
                {
                    ct.ThrowIfCancellationRequested();

                    // 회전 후 원점(고배 센터) 재고정: 센터 셀 마크 측정
                    var c0 = await MeasureCellAbsAsync(centerCell, ox, oy, WaferHighCamera, ct);
                    if (c0 == null) { WaferCenterStatus = $"[3/3] 고배 센터 셀 미검출 (ID {centerCell.Id})"; return; }
                    ox = c0.Value.X - WaferXMoveSign * centerCell.CenterX;
                    oy = c0.Value.Y - WaferYMoveSign * centerCell.CenterY;
                    WaferFoundCenterX = ox; WaferFoundCenterY = oy;

                    // 대칭 쌍을 안쪽→바깥 확장, 최장 baseline 1회 산출
                    (double X, double Y)? bestL = null, bestR = null;
                    double bestBase = 0;
                    for (int m = step; ; m += step)
                    {
                        ct.ThrowIfCancellationRequested();
                        var lc = RowCellAt(rowCells, centerCell.GridX - m);
                        var rc = RowCellAt(rowCells, centerCell.GridX + m);
                        if (lc == null || rc == null) break;              // 행 끝 도달

                        WaferCenterStatus = $"[3/3] 대칭 쌍 ±{m} 측정 (ID {lc.Id}/{rc.Id})...";
                        var l = await MeasureCellAbsAsync(lc, ox, oy, WaferHighCamera, ct);
                        var r = await MeasureCellAbsAsync(rc, ox, oy, WaferHighCamera, ct);
                        if (l == null || r == null) break;               // 미검출 → 지금까지 최장 쌍 사용

                        bestL = l; bestR = r;
                        bestBase = Math.Abs(rc.CenterX - lc.CenterX);
                    }

                    if (bestL == null || bestR == null)
                    {
                        WaferCenterStatus = $"완료 — 고배센터=({ox:F4},{oy:F4}) / 대칭 쌍 없음(θ 보정 생략)";
                        break;
                    }

                    thetaDeg = NormalizeAngle(
                        Math.Atan2(bestR.Value.Y - bestL.Value.Y, bestR.Value.X - bestL.Value.X).ToDegree());
                    WaferFoundThetaDeg = thetaDeg;
                    _logger.Information("Wafer θ [{Iter}] — 고배센터=({X:F4},{Y:F4}), θ={T:F4}° (baseline {B:F2}mm)",
                        iter + 1, ox, oy, thetaDeg, bestBase);

                    if (Math.Abs(thetaDeg) < WaferThetaTolDeg)
                    {
                        WaferCenterStatus = $"완료 — 고배센터=({ox:F4},{oy:F4}), Theta={thetaDeg:F4}° (baseline {bestBase:F2}mm, 임계 이내)";
                        break;
                    }

                    // θ 보정: W_T를 −corr 회전 (corr = ThetaSign·θ) — WaferSeq 규약
                    double corr = WaferThetaSign * thetaDeg;
                    WaferCenterStatus = $"[3/3] Theta {thetaDeg:F4}° → W_T {-corr:F4}° 회전 (baseline {bestBase:F2}mm)";
                    _logger.Information("Wafer θ 보정 — W_T {Rot:F4}° 회전(θ={T:F4}°)", -corr, thetaDeg);
                    await _sequenceService.RelativeMotionsMove(WaferThetaAxis, -corr, ct);
                }

                // 고배 센터 복귀
                await Task.WhenAll(
                    _sequenceService.MotionsMove(WaferXAxis, ox, ct),
                    _sequenceService.MotionsMove(WaferYAxis, oy, ct));
                _logger.Information("Wafer 전체 시퀀스 완료 — 고배센터=({X:F4},{Y:F4}), θ={T:F4}°",
                    ox, oy, WaferFoundThetaDeg);
            }
            catch (OperationCanceledException) { WaferCenterStatus = "중지됨"; }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                _logger.Warning(e, "Wafer 시퀀스 — 원 피팅 실패(점 일직선/부족)");
                WaferCenterStatus = "센터 실패 — 엣지 3점이 일직선/부족. WAFER_ALIGN_1/2/3 위치 확인.";
            }
            catch (Exception e)
            {
                _logger.Error(e, "RunWaferSequence failed");
                WaferCenterStatus = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        #endregion

    }

    /// <summary>
    /// 웨이퍼 맵 셀(사각형). 고유 ID와 마크를 보유.
    ///  · CenterX/Y, Size : 실제 mm 좌표/크기(측정·모션용 데이터)
    ///  · GridX/Y, MarkGrid : 그리기 전용 논리 좌표(단위 격자, mm 무관)
    /// </summary>
    public sealed class WaferMapCell
    {
        public int Id { get; }
        public int Row { get; }
        public int Col { get; }
        public double CenterX { get; }    // 실제 중심 X(mm)
        public double CenterY { get; }    // 실제 중심 Y(mm)
        public double Size { get; }       // 한 변 길이(mm)
        public double GridX { get; }      // [그리기] 단위 격자 X(원판 중심=0, 셀=1)
        public double GridY { get; }      // [그리기] 단위 격자 Y(위쪽 +)
        public int MarkGrid { get; }      // [그리기] 셀 내부 마크 축당 개수(정사각 격자)
        public List<WaferMapMark> Marks { get; } = new();

        public WaferMapCell(int id, int row, int col,
            double centerX, double centerY, double size,
            double gridX, double gridY, int markGrid)
        {
            Id = id; Row = row; Col = col;
            CenterX = centerX; CenterY = centerY; Size = size;
            GridX = gridX; GridY = gridY; MarkGrid = markGrid;
        }
    }

    /// <summary>
    /// 웨이퍼 맵 측정 포인트(마크).
    ///  · X/Y : 실제 mm 좌표(측정·모션용 데이터)
    ///  · MarkCol/Row : 그리기 전용 셀 내부 격자 인덱스(0-based, mm 무관)
    /// </summary>
    public readonly struct WaferMapMark
    {
        public double X { get; }          // 실제 X(mm)
        public double Y { get; }          // 실제 Y(mm)
        public int MarkCol { get; }       // [그리기] 셀 내부 열 인덱스
        public int MarkRow { get; }       // [그리기] 셀 내부 행 인덱스
        public WaferMapMark(double x, double y, int markCol, int markRow)
        {
            X = x; Y = y; MarkCol = markCol; MarkRow = markRow;
        }
    }
}