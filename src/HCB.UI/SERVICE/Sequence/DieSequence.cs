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

                // 1. Hc 피듀셜 측정 — HC1/HC2는 raw 수집만.
                //    (M2FidTheta/FidCurrentDist 등 Hc2Offset 의존 계산은 CoordinateSystemIntegration)
                if (data.UseFiducialTracking)
                {
                    await MeasureFiducialDrift(data, ct);
                }
                // TopDie 사이즈 검색
                var size = _recipeService.FindByParam("TOP_DIE_SIZE");

                // ── Right: Fid → Align 측정 (Fid/Align은 서로 다른 H_Z 높이에서 촬상) ──
                data.TopRightFidRaw = await TopDieVisionRightFid(data.AvgMove, ct);
                double rightFidZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);   // Fid 촬상 Z
                _logger.Information("TopHighAlign — RightFid: {Elapsed}ms", sw.ElapsedMilliseconds);

                sw.Restart();
                data.TopRightAlignRaw = await TopDieVisionRightAlign(data.AvgMove, size.Value, ct);
                double rightAlignZ = await GetCurrentPosition(MotionExtensions.H_Z, ct); // Align 촬상 Z
                _logger.Information("TopHighAlign — RightAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                // 측정만: ΔZ만 기록. H_Z tilt 투영은 CoordinateSystemIntegration에서 수행.
                data.TopRightDz = rightFidZ - rightAlignZ;

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

                // 측정만: ΔZ만 기록.
                data.TopLeftDz = leftFidZ - leftAlignZ;
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
                double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);

                sw.Restart();
                if (data.UseBtmIndividualMeasure)
                {
                    await RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                    await RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);
                    double btmAlignZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);

                    if (data.TracingMode == TracingMode.Manual)
                    {
                        await CameraDist(data, ct);
                    }

                    sw.Restart();
                    var rAlign = await BtmDieVisionRightAlign(data.AvgMove, ct);
                    data.BtmRightAlignRaw = Point2D.of(rAlign.DxCamToMark, rAlign.DyCamToMark);
                    _logger.Information("BtmHighAlign — RightAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                    sw.Restart();
                    var lAlign = await BtmDieVisionLeftAlign(data.AvgMove, ct);
                    data.BtmLeftAlignRaw = Point2D.of(lAlign.DxCamToMark, lAlign.DyCamToMark);
                    _logger.Information("BtmHighAlign — LeftAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                    
                    double btmFidZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);

                    await RelativeMotionsMove(MotionExtensions.H_Z, -fidAlignGap, ct);
                    await RelativeMotionsMove(MotionExtensions.h_z, fidAlignGap, ct);

                    var rFid = await BtmDieVisionRightFid(data.AvgMove, ct);
                    data.BtmRightFidRaw = Point2D.of(rFid.DxCamToMark, rFid.DyCamToMark);
                    _logger.Information("BtmHighAlign — RightFid: {Elapsed}ms", sw.ElapsedMilliseconds);

                    sw.Restart();
                    var lFid = await BtmDieVisionLeftFid(data.AvgMove, ct);
                    data.BtmLeftFidRaw = Point2D.of(lFid.DxCamToMark , lFid.DyCamToMark);
                    _logger.Information("BtmHighAlign — LeftFid: {Elapsed}ms", sw.ElapsedMilliseconds);


                    if (data.TracingMode == TracingMode.Manual)
                    {
                        (data.Hc1RoRaw, data.Hc2RoRaw) = await MeasureHcroPoints(data, ct);
                    }

                    data.BtmDz = btmAlignZ - btmFidZ;
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

                data.Hc1FidRef = Point2D.of(refHc1Dx, refHc1Dy);
                data.Hc2FidRef = Point2D.of(refHc2Dx, refHc2Dy);
                data.Hc1FidDrift = Point2D.of(fid1.X - refHc1Dx, fid1.Y - refHc1Dy);
                data.Hc2FidDrift = Point2D.of(fid2.X - refHc2Dx, fid2.Y - refHc2Dy);

                _logger.Information(
                    "피듀셜 트래킹(raw 수집) — HC1 drift({Hc1Dx:F6},{Hc1Dy:F6}), HC2 drift({Hc2Dx:F6},{Hc2Dy:F6}) | {Elapsed}ms",
                    data.Hc1FidDrift.X, data.Hc1FidDrift.Y,
                    data.Hc2FidDrift.X, data.Hc2FidDrift.Y,
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
