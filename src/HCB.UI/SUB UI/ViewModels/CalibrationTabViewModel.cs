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
                await _sequenceService.MotionsMove(MotionExtensions.H_Z, 0, ct);

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

                    await _sequenceService.MotionsMove(MotionExtensions.H_Z, 90.0, 100, ct);
                    await _sequenceService.MotionsMove(MotionExtensions.H_Z, 0, 100, ct);

                    CalibStatus = $"WarmUp #{WarmUpCycle} — Max 이동";
                    await Task.WhenAll(Array.ConvertAll(axes,
                        a => _sequenceService.MotionsMove(a.Name, a.Axis.LimitMaxPosition - 5, ct)));

                    ct.ThrowIfCancellationRequested();

                    CalibStatus = $"WarmUp #{WarmUpCycle} — Min 이동";
                    await Task.WhenAll(Array.ConvertAll(axes,
                        a => _sequenceService.MotionsMove(a.Name, 0, ct)));
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

                // 실제 측정 위치와 동일하게 h_z/H_Z를 FID_ALIGN_GAP만큼 이동 (align/fid 측정 갭)
                double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);

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