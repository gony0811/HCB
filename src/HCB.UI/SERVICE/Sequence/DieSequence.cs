using MediaFoundation;
using Microsoft.Extensions.Hosting;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Xml.Linq;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        public async Task  DTableLoading(CancellationToken ct)
        {
            try
            {
                _logger.Information("Die Loading Start");
                //EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동

                var DY = motionDevice?.FindMotionByName(MotionExtensions.D_Y);
                //var HX = motionDevice?.FindMotionByName(MotionExtensions.H_X);

                if (DY == null) throw new Exception("D Table Y axis not found in motion device.");
                //if (HX == null) throw new Exception("H Table X axis not found in motion device.");

                //Task moveHX = _sequenceHelper.MoveAsync(HX.MotorNo, MotionExtensions.DIE_LOADING, ct);
                Task moveDY = _sequenceHelper.MoveAsync(DY.MotorNo, MotionExtensions.LOAD_POSITION, ct);

                // 작업 동시에 수행
                await Task.WhenAll(moveDY);

                await Task.Delay(100, ct);

                // Vacuum Off
                await _sequenceHelper.DTableVacuumAll(eOnOff.Off, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Die Loading Canceled");
                throw new OperationCanceledException();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                throw new Exception(ex.Message);
            }
            finally
            {
                _logger.Information("Die Loading End");
            }
        }

        // Btm Die 저배율 측정
        public async Task<VisionMarkPositionResponse> BtmLowMeasure(int vacNum, MarkType markType, CancellationToken ct)
        {
            try
            {
                _logger.Information("Die 저배율 측정 시작");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                // 안전한 위치 셋업
                await Init_Head(ct);
                _logger.Information("Die Align 시작");

                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_X, $"DIE_BTM", ct),
                    MotionsMove(MotionExtensions.D_Y, $"DIE_ROW_{vacNum}", ct),
                    MotionsMove(MotionExtensions.W_T, 0, ct)
                );
                await MotionsMove(MotionExtensions.H_Z, MotionExtensions.DIE_VISION_LOW, ct);

                int retryMax = GetEcParamInt("LowVisionRetryMax", 3);
                for (int attempt = 0; attempt <= retryMax; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    var diePickupAlign = await communicationService.RequestVisionMarkPosition(markType, CameraType.HC_LOW, "");
                    if (diePickupAlign.Result != Result.NG)
                    {
                        _logger.Information("Die Align 종료");
                        return diePickupAlign;
                    }
                    if (attempt < retryMax)
                        _logger.Warning("저배율 비전 측정 실패 (BTM) — 재시도 {Attempt}/{Max}", attempt + 1, retryMax);
                }
                throw new VisionException(VisionErrorCode.MEASUREMENT_FAIL);
            }
            catch (ErrorException ex)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        // Top Die 저배율 측정
        public async Task<VisionMarkPositionResponse> TopLowMeasure(int vacNum, MarkType markType, CancellationToken ct)
        {
            try
            {
                _logger.Information("Die Align 요청 Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                // 안전한 위치 셋업
                await Init_Head(ct);
                _logger.Information("Die Align 시작");

                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_X, $"DIE_TOP", ct),
                    MotionsMove(MotionExtensions.D_Y, $"DIE_ROW_{vacNum}", ct),
                    MotionsMove(MotionExtensions.W_T, 0, ct)
                );

                await MotionsMove(MotionExtensions.H_Z, MotionExtensions.DIE_VISION_LOW, ct);

                int retryMax = GetEcParamInt("LowVisionRetryMax", 3);
                for (int attempt = 0; attempt <= retryMax; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    var diePickupAlign = await communicationService.RequestVisionMarkPosition(markType, CameraType.HC_LOW, "");
                    if (diePickupAlign.Result != Result.NG)
                    {
                        _logger.Information("Die Align 종료");
                        return diePickupAlign;
                    }
                    if (attempt < retryMax)
                        _logger.Warning("저배율 비전 측정 실패 (TOP) — 재시도 {Attempt}/{Max}", attempt + 1, retryMax);
                }
                throw new VisionException(VisionErrorCode.MEASUREMENT_FAIL);
            }
            catch (ErrorException ex)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }


        #region Top Die 고배율 측정

        public async Task<AlignData> TopHighAlign(
                AlignData data, CancellationToken ct)
        {
            data ??= new AlignData();
            var total = Stopwatch.StartNew();
            try
            {
                LoadCalibrationInto(data);
                var sw = Stopwatch.StartNew();;
                if (data.Use2DMapping) await PTable2DMappingOn();   // P-Table 2D Mapping On
                await Init_Head(ct);    // Head Z축 안전 위치로 이동
                // P-Table로 이동
                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct),
                    MotionsMove(MotionExtensions.H_X, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct),
                    MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct)
                );

                // 1. Hc 피듀셜 측정
                if (data.UseFiducialTracking)
                    await MeasureFiducialDrift(data, ct);
                    ProcessMeasurement(data, 2);

                // TopDie 사이즈 검색
                var size = _recipeService.FindByParam("TOP DIE SIZE");

                double PC_L_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.PC_L_HZ_TILT_X);
                double PC_L_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.PC_L_HZ_TILT_Y);
                double PC_R_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.PC_R_HZ_TILT_X);
                double PC_R_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.PC_R_HZ_TILT_Y);
                // ── Right: Fid → Align 측정 (Fid/Align은 서로 다른 H_Z 높이에서 촬상) ──
                data.TopRightFidRaw = await TopDieVisionRightFid(data.AvgMove, ct);
                double rightFidZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);   // Fid 촬상 Z
                _logger.Information("TopHighAlign — RightFid: {Elapsed}ms", sw.ElapsedMilliseconds);

                sw.Restart();
                data.TopRightAlignRaw = await TopDieVisionRightAlign(data.AvgMove, size.Value, ct);
                double rightAlignZ = await GetCurrentPosition(MotionExtensions.H_Z, ct); // Align 촬상 Z
                _logger.Information("TopHighAlign — RightAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                // Z축 수직도 보정: Align 좌표를 Fid와 동일한 Z 평면으로 투영
                //   ΔZ = FidZ − AlignZ,  보정량 = tilt(1mm당 XY 변화량) × ΔZ
                //   CenterX = StageX − DxCamToMark,  CenterY(PC) = StageY + DyCamToMark 이므로 Stage 좌표에 가산
                double rDz = rightFidZ - rightAlignZ;
                double rTiltX = PC_R_HZ_TILT_X * rDz;
                double rTiltY = PC_R_HZ_TILT_Y * rDz;
                data.TopRightAlignRaw.DxCamToMark += rTiltX;
                data.TopRightAlignRaw.DyCamToMark += rTiltY;
                _logger.Information(
                    "TopHighAlign — Right H_Z tilt 보정: FidZ={FidZ:F4}, AlignZ={AlignZ:F4}, ΔZ={Dz:F4}mm → ΔX={Dx:F5}, ΔY={Dy:F5}",
                    rightFidZ, rightAlignZ, rDz, rTiltX, rTiltY);

                sw.Restart();
                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_X, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct),
                    MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct)
                );

                // ── Left: Fid → Align 측정 ──
                data.TopLeftFidRaw = await TopDieVisionLeftFid(data.AvgMove, ct);
                double leftFidZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);    // Fid 촬상 Z
                _logger.Information("TopHighAlign — LeftFid: {Elapsed}ms", sw.ElapsedMilliseconds);

                sw.Restart();
                data.TopLeftAlignRaw = await TopDieVisionLeftAlign(data.AvgMove, size.Value, ct);
                double leftAlignZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);  // Align 촬상 Z
                _logger.Information("TopHighAlign — LeftAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                // Z축 수직도 보정: Left Align 좌표를 Left Fid Z 평면으로 투영 (PC_L 계수)
                double lDz = leftFidZ - leftAlignZ;
                double lTiltX = PC_L_HZ_TILT_X * lDz;
                double lTiltY = PC_L_HZ_TILT_Y * lDz;
                data.TopLeftAlignRaw.DxCamToMark += lTiltX;
                data.TopLeftAlignRaw.DyCamToMark += lTiltY;
                _logger.Information(
                    "TopHighAlign — Left H_Z tilt 보정: FidZ={FidZ:F4}, AlignZ={AlignZ:F4}, ΔZ={Dz:F4}mm → ΔX={Dx:F5}, ΔY={Dy:F5}",
                    leftFidZ, leftAlignZ, lDz, lTiltX, lTiltY);
            }
            catch (ErrorException e)
            {
                throw;
            }
            finally
            {
                if (data.Use2DMapping) await MappingOff();
                //await Init_Head(ct);
                _logger.Information("TopHighAlign — 총 소요: {Elapsed}ms", total.ElapsedMilliseconds);
            }
            return data;
        }

        #endregion

        #region Btm Die 고배율 측정

        public async Task<AlignData> BtmHighAlign(
            AlignData data, CancellationToken ct)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var total = Stopwatch.StartNew();
            try
            {
                var sw = Stopwatch.StartNew();
                
                await MappingOff();

                if (data.Use2DMapping)
                {
                    await WTable2DMappingOn();
                }

                await TopDieSet(ct);
                //double hc1FidOffsetX = _recipeService.FindByParamDouble("HC1 피듀셜 위치 보정 X");
                //double hc1FidOffsetY = _recipeService.FindByParamDouble("HC1 피듀셜 위치 보정 Y");
                //double hc2FidOffsetX = _recipeService.FindByParamDouble("HC2 피듀셜 위치 보정 X");
                //double hc2FidOffsetY = _recipeService.FindByParamDouble("HC2 피듀셜 위치 보정 Y");
                double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);

                // H_Z 수직도(tilt) 계수 — Z 1mm 변화 시 XY 변화량 (Right=HC2, Left=HC1)
                double HC1_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.HC1_HZ_TILT_X);
                double HC1_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.HC1_HZ_TILT_Y);
                double HC2_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.HC2_HZ_TILT_X);
                double HC2_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.HC2_HZ_TILT_Y);

                sw.Restart();
                // Hc1X: 0.00361, Hc1Y: -0.00112, Hc2X: 0.00807, Hc2Y: -0.00269

                // 개별 측정
                if (data.UseBtmIndividualMeasure)
                {
                    var rFid = await BtmDieVisionRightFid(data.AvgMove, ct);
                    //data.BtmRightFidRaw = Point2D.of(rFid.DxCamToMark + hc2FidOffsetX, rFid.DyCamToMark + hc2FidOffsetY);
                    data.BtmRightFidRaw = Point2D.of(rFid.DxCamToMark, rFid.DyCamToMark);
                    _logger.Information("BtmHighAlign — RightFid: {Elapsed}ms", sw.ElapsedMilliseconds);
                   
                    sw.Restart();
                    var lFid = await BtmDieVisionLeftFid(data.AvgMove, ct);
                    data.BtmLeftFidRaw = Point2D.of(lFid.DxCamToMark , lFid.DyCamToMark);
                    _logger.Information("BtmHighAlign — LeftFid: {Elapsed}ms", sw.ElapsedMilliseconds);

                    // Fiducial 촬상 시점의 H_Z (Fid 평면)
                    double btmFidZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);

                    List<Point2D> hc1Raw = null;
                    List<Point2D> hc2Raw = null;
                    if(data.TracingMode == TracingMode.Manual)
                    {
                        (hc1Raw, hc2Raw) = await MeasureHcroPoints(data, ct);
                    }

                    await RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                    await RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);

                    // Align 촬상 시점의 H_Z (Align 평면)
                    double btmAlignZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);

                    // ── Z축 수직도 보정량 계산 ──
                    //   Fid/Align은 서로 다른 H_Z에서 촬상되어 tilt로 XY가 어긋남
                    //   ΔZ = AlignZ − FidZ, 보정량 = tilt(1mm당 XY 변화량) × ΔZ 를 Fid 평면 좌표에 가산
                    //   Right = HC2, Left = HC1
                    double bDz = btmAlignZ - btmFidZ;
                    Point2D lFidTilt = Point2D.of(HC1_HZ_TILT_X * bDz, HC1_HZ_TILT_Y * bDz);
                    Point2D rFidTilt = Point2D.of(HC2_HZ_TILT_X * bDz, HC2_HZ_TILT_Y * bDz);

                    if (data.TracingMode == TracingMode.Manual)
                    {
                        await CameraDist(data, ct);
                    }

                    if (data.TracingMode == TracingMode.Manual)
                    {
                        // 회전중심 측정점(HC1/HC2)도 동일하게 Align 평면으로 보정 후 원 피팅
                        ComputeHcroCenter(data, hc1Raw, hc2Raw, lFidTilt, rFidTilt);
                    }

                    // ── Fiducial을 Align과 동일한 Z 평면으로 투영 ──
                    data.BtmRightFidRaw = Point2D.of(
                        data.BtmRightFidRaw.X + rFidTilt.X,
                        data.BtmRightFidRaw.Y + rFidTilt.Y);
                    data.BtmLeftFidRaw = Point2D.of(
                        data.BtmLeftFidRaw.X + lFidTilt.X,
                        data.BtmLeftFidRaw.Y + lFidTilt.Y);
                    _logger.Information(
                        "BtmHighAlign — H_Z tilt 보정(Fid+HcRO): FidZ={FidZ:F4}, AlignZ={AlignZ:F4}, ΔZ={Dz:F4}mm → " +
                        "Right(HC2) ΔX={RDx:F5},ΔY={RDy:F5} / Left(HC1) ΔX={LDx:F5},ΔY={LDy:F5}",
                        btmFidZ, btmAlignZ, bDz, rFidTilt.X, rFidTilt.Y, lFidTilt.X, lFidTilt.Y);

                    sw.Restart();
                    var rAlign = await BtmDieVisionRightAlign(data.AvgMove, ct);
                    data.BtmRightAlignRaw = Point2D.of(rAlign.DxCamToMark, rAlign.DyCamToMark);
                    _logger.Information("BtmHighAlign — RightAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                    sw.Restart();
                    var lAlign = await BtmDieVisionLeftAlign(data.AvgMove, ct);
                    data.BtmLeftAlignRaw = Point2D.of(lAlign.DxCamToMark, lAlign.DyCamToMark);
                    _logger.Information("BtmHighAlign — LeftAlign: {Elapsed}ms", sw.ElapsedMilliseconds);
                }
                else
                {
                    var result = await BtmDieVisionAlign(avgMode: data.AvgMove);
                    data.BtmLeftFidRaw = Point2D.of(result.LeftFid.X, result.LeftFid.Y);
                    data.BtmLeftAlignRaw = result.LeftAlign;
                    data.BtmRightFidRaw = Point2D.of(result.RightFid.X, result.RightFid.Y);
                    data.BtmRightAlignRaw = result.RightAlign;
                    _logger.Information("BtmHighAlign : {Elapsed}ms", sw.ElapsedMilliseconds);
                }

                _logger.Information("BtmHighAlign — 총 소요: {Elapsed}ms", total.ElapsedMilliseconds);
            }
            catch(Exception e)
            {
                throw;
            }
            finally
            {
                _logger.Information("BtmHighAlign — 총 소요: {Elapsed}ms", total.ElapsedMilliseconds);
            }
            
            return data;
        }

        #endregion

        #region 피듀셜 트래킹

        private async Task MeasureFiducialDrift(AlignData data, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                _logger.Information("피듀셜 트래킹 측정 시작");

                await communicationService.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
                var fid1 = await communicationService.RequestVisionMarkPosition(
                    MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                if (fid1 == null || fid1.Result == Result.NG)
                    throw new VisionException(VisionErrorCode.MEASUREMENT_FAIL);

                await communicationService.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
                var fid2 = await communicationService.RequestVisionMarkPosition(
                    MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                if (fid2 == null || fid2.Result == Result.NG)
                    throw new VisionException(VisionErrorCode.MEASUREMENT_FAIL);

                data.Hc1FidCurrent = Point2D.of(fid1.X, fid1.Y);
                data.Hc2FidCurrent = Point2D.of(fid2.X, fid2.Y);

                double refHc1Dx = _paramService.GetDouble("Hc1FidRefDx");
                double refHc1Dy = _paramService.GetDouble("Hc1FidRefDy");
                double refHc2Dx = _paramService.GetDouble("Hc2FidRefDx");
                double refHc2Dy = _paramService.GetDouble("Hc2FidRefDy");
                Point2D camOffset = data.Hc2Offset;

                Point2D lf = Point2D.of(
                    -data.Hc1FidCurrent.X,
                    -data.Hc1FidCurrent.Y);
                Point2D rf = Point2D.of(
                    camOffset.X - data.Hc2FidCurrent.X,
                    camOffset.Y - data.Hc2FidCurrent.Y);
                data.FidCurrentDist = CalibrationMath.Dist(rf, lf);
                data.Hc1FidRef = Point2D.of(refHc1Dx, refHc1Dy);
                data.Hc2FidRef = Point2D.of(refHc2Dx, refHc2Dy);
                data.Hc1FidDrift = Point2D.of(fid1.X - refHc1Dx, fid1.Y - refHc1Dy);
                data.Hc2FidDrift = Point2D.of(fid2.X - refHc2Dx, fid2.Y - refHc2Dy);
                
                _logger.Information(
                    "피듀셜 트래킹 — HC1 drift({Hc1Dx:F6},{Hc1Dy:F6}), HC2 drift({Hc2Dx:F6},{Hc2Dy:F6}), FidDist:{Cur:F6} | {Elapsed}ms",
                    data.Hc1FidDrift.X, data.Hc1FidDrift.Y,
                    data.Hc2FidDrift.X, data.Hc2FidDrift.Y,
                    data.FidCurrentDist,
                    sw.ElapsedMilliseconds);

            }
            catch (VisionException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Warning(e, "피듀셜 트래킹 측정 실패 — 건너뜀");
            }
        }

        #endregion

    }
}
