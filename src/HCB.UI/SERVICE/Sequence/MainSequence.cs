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
using System.Security.Cryptography;
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
                await Task.WhenAll(
                    Init_Head(ct),
                    MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct)
                );
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
            try
            {
                await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
                data.TopRightFidRaw = await TopDieVisionRightFid(data.AvgMove, ct);
                await PTable2DMappingOn();
                data.TopRightAlignRaw = await TopDieVisionRightAlign(data.AvgMove, ct);
                data.TopLeftFidRaw = await TopDieVisionLeftFid(data.AvgMove, ct);
                data.TopLeftAlignRaw = await TopDieVisionLeftAlign(data.AvgMove, ct);
            }catch(Exception e)
            {
                throw new Exception(e.Message);
            }finally
            {
                await PTable2DMappingOff();
            }

            return data;
        }

        #endregion

        #region Btm Die 고배율 측정

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
            CompensateHc2Offset(data);

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
        }

        #endregion

        public async Task PTable2DMappingOn()
        {
            var pmac = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            
            await pmac.SendCommand("CompTable[0].sf[0]=1");
            await pmac.SendCommand("CompTable[1].sf[0]=1");
            await pmac.SendCommand("CompTable[2].sf[0]=0");
            await pmac.SendCommand("CompTable[3].sf[0]=0");
            await pmac.SendCommand("sys.Compenable=4");

        }

        public async Task PTable2DMappingOff()
        {
            var pmac = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            await pmac.SendCommand("sys.Compenable=0");
        }

        // ═══════════════════════════════════════════════════
        //  private: 캘리브레이션 파라미터 로드
        // ═══════════════════════════════════════════════════


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
            }
            else
            {
                throw new Exception("데이터를 찾을 수 없습니다");
            }
        }

        /// <summary>
        /// 피듀셜 마크 변화량으로 Hc2Offset을 보정한다.
        ///   delta = −(dLF − dRF)
        ///   dLF = 현재 Hc1 DxCam − 기준 Hc1 DxCam
        ///   dRF = 현재 Hc2 DxCam − 기준 Hc2 DxCam
        ///   보정 Hc2Offset = 기준 Hc2Offset + delta
        /// </summary>

        private void CompensateHc2Offset(AlignData d)
        {
            if (d?.BtmLeftFidRaw == null || d.BtmRightFidRaw == null
                || d.Hc2Offset == null || d.Hcro == null)
                return;

            // 피듀셜 기준값만 DB에서 조회
            var refLfDxParam = _paramService.FindByName("Hc1FidRefDx");
            var refLfDyParam = _paramService.FindByName("Hc1FidRefDy");
            var refRfDxParam = _paramService.FindByName("Hc2FidRefDx");
            var refRfDyParam = _paramService.FindByName("Hc2FidRefDy");

            if (string.IsNullOrEmpty(refLfDxParam?.Value) || string.IsNullOrEmpty(refRfDxParam?.Value))
            {
                _logger.Warning("피듀셜 기준값 미설정 — 보정 스킵");
                return;
            }

            if (!double.TryParse(refLfDxParam.Value, out double refLfDx) ||
                !double.TryParse(refLfDyParam.Value, out double refLfDy) ||
                !double.TryParse(refRfDxParam.Value, out double refRfDx) ||
                !double.TryParse(refRfDyParam.Value, out double refRfDy))
            {
                _logger.Warning("피듀셜 기준값 파싱 실패 — 보정 스킵");
                return;
            }

            double dLfX = d.BtmLeftFidRaw.DxCamToMark - refLfDx;
            double dLfY = d.BtmLeftFidRaw.DyCamToMark - refLfDy;
            double dRfX = d.BtmRightFidRaw.DxCamToMark - refRfDx;
            double dRfY = d.BtmRightFidRaw.DyCamToMark - refRfDy;

            // Hc2Offset 보정: -(dLf - dRf)
            double hc2DeltaX = -(dLfX - dRfX);
            double hc2DeltaY = -(dLfY - dRfY);
            d.Hc2Offset = new Point2D(d.Hc2Offset.X + hc2DeltaX, d.Hc2Offset.Y + hc2DeltaY);

            // HcRO 보정: -dLf (Hc1 원점 드리프트)
            double hcroDeltaX = -dLfX;
            double hcroDeltaY = -dLfY;
            d.Hcro = new Point2D(d.Hcro.X + hcroDeltaX, d.Hcro.Y + hcroDeltaY);

            _logger.Information(
                "피듀셜 트래킹 보정\n" +
                "  Hc2Offset Δ({Hc2Dx:F5}, {Hc2Dy:F5}) → ({Hc2X:F6}, {Hc2Y:F6})\n" +
                "  HcRO      Δ({HcroDx:F5}, {HcroDy:F5}) → ({HcroX:F6}, {HcroY:F6})",
                hc2DeltaX, hc2DeltaY, d.Hc2Offset.X, d.Hc2Offset.Y,
                hcroDeltaX, hcroDeltaY, d.Hcro.X, d.Hcro.Y);
        }

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