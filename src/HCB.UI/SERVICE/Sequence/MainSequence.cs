using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using SharpDX.Direct2D1.Effects;
using SharpDX.Direct3D9;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        // ═══════════════════════════════════════════════════
        //  Public Sequence Entry Points
        // ═══════════════════════════════════════════════════


        public async Task DTablePickup(DieType dieType, int vacNum, VisionMarkPositionResponse? correction, CancellationToken ct)
        {
            string label = dieType == DieType.TOP ? "TOP" : "BTM";
            try
            {
                _logger.Information("{Label} Die pickup Start", label);
                EQStatusCheck();

                var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                // ── 공통 레시피 ──
                double xOffset = await GetRecipe("ShankLowOffsetX");
                double yOffset = await GetRecipe("ShankLowOffsetY");
                double shankToDieOffset = await GetRecipe("ShankToDieOffset");

                // ── Die 타입별 레시피 ──
                string thicknessKey = dieType == DieType.TOP ? "TopDieThickness" : "BtmDieThickness";

                double dieThickness = await GetRecipe(thicknessKey);
                int accTime = await GetRecipeInt("PICKUP_ACC_TIME");
                int contTime = await GetRecipeInt("PICKUP_CONT_TIME");
                int decTime = await GetRecipeInt("PICKUP_DEC_TIME");
                double loadCell = await GetRecipe("PICKUP_LOADCELL");
                double current = await GetRecipe("PICKUP_CURRENT");
                int headVacOnMs = await GetRecipeInt("PICKUP_HEAD_VAC_ON_TIME");
                int dtableVacOffMs = await GetRecipeInt("PICKUP_DTABLE_VAC_OFF_TIME");
                double readyPosition = await GetRecipe("PICKUP_READY_POSITION");

                // ── 1. Head 안전 위치 이동 ──
                await Init_Head(ct);

                // ── 2. 픽업 위치 이동 + 보정 ──
                double corrX = correction?.X ?? 0;
                double corrY = correction?.Y ?? 0;
                double corrT = correction?.Theta ?? 0;

                var xyTask = Task.WhenAll(
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, xOffset - corrX, ct),
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, yOffset - corrY, ct)
                );
                var tTask = MotionsMove(MotionExtensions.H_T, -corrT, ct);

                var goPickup = await xyTask;
                await tTask;

                if (!goPickup.All(r => r)) throw new Exception("픽업 위치로 이동 실패");

                // ── 3. Z축 하강 ──
                await MotionsMove(MotionExtensions.H_Z, shankToDieOffset - dieThickness - readyPosition, ct);
                await Task.Delay(200, ct);

                // ── 4. 가압 시퀀스 ──
                // 이전 상태 클리어
                await device.SendCommand(MotionExtensions.BONDING_START + "=0");
                await device.SendCommand(MotionExtensions.BONDING_INIT + "=1");
                await Task.Delay(100);
                await device.SendCommand(MotionExtensions.BONDING_INIT + "=0");
                await Task.Delay(50);

                // 클리어 확인
                string preCheck = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                int preStatus = int.TryParse(preCheck.Trim(), out int ps) ? ps : -1;
                _logger.Information("{Label} 가압 시작 전 상태: {Status}", label, preStatus);
                if (preStatus != 0)
                    _logger.Warning("{Label} STATUS_COMPLETE가 0으로 초기화되지 않음: {Status}", label, preStatus);

                // 파라미터 설정 + 시작
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={accTime}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={contTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={decTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={loadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={current}");
                await device.SendCommand(MotionExtensions.BONDING_START + "=1");

                const int pollingIntervalMs = 100;
                int timeoutMs = accTime + contTime + decTime + 2000;
                var sw = Stopwatch.StartNew();
                bool pressComplete = false;
                bool headVacOn = false;
                bool dtableVacOff = false;
                _logger.Information("{Label} PICKUP 파라미터: ACC={Acc}, CONT={Cont}, DEC={Dec}, LOADCELL={Load}, CURRENT={Cur}",
                    label, accTime, contTime, decTime, loadCell, current);
                while (!pressComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    long elapsed = sw.ElapsedMilliseconds;
                    var loopSw = Stopwatch.StartNew();

                    // Head 진공 ON 시점 + 픽업 센서 확인
                    if (!headVacOn && elapsed >= headVacOnMs)
                    {
                        var picked = await _sequenceHelper.HeadPickerVacuum(eOnOff.On, ct);
                        headVacOn = true;
                        _logger.Information("{Label} Head Vacuum ON ({Elapsed}ms, 설정={SetMs}ms)",
                            label, elapsed, headVacOnMs);
                        if (!picked) throw new Exception("Head에 Pick된 Die가 없습니다");
                    }

                    // DTable 진공 OFF 시점
                    if (!dtableVacOff && elapsed >= dtableVacOffMs)
                    {
                        await SwitchDTableVacuum(dieType, vacNum, eOnOff.Off, ct);
                        dtableVacOff = true;
                        _logger.Information("{Label} DTable Vacuum OFF ({Elapsed}ms, 설정={SetMs}ms)",
                            label, elapsed, dtableVacOffMs);
                    }

                    double forceValue = 0;
                    string analog = await device.SendCommand<string>(MotionExtensions.ANALOG_INPUT);
                    if (double.TryParse(analog.Trim(), out forceValue))
                    {
                        _logger.Debug("Force: {Force:F3}N ({Elapsed}ms)",
                            forceValue * 0.00373, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.Warning("AnalogInput 파싱 실패: {Response}", analog);
                    }

                    string strResponse = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                    _logger.Debug("BONDING_STATUS_COMPLETE 원본 응답: [{Response}]", strResponse);

                    var values = strResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length > 0 && int.TryParse(values[0].Trim(), out int statusCode))
                    {
                        pressComplete = statusCode == 6;
                        _logger.Information("{Label} Pickup press 상태: {Code} (complete={Complete}) | Force: {Force:F3}N (경과: {Elapsed}ms)",
                            label, statusCode, pressComplete, forceValue * 0.00373, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.Warning("파싱 실패 | Length={Len}, values[0]=[{Val}], 원본=[{Raw}]",
                            values.Length, values.Length > 0 ? values[0] : "EMPTY", strResponse);
                    }

                    loopSw.Stop();
                    _logger.Debug("폴링 루프 1회 소요: {LoopMs}ms", loopSw.ElapsedMilliseconds);

                    if (!pressComplete)
                    {
                        if (sw.ElapsedMilliseconds > timeoutMs)
                            throw new TimeoutException($"{label} Pickup press 완료 대기 시간 초과 ({timeoutMs}ms)");

                        await Task.Delay(pollingIntervalMs, ct);
                    }
                }

                sw.Stop();
                _logger.Information("{Label} Pickup press 완료 (총 소요: {Elapsed}ms)", label, sw.ElapsedMilliseconds);

                // ── 5. 복귀 ──
                await Task.Delay(300);
                await Init_Head(ct);
                await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("{Label} Pickup 작업이 취소되었습니다.", label);
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.Error(ex, "{Label} Pickup press 타임아웃", label);
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "{Label} Pickup 실패", label);
                throw new Exception(e.Message);
            }
            finally
            {
                try
                {
                    var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                    await device.SendCommand(MotionExtensions.BONDING_START + "=0");
                    await device.SendCommand(MotionExtensions.BONDING_INIT + "=1");
                    await Task.Delay(100);
                    await device.SendCommand(MotionExtensions.BONDING_INIT + "=0");
                    _logger.Information("Pickup press 초기화 완료");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Pickup press 초기화 실패");
                }
            }
        }
        /// <summary>
        /// DieType에 따라 DTable 진공을 전환하는 헬퍼
        /// </summary>
        private async Task SwitchDTableVacuum(DieType dieType, int vacNum, eOnOff onOff, CancellationToken ct)
        {
            if (dieType == DieType.TOP)
                await _sequenceHelper.TopVac(vacNum, onOff, ct);
            else
                await _sequenceHelper.BTMVac(vacNum, onOff, ct);
        }

        public async Task DieDrop(int vacNum, CancellationToken ct)
        {
            try
            {
                _logger.Information("BtmDieDrop Start");
                EQStatusCheck();

                var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = await GetRecipe("ShankToWaferOffset");
                double readyPosition = await GetRecipe("DROP_READY_POSITION");
                int accTime = await GetRecipeInt("DROP_ACC_TIME");
                int contTime = await GetRecipeInt("DROP_CONT_TIME");
                int decTime = await GetRecipeInt("DROP_DEC_TIME");
                double loadCell = await GetRecipe("DROP_LOADCELL");
                double current = await GetRecipe("DROP_CURRENT");
                int wtableVacOnMs = await GetRecipeInt("DROP_WTABLE_VAC_ON_TIME");   // WTable 진공 ON 시점
                int headVacOffMs = await GetRecipeInt("DROP_HEAD_VAC_OFF_TIME");     // Head 진공 OFF 시점

                // ── 1. 이동 ──
                await Init_Head(ct);
                await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);

                // ── 2. Z축 하강 ──
                await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - btmDieThickness - readyPosition, ct);
                await Task.Delay(200, ct);

                // ── 3. 가압 시퀀스 ──
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={accTime}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={contTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={decTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={loadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={current}");
                await device.SendCommand(MotionExtensions.BONDING_START + $"=1");

                const int pollingIntervalMs = 100;
                int timeoutMs = accTime + contTime + decTime + 2000;
                var sw = Stopwatch.StartNew();
                bool pressComplete = false;
                bool wtableVacOn = false;
                bool headVacOff = false;

                while (!pressComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    long elapsed = sw.ElapsedMilliseconds;

                    // WTable 진공 ON 시점 (받는 쪽 먼저 흡착)
                    if (!wtableVacOn && elapsed >= wtableVacOnMs)
                    {
                        await _sequenceHelper.WTableVacuum(vacNum, eOnOff.On, ct);
                        wtableVacOn = true;
                        _logger.Information("WTable Vacuum ON ({Elapsed}ms, 설정={SetMs}ms)",
                            elapsed, wtableVacOnMs);
                    }

                    // Head 진공 OFF 시점 (놓는 쪽 해제)
                    if (!headVacOff && elapsed >= headVacOffMs)
                    {
                        var released = await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, ct);
                        headVacOff = true;
                        _logger.Information("Head Vacuum OFF ({Elapsed}ms, 설정={SetMs}ms)",
                            elapsed, headVacOffMs);
                        if (!released) throw new Exception("HeadPicker를 확인해주세요");
                    }

                    double forceValue = 0;
                    string analog = await device.SendCommand<string>(MotionExtensions.ANALOG_INPUT);
                    if (double.TryParse(analog.Trim(), out forceValue))
                    {
                        _logger.Debug("Force: {Force:F3}N ({Elapsed}ms)",
                            forceValue * 0.00373, sw.ElapsedMilliseconds);
                    }

                    string strResponse = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                    var values = strResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length > 0 && bool.TryParse(values[0], out bool result))
                    {
                        _logger.Information("Drop press 상태: {Result} | Force: {Force:F3}N (경과: {Elapsed}ms)",
                            result, forceValue * 0.00373, sw.ElapsedMilliseconds);
                        pressComplete = result;
                    }
                    else
                    {
                        _logger.Warning("Drop press 상태 응답 파싱 실패: {Response}", strResponse);
                    }

                    if (!pressComplete)
                    {
                        if (sw.ElapsedMilliseconds > timeoutMs)
                            throw new TimeoutException($"Drop press 완료 대기 시간 초과 ({timeoutMs}ms)");

                        await Task.Delay(pollingIntervalMs, ct);
                    }
                }

                sw.Stop();
                _logger.Information("BtmDieDrop press 완료 (총 소요: {Elapsed}ms)", sw.ElapsedMilliseconds);

                // ── 4. 복귀 ──
                await Init_Head(ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("BtmDieDrop 작업이 취소되었습니다.");
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.Error(ex, "BtmDieDrop press 타임아웃");
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "BtmDieDrop 실패");
                throw new Exception(e.Message);
            }
            finally
            {
                try
                {
                    var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                    await device.SendCommand(MotionExtensions.BONDING_START + $"=0");
                    await device.SendCommand(MotionExtensions.BONDING_INIT + $"=1");
                    await Task.Delay(100);
                    await device.SendCommand(MotionExtensions.BONDING_INIT + $"=0");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Drop press 초기화 실패");
                }
            }
        }



        public async Task MachineStartAsync(int topDie, int btmDie, CancellationToken ct)
        {
            try
            {
                _logger.Information("Auto Run Start");

                var Status = _operationService.Status;
                if (Status.Availability == Availability.Down)
                {
                    _logger.Warning("Cannot execute MachineStartAsync: Sequence Service is not in Auto Standby Status.");
                    return;
                }

                // 1. Die wafer 얼라인
                var BtmDieAlign = await DTableCarrierAlign(btmDie, MarkType.DIE_CENTER_BOTTOM, ct);

                // 2. 픽업
                await DTableBTMPickup(btmDie, BtmDieAlign, ct);
                await BtmDieDrop(1, ct);
                await Init_Head(ct);

                // 3. 픽업 다이 얼라인
                var TopDieAlign = await DTableCarrierAlign(topDie, MarkType.DIE_CENTER_TOP, ct);
                await DTableTOPPickup(topDie, TopDieAlign, ct);

                // 4. 웨이퍼 다이 얼라인
                var topDieVisionResults = await TopDieVision(ct);

                // 5. 본딩
                await TopDieDrop(topDieVisionResults, ct);
                await Task.Delay(1000);
                await Init_Head(ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Auto Run Canceled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                throw;
            }
            finally
            {
                _logger.Information("Auto Run End");
            }
        }
        #region 저배율 카메라 측정 및 픽업
        public async Task<VisionMarkPositionResponse> TopLowAlign(
            int topDie, CancellationToken ct)
        {
            return await TopCarrierAlign(topDie, MarkType.DIE_CENTER_TOP, ct);
        }

        public async Task TopPickup(
            int topDie, VisionMarkPositionResponse visionResult, CancellationToken ct)
        {
            await DTableTOPPickup(topDie, visionResult, ct);
        }
        #endregion

        #region Top Die 고배율 측정


        public async Task<AlignData> TopHighAlign(
            AlignData data, CancellationToken ct)
        {
            data ??= new AlignData();            
            data.TopRightFidRaw = await TopDieVisionRightFid(data.AvgMove, ct);
            data.TopRightAlignRaw = await TopDieVisionRightAlign(data.AvgMove,ct);
            data.TopLeftFidRaw = await TopDieVisionLeftFid(data.AvgMove, ct);
            data.TopLeftAlignRaw = await TopDieVisionLeftAlign( data.AvgMove, ct);

            return data;
        }

        #endregion

        #region Btm Die 고배율 측정

        //public async Task<AlignContext> BtmHighAlign(
        //    AlignContext ctx, bool avgMode, CancellationToken ct)
        //{
        //    if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        //    await TopDiePlace(ct);

        //    // 비전 원본 → Raw (이후 어떤 메서드도 수정 금지)
        //    ctx.BtmRightFidRaw = await BtmDieVisionRightFid(avgMode, ct);
        //    ctx.BtmRightAlignRaw = await BtmDieVisionRightAlign(avgMode,ct);
        //    ctx.BtmLeftFidRaw = await BtmDieVisionLeftFid(avgMode, ct);
        //    ctx.BtmLeftAlignRaw = await BtmDieVisionLeftAlign(avgMode, ct);
        //    //await GetHcro(ctx, ct);
        //    LoadCalibrationInto(ctx);   

        //    // 카메라 오프셋 + 회전 보정
        //    ApplyBtmCorrections(ctx);
        //    // 회전 중심 좌표로 변환
        //    ComputeHcroCoords(ctx);
        //    ComputeBtmOffsets(ctx);

        //    return ctx;
        //}

        public async Task<AlignData> BtmHighAlign(
            AlignData data, CancellationToken ct)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            await TopDiePlace(ct);

            data.BtmRightFidRaw = await BtmDieVisionRightFid(data.AvgMove, ct);
            data.BtmRightAlignRaw = await BtmDieVisionRightAlign(   data.AvgMove, ct);
            data.BtmLeftFidRaw = await BtmDieVisionLeftFid(data.AvgMove, ct);
            data.BtmLeftAlignRaw = await BtmDieVisionLeftAlign(data.AvgMove, ct);
            return data;
        }

        #endregion

        #region Top Die Place 및 Hcro 연산
        public async Task TopPlace(AlignData data, CancellationToken ct)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            LoadCalibrationInto(data);

            // ── STEP 1: Top Die — Fid→Align 이동량 ──
            var lDist = Point2D.of(
                data.TopLeftAlignRaw.CenterX - data.TopLeftFidRaw.CenterX,
                data.TopLeftAlignRaw.CenterY - data.TopLeftFidRaw.CenterY);
            var rDist = Point2D.of(
                data.TopRightAlignRaw.CenterX - data.TopRightFidRaw.CenterX,
                data.TopRightAlignRaw.CenterY - data.TopRightFidRaw.CenterY);
            data.LDist = lDist;
            data.RDist = rDist;

            // ── STEP 2: Btm Die 좌표 통합 + Top 위치 생성 ──
            // Btm: Stage 기준 X:-, Y:- → DxCam 부호 반전
            Point2D camOffset = data.Hc2Offset;

            Point2D bl = Point2D.of(
                -data.BtmLeftAlignRaw.DxCamToMark,
                -data.BtmLeftAlignRaw.DyCamToMark);
            Point2D br = Point2D.of(
                camOffset.X - data.BtmRightAlignRaw.DxCamToMark,
                camOffset.Y - data.BtmRightAlignRaw.DyCamToMark);

            Point2D bfl = Point2D.of(
                -data.BtmLeftFidRaw.DxCamToMark,
                -data.BtmLeftFidRaw.DyCamToMark);
            Point2D bfr = Point2D.of(
                camOffset.X - data.BtmRightFidRaw.DxCamToMark,
                camOffset.Y - data.BtmRightFidRaw.DyCamToMark);
            data.BFL = bfl;
            data.BFR = bfr;

            // Top: Center 기준 X:-, Y:+ → lDist에 (-X, -Y) 적용
            Point2D tl = Point2D.of(bfl.X - lDist.X, bfl.Y - lDist.Y);
            Point2D tr = Point2D.of(bfr.X - rDist.X, bfr.Y - rDist.Y);

            // ── STEP 3: 회전중심(HCRO) 기준으로 좌표 이동 ──
            Point2D hcro = data.Hcro;
            bl = Point2D.of(bl.X - hcro.X, bl.Y - hcro.Y);
            br = Point2D.of(br.X - hcro.X, br.Y - hcro.Y);
            tl = Point2D.of(tl.X - hcro.X, tl.Y - hcro.Y);
            tr = Point2D.of(tr.X - hcro.X, tr.Y - hcro.Y);

            // ── STEP 4: θ 계산 ──
            double thetaS = ParseRecipe("SPEC_THETA");
            double bTheta = Math.Atan2(br.Y - bl.Y, br.X - bl.X);
            double tTheta = Math.Atan2(tr.Y - tl.Y, tr.X - tl.X);
            double thetaF = thetaS - CalibrationMath.ToDegree(tTheta - bTheta);
            double thetaF_rad = CalibrationMath.ToRadian(thetaF);

            data.SpecTheta = thetaS;
            data.BTheta = bTheta;
            data.TTheta = tTheta;
            data.ThetaF = thetaF;
            data.ThetaFRad = thetaF_rad;

            // ── STEP 5: Top 마크 회전 보정 ──
            tl = CalibrationMath.ApplyRotation(tl, thetaF_rad);
            tr = CalibrationMath.ApplyRotation(tr, thetaF_rad);

            // ── STEP 6: Shift 계산 ──
            Point2D tCenter = Point2D.of((tl.X + tr.X) / 2.0, (tl.Y + tr.Y) / 2.0);
            Point2D bCenter = Point2D.of((bl.X + br.X) / 2.0, (bl.Y + br.Y) / 2.0);

            data.BL = bl;
            data.BR = br;
            data.TL = tl;
            data.TR = tr;
            data.TCenter = tCenter;
            data.BCenter = bCenter;

            double shiftX = tCenter.X - bCenter.X;
            double shiftY = tCenter.Y - bCenter.Y;

            data.ResultX = shiftX + data.OffsetXY.X;
            data.ResultY = shiftY + data.OffsetXY.Y;
            data.ResultT = thetaF + data.OffsetT;

            //await Task.WhenAll(
            //    RelativeMotionsMove(MotionExtensions.H_X, shiftX, ct),
            //    RelativeMotionsMove(MotionExtensions.W_Y, shiftY, ct),
            //    RelativeMotionsMove(MotionExtensions.H_T, thetaF, ct)
            //);
        }

        #endregion

        // ═══════════════════════════════════════════════════
        //  Step 3: 각도 보정 → Shift → 본딩
        //   BtmHighAlign 완료 후 ctx.Hcro* 필요
        // ═══════════════════════════════════════════════════


        public async Task TopPlace(
            AlignContext ctx, RecipeService recipeService, ObservableCollection<BondingDataPoint> bondingDataPoints, CancellationToken ct)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var offsetX = double.Parse(_recipeService.FindByParam("X_ALIGN_OFFSET").Value);
            var offsetY = double.Parse(_recipeService.FindByParam("Y_ALIGN_OFFSET").Value);
            var offsetT = double.Parse(_recipeService.FindByParam("T_ALIGN_OFFSET").Value);

            // ── 입력값 전체 로깅 ─────────────────────────────────
            _logger.Information($"[INPUT] HcroLA    = ({ctx.HcroLA.X:F4}, {ctx.HcroLA.Y:F4})");
            _logger.Information($"[INPUT] HcroRA    = ({ctx.HcroRA.X:F4}, {ctx.HcroRA.Y:F4})");
            _logger.Information($"[INPUT] HcroLF    = ({ctx.HcroLF.X:F4}, {ctx.HcroLF.Y:F4})");
            _logger.Information($"[INPUT] HcroRF    = ({ctx.HcroRF.X:F4}, {ctx.HcroRF.Y:F4})");
            _logger.Information($"[INPUT] HcroTopLA = ({ctx.HcroTopLA.X:F4}, {ctx.HcroTopLA.Y:F4})");
            _logger.Information($"[INPUT] HcroTopRA = ({ctx.HcroTopRA.X:F4}, {ctx.HcroTopRA.Y:F4})");
            _logger.Information($"[INPUT] HcroTopLF = ({ctx.HcroTopLF.X:F4}, {ctx.HcroTopLF.Y:F4})");
            _logger.Information($"[INPUT] HcroTopRF = ({ctx.HcroTopRF.X:F4}, {ctx.HcroTopRF.Y:F4})");

            // ── B-Die 중심 ───────────────────────────────────────
            double btmCenterX = (ctx.HcroLA.X + ctx.HcroRA.X) / 2.0;
            double btmCenterY = (ctx.HcroLA.Y + ctx.HcroRA.Y) / 2.0;
            _logger.Information($"[BTM]   Center    = ({btmCenterX:F4}, {btmCenterY:F4})");

            // ── T-Die 중심 (회전 전) ─────────────────────────────
            double topCenterBeforeX = (ctx.HcroTopLA.X + ctx.HcroTopRA.X) / 2.0;
            double topCenterBeforeY = (ctx.HcroTopLA.Y + ctx.HcroTopRA.Y) / 2.0;
            _logger.Information($"[TOP]   Center(before rotate) = ({topCenterBeforeX:F4}, {topCenterBeforeY:F4})");

            // ── 얼라인 Angle 연산 ────────────────────────────────
            double thetaO = CalibrationMath.ComputeAlignAngle(
                ctx.HcroLA, ctx.HcroRA,
                ctx.HcroTopLA, ctx.HcroTopRA);      // rad 
            double thetaS = ParseRecipe("SPEC_THETA") * Math.PI / 180.0;     //rad 
            double specXs = ParseRecipe("SPEC_X");
            double specYs = ParseRecipe("SPEC_Y");
            double thetaF = thetaO - thetaS + offsetT * Math.PI / 180.0;

            _logger.Information($"[ANGLE] ThetaO = {thetaO:F6}rad ({thetaO * 180 / Math.PI:F4}deg)");
            _logger.Information($"[ANGLE] ThetaS = {thetaS:F6}rad ({thetaS * 180 / Math.PI:F4}deg)");
            _logger.Information($"[ANGLE] ThetaF = {thetaF:F6}rad ({thetaF * 180 / Math.PI:F4}deg)");
            _logger.Information($"[SPEC]  SpecXs = {specXs:F4}  SpecYs = {specYs:F4}");

            // ── hT 회전 ──────────────────────────────────────────
            await RelativeMotionsMove(MotionExtensions.H_T, -thetaF * 180.0 / Math.PI, ct);

            // ── Shift 연산 ───────────────────────────────────────
            Point2D hcroTopLTPrime = CalibrationMath.RotateAroundPivot(
                ctx.HcroTopLA, Point2D.Zero, thetaF);
            Point2D hcroTopRTPrime = CalibrationMath.RotateAroundPivot(
                ctx.HcroTopRA, Point2D.Zero, thetaF);

            _logger.Information($"[ROT]   HcroTopLA → Prime = ({hcroTopLTPrime.X:F4}, {hcroTopLTPrime.Y:F4})");
            _logger.Information($"[ROT]   HcroTopRA → Prime = ({hcroTopRTPrime.X:F4}, {hcroTopRTPrime.Y:F4})");

            // T-Die 중심 (회전 후)
            double topCenterX = (hcroTopLTPrime.X + hcroTopRTPrime.X) / 2.0;
            double topCenterY = (hcroTopLTPrime.Y + hcroTopRTPrime.Y) / 2.0;
            _logger.Information($"[TOP]   Center(after rotate) = ({topCenterX:F4}, {topCenterY:F4})");

            // T-Die - B-Die 차이
            _logger.Information($"[DIFF]  X = {topCenterX - btmCenterX:F4}");
            _logger.Information($"[DIFF]  Y = {topCenterY - btmCenterY:F4}");

            double shiftX = topCenterX - btmCenterX + offsetX;
            double shiftY = topCenterY - btmCenterY + offsetY;

            _logger.Information($"[SHIFT] X = {shiftX:F4}  Y = {shiftY:F4}");

            await Task.WhenAll(
                RelativeMotionsMove(MotionExtensions.H_X, -shiftX, ct),
                RelativeMotionsMove(MotionExtensions.W_Y, -shiftY, ct));

            //await RelativeMotionsMove(MotionExtensions.W_Y, shiftY, ct);

            ctx.FinalThetaO = thetaO;
            ctx.FinalThetaF = thetaF;
            ctx.FinalShiftX = shiftX;
            ctx.FinalShiftY = shiftY;
            ctx.OffsetXApplied = offsetX;
            ctx.OffsetYApplied = offsetY;
            ctx.OffsetTApplied = offsetT;

            //await Bonding(bondingDataPoints, ct);
            await Init_Head(ct);
            await Task.Delay(2000);
        }

        // ═══════════════════════════════════════════════════
        //  Full Sequence (개별 단계 조합)
        //  ★ ctx 는 반드시 새 인스턴스 생성
        // ═══════════════════════════════════════════════════

        //public async Task<AlignContext> TopRunFullSequence(
        //    int topDie,
        //    VisionMarkPositionResponse visionTopLowAlign,
        //    RecipeService recipeService,
        //    ObservableCollection<BondingDataPoint> bondingDataPoints,
        //    CancellationToken ct)
        //{
        //    var visionResult = await TopLowAlign(topDie, ct);
        //    await TopPickup(topDie, visionResult, ct);

        //    var ctx = await TopHighAlign(new AlignContext(), ct);
        //    ctx = await BtmHighAlign(ctx, ct);
        //    await TopPlace(ctx, recipeService, bondingDataPoints, ct);

        //    return ctx;
        //}

        // ═══════════════════════════════════════════════════
        //  private: 캘리브레이션 파라미터 로드
        // ═══════════════════════════════════════════════════

        private void LoadCalibrationInto(AlignContext ctx)
        {
            var pcTParam = _paramService.FindByName(MotionExtensions.PC_T);
            var hc1Param = _paramService.FindByName(MotionExtensions.HC1_T);
            var hc2Param = _paramService.FindByName(MotionExtensions.HC2_T);
            var hcroXParam = _paramService.FindByName(MotionExtensions.HCRO_X);
            var hcroYParam = _paramService.FindByName(MotionExtensions.HCRO_Y);
            var hc1XParam = _paramService.FindByName(MotionExtensions.HC1_X);
            var hc1YParam = _paramService.FindByName(MotionExtensions.HC1_Y);
            var hc2XParam = _paramService.FindByName(MotionExtensions.HC2_X);
            var hc2YParam = _paramService.FindByName(MotionExtensions.HC2_Y);

            ctx.HasHcRO = hcroXParam.Id != 0 && hcroYParam.Id != 0
                       && hc1Param.Id != 0 && hc2Param.Id != 0
                       && hc1XParam.Id != 0 && hc1YParam.Id != 0
                       && hc2XParam.Id != 0 && hc2YParam.Id != 0;
            ctx.HasPcT = pcTParam.Id != 0;

            if (ctx.HasHcRO)
            {
                ctx.Hc1Rad = ParseDouble(hc1Param.Value) * Math.PI / 180.0;
                ctx.Hc2Rad = ParseDouble(hc2Param.Value) * Math.PI / 180.0;
                ctx.Hcro = Point2D.of(ParseDouble(hcroXParam.Value), ParseDouble(hcroYParam.Value));
                ctx.Hc1Offset = Point2D.of(ParseDouble(hc1XParam.Value), ParseDouble(hc1YParam.Value));
                ctx.Hc2Offset = Point2D.of(ParseDouble(hc2XParam.Value), ParseDouble(hc2YParam.Value));
            }
            if (ctx.HasPcT)
                ctx.PcTRad = ParseDouble(pcTParam.Value) * Math.PI / 180.0;
        }

        private void LoadCalibrationInto(AlignData data)
        {
            var pcT = _paramService.FindByName(MotionExtensions.PC_T);
            var hc1T = _paramService.FindByName(MotionExtensions.HC1_T);
            var hc2T= _paramService.FindByName(MotionExtensions.HC2_T);
            var hcroXParam = _paramService.FindByName(MotionExtensions.HCRO_X);
            var hcroYParam = _paramService.FindByName(MotionExtensions.HCRO_Y);
            var hc2XParam = _paramService.FindByName(MotionExtensions.HC2_X);
            var hc2YParam = _paramService.FindByName(MotionExtensions.HC2_Y);

            data.OffsetXY = new Point2D(
                double.Parse(_recipeService.FindByParam("X_ALIGN_OFFSET").Value),
                double.Parse(_recipeService.FindByParam("Y_ALIGN_OFFSET").Value));
            data.OffsetT = double.Parse(_recipeService.FindByParam("T_ALIGN_OFFSET").Value);

            var HasHcRO = hcroXParam.Id != 0 && hcroYParam.Id != 0
                       && hc1T.Id != 0 && hc2T.Id != 0
                       && hc2XParam.Id != 0 && hc2YParam.Id != 0;

            if (HasHcRO)
            {
                data.Hc1Rad = CalibrationMath.ToRadian(ParseDouble(hc1T.Value));
                data.Hc2Rad = CalibrationMath.ToRadian(ParseDouble(hc2T.Value));
                data.PcTRad = CalibrationMath.ToRadian(ParseDouble(pcT.Value));
                data.Hcro = Point2D.of(ParseDouble(hcroXParam.Value), ParseDouble(hcroYParam.Value));
                data.Hc2Offset = Point2D.of(ParseDouble(hc2XParam.Value), ParseDouble(hc2YParam.Value));

                // 카메라 회전량 보정: DxCamToMark/DyCamToMark in-place 적용
                // Top(P-Cam) → PcTRad,  Btm Left(Hc1) → Hc1Rad,  Btm Right(Hc2) → Hc2Rad
                //ApplyCameraRotation(data.TopLeftFidRaw,    data.PcTRad);
                //ApplyCameraRotation(data.TopRightFidRaw,   data.PcTRad);
                //ApplyCameraRotation(data.TopLeftAlignRaw,  data.PcTRad);
                //ApplyCameraRotation(data.TopRightAlignRaw, data.PcTRad);

                //ApplyCameraRotation(data.BtmLeftFidRaw,    data.Hc1Rad);
                //ApplyCameraRotation(data.BtmLeftAlignRaw,  data.Hc1Rad);
                //ApplyCameraRotation(data.BtmRightFidRaw,   data.Hc2Rad);
                //ApplyCameraRotation(data.BtmRightAlignRaw, data.Hc2Rad);
            }
            else
            {
                throw new Exception("데이터를 찾을 수 없습니다");
            }
        }

        private static void ApplyCameraRotation(VisionMarkResult m, double rad)
        {
            if (m == null) return;
            var p = CalibrationMath.ApplyRotation(
                Point2D.of(m.DxCamToMark, m.DyCamToMark), rad);
            m.DxCamToMark = p.X;
            m.DyCamToMark = p.Y;
        }

        private void TCorr(VisionMarkResult vision)
        {

        }
        // ═══════════════════════════════════════════════════
        //  private: Top(Pc) 보정
        //   Raw → Clone → 회전 보정 → Corrected
        //   ★ Raw 읽기만, 쓰기는 Corrected 에만
        // ═══════════════════════════════════════════════════

        private static void ApplyTopPcCorrections(AlignContext ctx)
        {
            if (!ctx.HasPcT)
            {
                // 보정 없이 Raw 복사본을 Corrected 에 저장
                ctx.TopRightFidCorrected = ctx.TopRightFidRaw.Clone();
                ctx.TopRightAlignCorrected = ctx.TopRightAlignRaw.Clone();
                ctx.TopLeftFidCorrected = ctx.TopLeftFidRaw.Clone();
                ctx.TopLeftAlignCorrected = ctx.TopLeftAlignRaw.Clone();
                return;
            }

            // Clone → 회전 보정 → Corrected
            ctx.TopRightFidCorrected = ApplyRotationToCopy(ctx.TopRightFidRaw, ctx.PcTRad);
            ctx.TopRightAlignCorrected = ApplyRotationToCopy(ctx.TopRightAlignRaw, ctx.PcTRad);
            ctx.TopLeftFidCorrected = ApplyRotationToCopy(ctx.TopLeftFidRaw, ctx.PcTRad);
            ctx.TopLeftAlignCorrected = ApplyRotationToCopy(ctx.TopLeftAlignRaw, ctx.PcTRad);
        }

        // ═══════════════════════════════════════════════════
        //  private: Btm(Hc) 보정
        //   Raw → Clone → 기구 오프셋 → Hc 회전 → Corrected
        //   ★ Raw 읽기만, 쓰기는 Corrected 에만
        // ═══════════════════════════════════════════════════

        private static void ApplyBtmCorrections(AlignContext ctx)
        {
            // ── Raw → Clone (원본 보존) ──────────────────────
            var rightFid = ctx.BtmRightFidRaw.Clone();
            var rightAlign = ctx.BtmRightAlignRaw.Clone();
            var leftFid = ctx.BtmLeftFidRaw.Clone();
            var leftAlign = ctx.BtmLeftAlignRaw.Clone();

            // ── 기구 오프셋 적용 (Clone 에만) ────────────────
            rightFid.StageX += ctx.Hc2Offset.X; rightFid.StageY += ctx.Hc2Offset.Y;
            rightAlign.StageX += ctx.Hc2Offset.X; rightAlign.StageY += ctx.Hc2Offset.Y;
            leftFid.StageX += ctx.Hc1Offset.X; leftFid.StageY += ctx.Hc1Offset.Y;
            leftAlign.StageX += ctx.Hc1Offset.X; leftAlign.StageY += ctx.Hc1Offset.Y;

            // ── Hc 카메라 회전 보정 ──────────────────────────
            if (ctx.HasHcRO)
            {
                rightFid = ApplyRotationToCopy(rightFid, ctx.Hc2Rad);
                rightAlign = ApplyRotationToCopy(rightAlign, ctx.Hc2Rad);
                leftFid = ApplyRotationToCopy(leftFid, ctx.Hc1Rad);
                leftAlign = ApplyRotationToCopy(leftAlign, ctx.Hc1Rad);
            }

            // ── Corrected 저장 ───────────────────────────────
            ctx.BtmRightFidCorrected = rightFid;
            ctx.BtmRightAlignCorrected = rightAlign;
            ctx.BtmLeftFidCorrected = leftFid;
            ctx.BtmLeftAlignCorrected = leftAlign;
        }

        // ═══════════════════════════════════════════════════
        //  private: Top 오프셋 계산
        //   Corrected 읽기만 (수정 없음)
        // ═══════════════════════════════════════════════════

        private static void ComputeTopOffsets(AlignContext ctx)
        {
            var tRF = ctx.TopRightFidCorrected;
            var tRA = ctx.TopRightAlignCorrected;
            var tLF = ctx.TopLeftFidCorrected;
            var tLA = ctx.TopLeftAlignCorrected;

            double rXFA = tRF.CenterX - tRA.CenterX;
            double rYFA = tRF.CenterY - tRA.CenterY;
            double lXFA = tLF.CenterX - tLA.CenterX;
            double lYFA = tLF.CenterY - tLA.CenterY;

            ctx.TopOffsetX = (tRF.CenterX + tLF.CenterX) / 2.0
                           - (tRA.CenterX + tLA.CenterX) / 2.0;
            ctx.TopOffsetY = (tRF.CenterY + tLF.CenterY) / 2.0
                           - (tRA.CenterY + tLA.CenterY) / 2.0;
            ctx.TopOffsetT = CalcTheta(tLF, tRF) - CalcTheta(tLA, tRA);

            ctx.TopAlignRelOffsetX = -((rXFA + lXFA) / 2.0);
            ctx.TopAlignRelOffsetY = -((rYFA + lYFA) / 2.0);
            ctx.TopAlignRelOffsetT = ctx.TopOffsetT;
        }

        // ═══════════════════════════════════════════════════
        //  private: HcRO 좌표 변환
        //   Corrected 읽기만 (수정 없음)
        //
        //  변환 순서:
        //   ① Btm(Hc) → HcRO 좌표 (평행 이동)
        //   ② T-Die Fid = B-Die Fid (deep copy)
        //   ③ Pc Fid/Align 오프셋 X·Y 미러링
        //   ④ 비등방 배율 Sx·Sy 산출
        //   ⑤ 배율 먼저 → 회전 보정 (commutative 아님)
        //   ⑥ HcRO Fid 좌표에 더해 HcroTopLA·RA 산출
        // ═══════════════════════════════════════════════════

        private static void ComputeHcroCoords(AlignContext ctx)
        {
            // Corrected 결과 참조 (Raw 접근 금지)
            var bRF = ctx.BtmRightFidCorrected;
            var bRA = ctx.BtmRightAlignCorrected;
            var bLF = ctx.BtmLeftFidCorrected;
            var bLA = ctx.BtmLeftAlignCorrected;

            var tRF = ctx.TopRightFidCorrected;
            var tRA = ctx.TopRightAlignCorrected;
            var tLF = ctx.TopLeftFidCorrected;
            var tLA = ctx.TopLeftAlignCorrected;

            if (ctx.HasHcRO)
            {
                // ── ① B-Die (Hc) -> HcRO 좌표 변환 ──────────────
                ctx.HcroLF = Point2D.of(bLF.CenterX - ctx.Hcro.X, bLF.CenterY - ctx.Hcro.Y);
                ctx.HcroLA = Point2D.of(bLA.CenterX - ctx.Hcro.X, bLA.CenterY - ctx.Hcro.Y);
                ctx.HcroRF = Point2D.of(bRF.CenterX - ctx.Hcro.X, bRF.CenterY - ctx.Hcro.Y);
                ctx.HcroRA = Point2D.of(bRA.CenterX - ctx.Hcro.X, bRA.CenterY - ctx.Hcro.Y);

                // ── ② T-Die Fid = B-Die Fid (기준점 설정) ─────────
                ctx.HcroTopLF = Point2D.of(ctx.HcroLF.X, ctx.HcroLF.Y);
                ctx.HcroTopRF = Point2D.of(ctx.HcroRF.X, ctx.HcroRF.Y);

                // ── ③ Pc 공간 벡터 (미러링 핵심 수정) ─────────────────
                // P-Camera가 위를 보고 T-Die의 '하단'을 보므로, 
                // 설계상의 Right(오른쪽)가 비전상에서는 Left(왼쪽)로 보일 수 있습니다.

                double pcFidDx = (tRF.CenterX - tLF.CenterX) ; 
                double pcFidDy = (tRF.CenterY - tLF.CenterY) * 1.0;  // Y 유지 (혹은 장비 특성에 따라 -1)

                double hcroFidDx = ctx.HcroRF.X - ctx.HcroLF.X;
                double hcroFidDy = ctx.HcroRF.Y - ctx.HcroLF.Y;

                // ── ④ 스케일 및 변환 각도(Theta Plus) 계산 ─────────────
                double pcFidDist = Math.Sqrt(pcFidDx * pcFidDx + pcFidDy * pcFidDy);
                double hcroFidDist = Math.Sqrt(hcroFidDx * hcroFidDx + hcroFidDy * hcroFidDy);
                double scale = (pcFidDist > 1e-6) ? hcroFidDist / pcFidDist : 1.0;
                //double scale = 1;
                // ThetaPlus: Pc 벡터를 HcRO 벡터 방향으로 일치시키기 위한 회전량
                double thetaPlus = Math.Atan2(hcroFidDy, hcroFidDx) - Math.Atan2(pcFidDy, pcFidDx);

                // ── ⑤ T-Die Align -> HcRO 좌표 변환 ─────────────
                // Fid에서 Align Mark로 가는 '상대 벡터'를 구한 뒤, 
                // 미러링 -> 스케일 -> 회전(ThetaPlus) 순으로 적용합니다.

                // [Left Align 변환]
                double lVecX = (tLA.CenterX - tLF.CenterX) * 1.0; 
                double lVecY = (tLA.CenterY - tLF.CenterY) * 1.0;  // Y 유지
                var rotL = CalibrationMath.ApplyRotation(Point2D.of(lVecX * scale, lVecY * scale), thetaPlus);
                ctx.HcroTopLA = Point2D.of(ctx.HcroLF.X + rotL.X, ctx.HcroLF.Y + rotL.Y);

                // [Right Align 변환]
                double rVecX = (tRA.CenterX - tRF.CenterX) * 1.0; 
                double rVecY = (tRA.CenterY - tRF.CenterY) * 1.0;  // Y 유지
                var rotR = CalibrationMath.ApplyRotation(Point2D.of(rVecX * scale, rVecY * scale), thetaPlus);
                ctx.HcroTopRA = Point2D.of(ctx.HcroRF.X + rotR.X, ctx.HcroRF.Y + rotR.Y);

                // 진단값 기록
                ctx.PcHcroScaleX = scale;
                ctx.PcHcroScaleY = scale;
                ctx.PcHcroThetaPlus = thetaPlus;
            }
            else
            {
                // HcRO 캘리브레이션 없음 → 절대좌표 그대로
                ctx.HcroLF = Point2D.of(bLF.CenterX, bLF.CenterY);
                ctx.HcroLA = Point2D.of(bLA.CenterX, bLA.CenterY);
                ctx.HcroRF = Point2D.of(bRF.CenterX, bRF.CenterY);
                ctx.HcroRA = Point2D.of(bRA.CenterX, bRA.CenterY);

                ctx.HcroTopLF = Point2D.of(bLF.CenterX, bLF.CenterY);
                ctx.HcroTopRF = Point2D.of(bRF.CenterX, bRF.CenterY);
                ctx.HcroTopLA = Point2D.of(bLA.CenterX, bLA.CenterY);
                ctx.HcroTopRA = Point2D.of(bRA.CenterX, bRA.CenterY);

                ctx.PcHcroScaleX = 1.0;
                ctx.PcHcroScaleY = 1.0;
                ctx.PcHcroThetaPlus = 0.0;
                ctx.ScaleFallbackApplied = false;
            }
        }
        // ═══════════════════════════════════════════════════
        //  private: Btm 오프셋 계산
        //   Corrected 읽기만 (수정 없음)
        // ═══════════════════════════════════════════════════

        private static void ComputeBtmOffsets(AlignContext ctx)
        {
            var bRF = ctx.BtmRightFidCorrected;
            var bRA = ctx.BtmRightAlignCorrected;
            var bLF = ctx.BtmLeftFidCorrected;
            var bLA = ctx.BtmLeftAlignCorrected;

            double btmRXFA = bRF.CenterX - bRA.CenterX;
            double btmRYFA = bRF.CenterY - bRA.CenterY;
            double btmLXFA = bLF.CenterX - bLA.CenterX;
            double btmLYFA = bLF.CenterY - bLA.CenterY;

            double topInBtmX = -ctx.TopAlignRelOffsetX;
            double topInBtmY = ctx.TopAlignRelOffsetY;
            double topInBtmT = -ctx.TopAlignRelOffsetT;

            ctx.BtmOffsetX = topInBtmX - (btmRXFA + btmLXFA) / 2.0;
            ctx.BtmOffsetY = topInBtmY - (btmRYFA + btmLYFA) / 2.0;
            ctx.BtmOffsetT = topInBtmT - (CalcTheta(bLF, bRF) - CalcTheta(bLA, bRA));
        }

        

        // ═══════════════════════════════════════════════════
        //  핵심 유틸: Non-destructive 회전 보정
        //   ★ 기존 Rotate4Points 를 완전 대체
        //   ★ 원본 Clone → DxCamToMark/DyCamToMark 회전 → 새 인스턴스 반환
        //   ★ 원본 객체는 절대 수정하지 않음
        // ═══════════════════════════════════════════════════

        private static VisionMarkResult ApplyRotationToCopy(VisionMarkResult m, double rad)
        {
            var copy = m.Clone();
            var p = CalibrationMath.ApplyRotation(
                Point2D.of(m.DxCamToMark, m.DyCamToMark), rad);
            copy.DxCamToMark = p.X;
            copy.DyCamToMark = p.Y;
            return copy;
        }

        // ★ 기존 Rotate4Points 삭제됨 — in-place 수정으로 누적 오차 원인이었음
        // private static void Rotate4Points(...) { ... }

        // ═══════════════════════════════════════════════════
        //  기타 유틸
        // ═══════════════════════════════════════════════════

        private double ParseDouble(string s)
        {
            s = s.Replace('\u2212', '-')   // minus sign
                 .Replace('\u2013', '-')   // en-dash
                 .Replace('\u00A0', ' ')   // non-breaking space
                 .Trim();
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        private double ParseRecipe(string paramName)
        {
            var p = _recipeService.FindByParam(paramName);
            return p != null ? double.Parse(p.Value) : 0.0;
        }
    }
}