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
                    MotionsMove(MotionExtensions.D_Y, $"DIE_ROW_{vacNum}", ct)
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
                    MotionsMove(MotionExtensions.D_Y, $"DIE_ROW_{vacNum}", ct)
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

        #region 회전중심 + 카메라 거리 측정 (Pickup 이전)

        /// <summary>
        /// 회전중심(HcRO)과 카메라 거리(Hc2Offset)를 측정한다.
        /// 종전에는 <see cref="BtmHighAlign"/> 내부에서 수행하던 캘리브레이션 측정을 Pickup 이전 단계로 분리한 것.
        ///  · <see cref="CameraDist"/>       → data.Hc2Offset       (Manual 트레이싱 또는 DIE 레시피)
        ///  · <see cref="MeasureHcroPoints"/> → data.Hc1RoRaw/Hc2RoRaw (Manual 트레이싱, 이후 ComputeHcroCenter에서 소비)
        ///
        /// 측정 결과는 <paramref name="data"/>에 누적되며, 동일 객체가 Pickup→TopHighAlign→BtmHighAlign→
        /// CoordinateSystemIntegration까지 공유되어야 캘리브레이션 값이 좌표계 통합에 반영된다.
        /// </summary>
        // Wafer 본딩 전용: 카메라 거리·회전중심(MeasureCamDistAndHcro) 측정을 수행할 위치(고배 절대좌표) 오버라이드.
        //  · null  → 기존 동작(PLACE_CENTER + HcCenterError 로 이동)
        //  · 지정  → 해당 절대좌표(예: WaferCenter의 고배 위치)로 이동해 측정
        // WaferSeqTabViewModel이 본딩 직전 WaferCenter로 설정하고 본딩 후 null로 되돌린다.
        // (StepSeqTab 자체 본딩은 이 값을 설정하지 않으므로 영향 없음)
        public Point2D CamHcroCenterOverride { get; set; }

        public async Task<AlignData> MeasureCamDistAndHcro(AlignData data, CancellationToken ct)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var total = Stopwatch.StartNew();
            try
            {
                await MappingOff();
                if (data.TracingMode == TracingMode.Manual)
                {
                    bool isDieRecipe = _recipeService.UseRecipe?.Component == HCB.Data.Entity.Type.ComponentType.DIE;
                    if (isDieRecipe && data.Use2DMapping) await WTable2DMappingOn();

                    // 측정 위치: 오버라이드(WaferCenter)가 있으면 그 위치, 없으면 기존 PLACE_CENTER
                    await TopDieSet(ct, CamHcroCenterOverride);
                    double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
                    await RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                    await RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);
                    await CameraDist(data, ct);
                    await RelativeMotionsMove(MotionExtensions.H_Z, -fidAlignGap, ct);
                    await RelativeMotionsMove(MotionExtensions.h_z, fidAlignGap, ct);                    
                    (data.Hc1RoRaw, data.Hc2RoRaw) = await MeasureHcroPoints(data, ct);

                }

                _logger.Information("MeasureCamDistAndHcro — 총 소요: {Elapsed}ms", total.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Error(e, "MeasureCamDistAndHcro 실패");
                throw;
            }
            finally
            {
                if (data.Use2DMapping) await MappingOff();
            }

            return data;
        }

        #endregion

        #region Btm Die 고배율 측정

        // placeCenter != null : TopDieSet에서 PLACE_CENTER 대신 지정 Die Center(고배 절대좌표)로 이동
        public async Task<AlignData> BtmHighAlign(
            AlignData data, CancellationToken ct, Point2D placeCenter = null)
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

                await TopDieSet(ct, placeCenter);
                double fidAlignGap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);

                // RECIPE가 DIE일 때만 Btm θ 보정(카메라 거리 → BLA/BRA 각도 → W_T)을 수행한다.
                bool isDieRecipe = _recipeService.UseRecipe?.Component == HCB.Data.Entity.Type.ComponentType.DIE;

                sw.Restart();
                if (data.UseBtmIndividualMeasure)
                {
                    await RelativeMotionsMove(MotionExtensions.h_z, -fidAlignGap, ct);
                    await RelativeMotionsMove(MotionExtensions.H_Z, fidAlignGap, ct);
                    double btmAlignZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);

                    // 카메라 거리(Hc2Offset)·회전중심(HcRO) 측정은 Pickup 이전 단계(MeasureCamDistAndHcro)로 분리됨

                    // 2. BTM DIE 측정 (BLA=Left/HC1, BRA=Right/HC2 얼라인마크)
                    sw.Restart();
                    var rAlign = await BtmDieVisionRightAlign(data.AvgMove, ct);
                    data.BtmRightAlignRaw = Point2D.of(rAlign.DxCamToMark, rAlign.DyCamToMark);
                    _logger.Information("BtmHighAlign — RightAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                    sw.Restart();
                    var lAlign = await BtmDieVisionLeftAlign(data.AvgMove, ct);
                    data.BtmLeftAlignRaw = Point2D.of(lAlign.DxCamToMark, lAlign.DyCamToMark);
                    _logger.Information("BtmHighAlign — LeftAlign: {Elapsed}ms", sw.ElapsedMilliseconds);

                    //// 3. BTM 보정 시퀀스 → 4. BTM DIE 재측정 (DIE 레시피 전용)
                    //if (isDieRecipe)
                    //{
                    //    // 3. 측정 θ(카메라 거리 기반 BLA→BRA)와 도면 θ의 차이만큼 W_T 회전 보정
                    //    await BtmThetaCorrection(data, ct);

                    //    // 4. 보정 후 BTM DIE 재측정 (다운스트림 좌표통합에 보정된 값 반영)
                    //    sw.Restart();
                    //    var rAlign2 = await BtmDieVisionRightAlign(data.AvgMove, ct);
                    //    data.BtmRightAlignRaw = Point2D.of(rAlign2.DxCamToMark, rAlign2.DyCamToMark);
                    //    var lAlign2 = await BtmDieVisionLeftAlign(data.AvgMove, ct);
                    //    data.BtmLeftAlignRaw = Point2D.of(lAlign2.DxCamToMark, lAlign2.DyCamToMark);
                    //    _logger.Information("BtmHighAlign — DIE θ보정 후 재측정 완료: {Elapsed}ms", sw.ElapsedMilliseconds);
                    //}

                    double btmFidZ = await GetCurrentPosition(MotionExtensions.H_Z, ct);

                    await RelativeMotionsMove(MotionExtensions.H_Z, -fidAlignGap, ct);                    
                    await RelativeMotionsMove(MotionExtensions.h_z, fidAlignGap, ct);
                    //await MotionsMove(MotionExtensions.h_z, MotionExtensions.HEAD_SAFETY, ct);
                    //await RelativeMotionsMove(MotionExtensions.H_Z, 0.2, ct);
                    
                    var rFid = await BtmDieVisionRightFid(data.AvgMove, ct);
                    data.BtmRightFidRaw = Point2D.of(rFid.DxCamToMark, rFid.DyCamToMark);
                    _logger.Information("BtmHighAlign — RightFid: {Elapsed}ms", sw.ElapsedMilliseconds);

                    sw.Restart();
                    var lFid = await BtmDieVisionLeftFid(data.AvgMove, ct);
                    data.BtmLeftFidRaw = Point2D.of(lFid.DxCamToMark , lFid.DyCamToMark);
                    _logger.Information("BtmHighAlign — LeftFid: {Elapsed}ms", sw.ElapsedMilliseconds);

                    // 회전중심(HcRO) raw 측정은 Pickup 이전 단계(MeasureCamDistAndHcro)로 분리됨

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

                    // DIE θ 보정은 개별 측정(카메라 거리 → 측정 → 보정 → 재측정) 흐름을 전제로 한다.
                    if (isDieRecipe)
                        _logger.Warning("BtmHighAlign — DIE 레시피이나 통합 측정 모드입니다. Btm θ 보정을 수행하려면 개별 측정(UseBtmIndividualMeasure=true)을 사용하세요.");
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

        #region Btm θ 보정 (RECIPE=DIE 전용)

        /// <summary>
        /// RECIPE가 DIE일 때만 수행하는 Btm 각도 보정.
        /// BLA→BRA 도면상 상대거리(레시피 BTM_ALIGN_REF_X/Y)로부터 "도면 θ"를 구하고,
        /// 실제 측정한 BLA/BRA로부터 "측정 θ"를 구해, 둘이 다를 때 그 차이만큼 W_T를 회전 보정한다.
        ///
        ///  · 측정 θ: BLA(Btm Left AlignMark, HC1) / BRA(Btm Right AlignMark, HC2)는 서로 다른 카메라
        ///           프레임에서 측정되므로, 카메라 거리(<see cref="AlignData.Hc2Offset"/> = CameraDist 결과)로
        ///           두 측정을 통합한 실제 BLA→BRA 벡터의 각도.
        ///  · 도면 θ: 레시피에 입력된 도면상 BLA→BRA 상대거리(BTM_ALIGN_REF_X/Y) 벡터의 각도.
        ///           (미설정 시 0° = 수평 도면으로 간주)
        ///  · 보정량: (측정 θ − 도면 θ) 만큼 W_T 회전 → 다이의 실제 각도를 도면 각도에 일치.
        ///
        /// 호출 전 <paramref name="data"/>.Hc2Offset, BtmLeftAlignRaw, BtmRightAlignRaw가
        /// 채워져 있어야 한다(카메라 거리 측정 + Btm Die 측정 완료 후 호출).
        /// 반환: 적용한 (측정 θ − 도면 θ) 차이(°).
        /// </summary>
        private async Task<double> BtmThetaCorrection(AlignData data, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // ── 측정 θ: 카메라 거리(Hc2Offset)로 HC1/HC2 두 프레임 측정을 통합한 실제 BLA→BRA 벡터 각도 ──
            //  bl = -BtmLeftAlignRaw (HC1, Stage 기준 부호 반전)
            //  br = Hc2Offset - BtmRightAlignRaw (HC2, 카메라 거리 적용)
            Point2D camOffset = data.Hc2Offset;
            Point2D bl = Point2D.of(-data.BtmLeftAlignRaw.X, -data.BtmLeftAlignRaw.Y);
            Point2D br = Point2D.of(camOffset.X - data.BtmRightAlignRaw.X,
                                    camOffset.Y - data.BtmRightAlignRaw.Y);
            Point2D measRel = Point2D.of(br.X - bl.X, br.Y - bl.Y);
            double measThetaDeg = NormalizeHalfDeg(Math.Atan2(measRel.Y, measRel.X) * (180.0 / Math.PI));

            // ── 도면 θ: 레시피에 입력된 도면상 BLA→BRA 상대거리(BTM_ALIGN_REF_X/Y)의 각도 ──
            double designThetaDeg = 0.0;
            if (TryGetRecipeDouble("BTM_ALIGN_REF_X", out double refX) &&
                TryGetRecipeDouble("BTM_ALIGN_REF_Y", out double refY) &&
                (refX != 0.0 || refY != 0.0))
            {
                designThetaDeg = NormalizeHalfDeg(Math.Atan2(refY, refX) * (180.0 / Math.PI));
            }
            else
            {
                _logger.Warning("BTM θ 보정 — 도면상 BLA→BRA 상대거리(BTM_ALIGN_REF_X/Y) 미설정 → 도면 θ=0°로 간주");
            }

            // ── 측정 θ와 도면 θ의 차이만큼 W_T 회전 보정 ──
            double diffDeg = NormalizeHalfDeg(measThetaDeg + designThetaDeg);

            double thetaSign = GetEcParamDouble("BtmThetaSign", -1.0);   // 하드웨어 방향 반대면 +1
            double thetaMinDeg = GetEcParamDouble("BtmThetaMinDeg", 0.0); // 데드밴드(° 미만이면 스킵)
            if (Math.Abs(diffDeg) >= thetaMinDeg)
            {
                double corr = thetaSign * diffDeg;
                _logger.Information(
                    "BTM θ 보정 — 측정θ={Meas:F6}°, 도면θ={Design:F6}°, 차이={Diff:F6}° → W_T {Move:F6}° 회전 " +
                    "(measRel=({RX:F6},{RY:F6}), Hc2Offset=({OX:F5},{OY:F5}))",
                    measThetaDeg, designThetaDeg, diffDeg, -corr, measRel.X, measRel.Y, camOffset.X, camOffset.Y);
                await RelativeMotionsMove(MotionExtensions.W_T, -corr, ct);
            }
            else
            {
                _logger.Information(
                    "BTM θ 보정 — 측정θ={Meas:F6}°, 도면θ={Design:F6}°, 차이={Diff:F6}° < {Min}° → 보정 스킵",
                    measThetaDeg, designThetaDeg, diffDeg, thetaMinDeg);
            }

            return diffDeg;
        }

        /// <summary>
        /// 각도를 ±90° 범위로 정규화한다(좌/우 마크 순서 뒤바뀜 등 180° 모호성 제거).
        /// </summary>
        private static double NormalizeHalfDeg(double deg)
        {
            while (deg > 90.0) deg -= 180.0;
            while (deg < -90.0) deg += 180.0;
            return deg;
        }

        /// <summary>
        /// 사용중인 레시피에서 double 파라미터를 안전하게 읽는다(없거나 파싱 실패 시 false).
        /// </summary>
        private bool TryGetRecipeDouble(string name, out double value)
        {
            value = 0.0;
            var p = _recipeService.UseRecipe?.ParamList?.FirstOrDefault(x => x.Name == name);
            if (p == null || string.IsNullOrWhiteSpace(p.Value)) return false;
            return double.TryParse(p.Value, out value);
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
