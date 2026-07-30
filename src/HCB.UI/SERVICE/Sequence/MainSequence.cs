using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telerik.Windows.Persistence.Core;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        // ═══════════════════════════════════════════════════
        //  Public Sequence Entry Points
        // ═══════════════════════════════════════════════════
        public async Task WTableLoading(CancellationToken ct)
        {
            try
            {
                _logger.Information("Wafer Loading Start");
                //EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                string[] motions = { MotionExtensions.W_Y };
                await MotionsMove(motions, MotionExtensions.WAFER_LOADING, ct);
                await Task.Delay(3000, ct);

                // Vacuum Off
                await _sequenceHelper.WTableVacuumAll(eOnOff.Off, ct);

                // Wafer Pin UP
                await _sequenceHelper.WTableLiftPin(eUpDown.Up, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Wafer Loading Canceled");
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                return;
            }
            finally
            {
                _logger.Information("Wafer Loading End");
            }
        }

        public async Task DTablePickup(DieType dieType, int vacNum, VisionMarkPositionResponse? correction, CancellationToken ct)
        {
            string label = dieType == DieType.TOP ? "TOP" : "BTM";
            try
            {
                _logger.Information("{Label} Die pickup Start", label);
                EQStatusCheck();

                var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                // ── 공통 EC 파라미터 ──
                double xOffset = _paramService.GetDouble("ShankLowOffsetX");
                double yOffset = _paramService.GetDouble("ShankLowOffsetY");

                double xLowErrorOffset = await GetRecipe("xLowErrorOffset");
                double yLowErrorOffset = await GetRecipe("yLowErrorOffset");

                double shankToDieOffset = _paramService.GetDouble("ShankToDieOffset");

                // ── Die 타입별 레시피 ──
                string thicknessKey = dieType == DieType.TOP ? "TopDieThickness" : "BtmDieThickness";
                double dieThickness = await GetRecipe(thicknessKey);

                // ── Bonding Step ──
                var step = _recipeService.FindStepByName("PICK UP");

                double readyPosition = await GetRecipe("READY_POSITION");

                // ── 1. Head 안전 위치 이동 ──
                await Init_Head(ct);

                // ── 2. 픽업 위치 이동 + 보정 ──
                double corrX = correction?.X ?? 0;
                double corrY = correction?.Y ?? 0;
                double corrT = correction?.Theta ?? 0;

                await Task.WhenAll(
                    RelativeMotionsMove(MotionExtensions.H_X, xOffset - corrX + xLowErrorOffset, ct),
                    RelativeMotionsMove(MotionExtensions.D_Y,  yOffset - corrY + yLowErrorOffset, ct),
                    MotionsMove(MotionExtensions.H_T, -corrT, ct)
                );

                // ── 3. Z축 하강 ──
                await MotionsMove(MotionExtensions.H_Z, shankToDieOffset - dieThickness - readyPosition, ct);

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
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={step.AccTime}");
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME2 + $"={step.AccTime2}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={step.ContTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={step.DecTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={step.LoadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={step.Current}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT2 + $"={step.Current2}");
                await device.SendCommand(MotionExtensions.BONDING_START + "=1");
                
                const int pollingIntervalMs = 100;
                int timeoutMs = step.AccTime + step.AccTime2 + step.ContTime + step.DecTime + 2000;
                var sw = Stopwatch.StartNew();
                bool pressComplete = false;
                bool headVacOn = false;
                bool dtableVacOff = false;
                _logger.Information("{Label} PICKUP Step={StepName}: ACC={Acc}, ACC2={Acc2}, CONT={Cont}, DEC={Dec}, LOADCELL={Load}, CURRENT={Cur}, CURRENT2={Cur2}",
                    label, step.Name, step.AccTime, step.AccTime2, step.ContTime, step.DecTime, step.LoadCell, step.Current, step.Current2);
                while (!pressComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    long elapsed = sw.ElapsedMilliseconds;
                    var loopSw = Stopwatch.StartNew();

                    // Head 진공 ON 시점 + 픽업 센서 확인
                    if (!headVacOn && elapsed >= step.VacOffTime)
                    {
                        var picked = await _sequenceHelper.HeadPickerVacuum(eOnOff.On, ct);
                        headVacOn = true;
                        _logger.Information("{Label} Head Vacuum ON ({Elapsed}ms, 설정={SetMs}ms)",
                            label, elapsed, step.VacOffTime);
                        if (!picked) throw new Exception("Head에 Pick된 Die가 없습니다");
                    }

                    // DTable 진공 OFF 시점
                    if (!dtableVacOff && elapsed >= step.VacOffTime)
                    {
                        await SwitchDTableVacuum(dieType, vacNum, eOnOff.Off, ct);
                        dtableVacOff = true;
                        _logger.Information("{Label} DTable Vacuum OFF ({Elapsed}ms, 설정={SetMs}ms)",
                            label, elapsed, step.VacOffTime);
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

                await Task.Delay(300);
                await Init_Head(ct);
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
            catch (ErrorException ex)
            {
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
                double shankToWaferOffset = _paramService.GetDouble("ShankToWaferOffset");

                // ── Bonding Step ──
                var step = _recipeService.FindStepByName("BTM PRESS");
                double readyPosition = await GetRecipe("READY_POSITION");

                // ── 1. 이동 ──
                await Init_Head(ct);
                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_X, "PLACE_CENTER", ct),
                    MotionsMove(MotionExtensions.W_Y, "PLACE_CENTER", ct),
                    MotionsMove(MotionExtensions.H_T, 0, ct)
                );

                // ── 2. Z축 하강 ──
                await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - btmDieThickness - readyPosition, ct);
                await Task.Delay(200, ct);

                // ── 3. 가압 시퀀스 ──
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={step.AccTime}");
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME2 + $"={step.AccTime2}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={step.ContTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={step.DecTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={step.LoadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={step.Current}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT2 + $"={step.Current2}");
                await device.SendCommand(MotionExtensions.BONDING_START + $"=1");

                const int pollingIntervalMs = 100;
                int timeoutMs = step.AccTime + step.AccTime2 + step.ContTime + step.DecTime + 2000;
                var sw = Stopwatch.StartNew();
                bool pressComplete = false;
                bool wtableVacOn = false;
                bool headVacOff = false;

                while (!pressComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    long elapsed = sw.ElapsedMilliseconds;

                    // WTable 진공 ON 시점 (받는 쪽 먼저 흡착)
                    if (!wtableVacOn && elapsed >= step.VacOffTime)
                    {
                        await _sequenceHelper.WTableVacuum(vacNum, eOnOff.On, ct);
                        wtableVacOn = true;
                        _logger.Information("WTable Vacuum ON ({Elapsed}ms, 설정={SetMs}ms)",
                            elapsed, step.VacOffTime);
                    }

                    // Head 진공 OFF 시점 (놓는 쪽 해제)
                    if (!headVacOff && elapsed >= step.VacOffTime)
                    {
                        var released = await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, ct);
                        headVacOff = true;
                        _logger.Information("Head Vacuum OFF ({Elapsed}ms, 설정={SetMs}ms)",
                            elapsed, step.VacOffTime);
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

                    if (values.Length > 0 && int.TryParse(values[0].Trim(), out int statusCode))
                    {
                        pressComplete = statusCode == 6;
                        _logger.Information("{Label} Drop press 상태: {Code} (complete={Complete}) | Force: {Force:F3}N (경과: {Elapsed}ms)",
                             statusCode, pressComplete, forceValue * 0.00373, sw.ElapsedMilliseconds);
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
                    // ── 4. 복귀 ──
                    await Init_Head(ct);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Drop press 초기화 실패");
                }
            }
        }


        #region 좌표계 통합
        public async Task CoordinateSystemIntegration(AlignData data, CancellationToken ct)
        {
            try
            {
                if (data == null) throw new ArgumentNullException(nameof(data));

                ApplyMeasurementCorrections(data);

                switch (data.TracingMode)
                {
                    case TracingMode.Auto:
                        CompensateHc2Offset(data);
                        break;
                    //case TracingMode.Manual:
                    //    await CameraDist(data, ct);
                    //    var (hc1Raw, hc2Raw) = await MeasureHcroPoints(data, ct);
                    //    ComputeHcroCenter(data, hc1Raw, hc2Raw);
                    //    break;
                    case TracingMode.None:
                        break;
                }

                ComputeHc2OffsetDependent(data);

                // ── STEP 0: 측정2→3 Theta 변화량을 Top 마크에 반영 ──
                Point2D topLF, topRF, topLA, topRA;

                if (data.M2FidTheta != 0 && data.M3FidTheta != 0)
                {
                    double deltaTheta = -(data.M3FidTheta - data.M2FidTheta);
                    double deltaThetaRad = CalibrationMath.ToRadian(deltaTheta);

                    Point2D fidCenter = Point2D.of(data.TopLeftFidRaw.CenterX, data.TopLeftFidRaw.CenterY);

                    topLF = CalibrationMath.RotateAroundPivot(
                        Point2D.of(data.TopLeftFidRaw.CenterX, data.TopLeftFidRaw.CenterY), fidCenter, deltaThetaRad);
                    topRF = CalibrationMath.RotateAroundPivot(
                        Point2D.of(data.TopRightFidRaw.CenterX, data.TopRightFidRaw.CenterY), fidCenter, deltaThetaRad);
                    topLA = CalibrationMath.RotateAroundPivot(
                        Point2D.of(data.TopLeftAlignRaw.CenterX, data.TopLeftAlignRaw.CenterY), fidCenter, deltaThetaRad);
                    topRA = CalibrationMath.RotateAroundPivot(
                        Point2D.of(data.TopRightAlignRaw.CenterX, data.TopRightAlignRaw.CenterY), fidCenter, deltaThetaRad);

                    _logger.Information(
                        "Theta 보정 — M2={M2:F4}° M3={M3:F4}° Δ={Delta:F4}°",
                        data.M2FidTheta, data.M3FidTheta, deltaTheta);
                }
                else
                {
                    topLF = Point2D.of(data.TopLeftFidRaw.CenterX, data.TopLeftFidRaw.CenterY);
                    topRF = Point2D.of(data.TopRightFidRaw.CenterX, data.TopRightFidRaw.CenterY);
                    topLA = Point2D.of(data.TopLeftAlignRaw.CenterX, data.TopLeftAlignRaw.CenterY);
                    topRA = Point2D.of(data.TopRightAlignRaw.CenterX, data.TopRightAlignRaw.CenterY);

                    _logger.Information("Theta 보정 스킵 — M2={M2:F4}° M3={M3:F4}°",
                        data.M2FidTheta, data.M3FidTheta);
                }

                // ── STEP 1: Top Die — Fid→Align 이동량 ──
                var lDist = Point2D.of(topLA.X - topLF.X, topLA.Y - topLF.Y);
                var rDist = Point2D.of(topRA.X - topRF.X, topRA.Y - topRF.Y);
                data.LDist = lDist;
                data.RDist = rDist;

                // ── STEP 2: Btm Die 좌표 통합 + Top 위치 생성 ──
                // Btm: Stage 기준 X:-, Y:- → DxCam 부호 반전
                Point2D camOffset = data.Hc2Offset;
                
                // Hc1X: 0.00361, Hc1Y: -0.00112, Hc2X: 0.00807, Hc2Y: -0.00269
                // X: +, Y: -   1.7 um
                // X: +, Y: +   0.2 um
                Point2D topFidRel = Point2D.of(topRF.X - topLF.X, topRF.Y - topLF.Y);
                Point2D bfl = Point2D.of(
                    -data.BtmLeftFidRaw.X,
                    -data.BtmLeftFidRaw.Y);

                //Point2D bfr = Point2D.of(
                //    camOffset.X - data.BtmRightFidRaw.X,
                //    camOffset.Y - data.BtmRightFidRaw.Y);

                Point2D bfr = Point2D.of(bfl.X - topFidRel.X, bfl.Y - topFidRel.Y);
                Point2D topRel = Point2D.of(topRA.X - topLA.X, topRA.Y - topLA.Y);
                Point2D tl = Point2D.of(bfl.X - lDist.X, bfl.Y - lDist.Y);
                Point2D tr = Point2D.of(tl.X - topRel.X, tl.Y - topRel.Y);
                //Point2D tr = Point2D.of(bfr.X - rDist.X, bfr.Y - rDist.Y);

                //var blDist = Point2D.of(data.BtmLeftAlignRaw.X - data.BtmLeftFidRaw.X, data.BtmLeftAlignRaw.Y - data.BtmLeftFidRaw.Y);
                //var brDist = Point2D.of(data.BtmRightAlignRaw.X - data.BtmRightFidRaw.X, data.BtmRightAlignRaw.Y - data.BtmRightFidRaw.Y);

                //Point2D bl = Point2D.of(
                //    bfl.X - blDist.X,
                //    bfl.Y - blDist.Y);
                //Point2D br = Point2D.of(
                //    bfr.X - brDist.X,
                //    bfr.Y - brDist.Y);
                Point2D bl = Point2D.of(
                    -data.BtmLeftAlignRaw.X,
                    -data.BtmLeftAlignRaw.Y);
                Point2D br = Point2D.of(
                    camOffset.X - data.BtmRightAlignRaw.X,
                    camOffset.Y - data.BtmRightAlignRaw.Y);

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

                data.BFL = bfl;
                data.BFR = bfr;
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

            }catch(Exception e)
            {
                throw;
            }
           
        }


        // Pc 좌표계 
        public async Task PcCoordinateSystemIntegration(AlignData data, CancellationToken ct)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            LoadCalibrationInto(data);;

            // ── STEP 1: Btm Die — Fid→Align 이동량 ──
            //var lDist = Point2D.of(
            //    data.BtmLeftAlignRaw.CenterX - data.BtmLeftFidRaw.CenterX,
            //    data.BtmLeftAlignRaw.CenterY - data.BtmLeftFidRaw.CenterY);
            //var rDist = Point2D.of(
            //    data.BtmRightAlignRaw.CenterX - data.BtmRightFidRaw.CenterX,
            //    data.BtmRightAlignRaw.CenterY - data.BtmRightFidRaw.CenterY);
            var lDist = Point2D.of(
                data.BtmLeftFidRaw.X - data.BtmLeftAlignRaw.X,
                data.BtmLeftFidRaw.Y - data.BtmLeftAlignRaw.Y
                );
            var rDist = Point2D.of(
                data.BtmRightFidRaw.X - data.BtmRightAlignRaw.X,
                data.BtmRightFidRaw.Y - data.BtmRightAlignRaw.Y
                );

            data.LDist = lDist;
            data.RDist = rDist;

            Point2D tl = Point2D.of(data.TopLeftAlignRaw.CenterX, data.TopLeftAlignRaw.CenterY);
            Point2D tr = Point2D.of(data.TopRightAlignRaw.CenterX, data.TopLeftAlignRaw.CenterY);

            Point2D bl = Point2D.of(data.TopLeftFidRaw.CenterX - lDist.X, data.TopLeftFidRaw.CenterY - lDist.Y);
            Point2D br = Point2D.of(data.TopRightFidRaw.CenterX - rDist.X, data.TopRightFidRaw.CenterY - rDist.Y);

            // ── STEP 3: 회전중심(HCRO) 기준으로 좌표 이동 ──
            Point2D hcro = data.PcHcro;
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

        #region 피듀셜 각도 추적
        // Hc1X: 0.00361, Hc1Y: -0.00112, Hc2X: 0.00807, Hc2Y: -0.00269
        public async Task<FiducialAngleResult> FiducialAngleTracking(bool avgMode, CancellationToken ct)
        {
            var result = new FiducialAngleResult();

            try
            {
                _logger.Information("피듀셜 각도 추적 시작");
                double hc1FidOffsetX = _recipeService.FindByParamDouble("HC1 피듀셜 위치 보정 X");
                double hc1FidOffsetY = _recipeService.FindByParamDouble("HC1 피듀셜 위치 보정 Y");
                double hc2FidOffsetX = _recipeService.FindByParamDouble("HC2 피듀셜 위치 보정 X");
                double hc2FidOffsetY = _recipeService.FindByParamDouble("HC2 피듀셜 위치 보정 Y");

                // ── 1. PC TABLE: TopDIE Fiducial 촬상 ──
                await PTable2DMappingOn();

                var topRightFid = await TopDieVisionRightFid(avgMode, ct);
                var topLeftFid = await TopDieVisionLeftFid(avgMode, ct);

                result.PcLeftFid = Point2D.of(topLeftFid.CenterX + hc1FidOffsetX, topLeftFid.CenterY + hc1FidOffsetY);
                result.PcRightFid = Point2D.of(topRightFid.CenterX + hc2FidOffsetX, topRightFid.CenterY + hc2FidOffsetY);
                result.PcAngleDeg = CalibrationMath.ToDegree(
                    Math.Atan2(
                        result.PcRightFid.Y - result.PcLeftFid.Y,
                        result.PcRightFid.X - result.PcLeftFid.X));

                await MappingOff();
                _logger.Information(
                    "PC Table 피듀셜 각도: {Angle:F6}°, LF=({LX:F4},{LY:F4}), RF=({RX:F4},{RY:F4})",
                    result.PcAngleDeg,
                    result.PcLeftFid.X, result.PcLeftFid.Y,
                    result.PcRightFid.X, result.PcRightFid.Y);

                // ── 2. Hc1/Hc2: Bonding 위치 Fiducial 촬상 ──
                var hc2XParam = _paramService.FindByName(MotionExtensions.HC2_X);
                var hc2YParam = _paramService.FindByName(MotionExtensions.HC2_Y);
                Point2D camOffset = Point2D.of(
                    ParseDouble(hc2XParam.Value),
                    ParseDouble(hc2YParam.Value));

                await WTable2DMappingOn();
                await TopDieSet(ct);

                var hcLeftFid = await BtmDieVisionLeftFid(avgMode, ct);
                var hcRightFid = await BtmDieVisionRightFid(avgMode, ct);

                result.HcLeftFid = Point2D.of(
                    -hcLeftFid.DxCamToMark,
                    -hcLeftFid.DyCamToMark);
                result.HcRightFid = Point2D.of(
                    camOffset.X - hcRightFid.DxCamToMark,
                    camOffset.Y - hcRightFid.DyCamToMark);
                result.HcAngleDeg = 180 + CalibrationMath.ToDegree(
                    Math.Atan2(
                        result.HcRightFid.Y - result.HcLeftFid.Y,
                        result.HcRightFid.X - result.HcLeftFid.X));

                _logger.Information(
                    "Hc 피듀셜 각도: {Angle:F6}°, LF=({LX:F4},{LY:F4}), RF=({RX:F4},{RY:F4})",
                    result.HcAngleDeg,
                    result.HcLeftFid.X, result.HcLeftFid.Y,
                    result.HcRightFid.X, result.HcRightFid.Y);

                // ── 3. Wafer Table: Fiducial 촬상 ──
                await Init_Head(ct);
                

                double shankToWaferOffset = _paramService.GetDouble("ShankToWaferOffset");
                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                await MotionsMove(MotionExtensions.H_Z,
                    shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);

                var wLeftFid = await BtmDieVisionLeftFid(avgMode, ct);
                var wRightFid = await BtmDieVisionRightFid(avgMode, ct);

                result.WaferLeftFid = Point2D.of(
                    -wLeftFid.DxCamToMark,
                    -wLeftFid.DyCamToMark);
                result.WaferRightFid = Point2D.of(
                    camOffset.X - wRightFid.DxCamToMark,
                    camOffset.Y - wRightFid.DyCamToMark);
                result.WaferAngleDeg = 180 + CalibrationMath.ToDegree(
                    Math.Atan2(
                        result.WaferRightFid.Y - result.WaferLeftFid.Y,
                        result.WaferRightFid.X - result.WaferLeftFid.X));

                await MappingOff();
                await Init_Head(ct);

                _logger.Information(
                    "Wafer Table 피듀셜 각도: {Angle:F6}°, LF=({LX:F4},{LY:F4}), RF=({RX:F4},{RY:F4})",
                    result.WaferAngleDeg,
                    result.WaferLeftFid.X, result.WaferLeftFid.Y,
                    result.WaferRightFid.X, result.WaferRightFid.Y);

                _logger.Information(
                    "피듀셜 각도 추적 완료 — PC={PcAngle:F6}°, Hc={HcAngle:F6}°, Wafer={WaferAngle:F6}°",
                    result.PcAngleDeg, result.HcAngleDeg, result.WaferAngleDeg);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Error(e, "피듀셜 각도 추적 실패");
                throw;
            }

            return result;
        }

        #endregion

        public async Task DTableReady(CancellationToken ct)
        {
            string DtReady = "D_READY";
            try
            {
                var status = _operationService.Status;
                if (status.Availability == Availability.Down || status.Run == RunStop.Run || status.Operation == OperationMode.Auto || status.Alarm == AlarmState.HEAVY)
                {
                    _logger.Warning("Cannot execute DTableLoading: Sequence Service is not in Manual Standby Status.");
                    return;
                }

                _logger.Information("Die Ready Start");

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                var d_y = motionDevice?.FindMotionByName(MotionExtensions.D_Y); // D Table Y축 (예시)
                var H_X = motionDevice?.FindMotionByName(MotionExtensions.H_X); // H Table X축 (예시)
                var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // H Table Z축 (예시)

                if (d_y == null || H_X == null || H_Z == null)
                {
                    string errorMsg = "";
                    if (d_y == null) errorMsg += "[D_Y] ";
                    if (H_X == null) errorMsg += "[H_X] ";
                    if (H_Z == null) errorMsg += "[H_Z] ";
                    throw new Exception(errorMsg + "축을 찾을 수 없습니다");
                }

                await _sequenceHelper.MoveAsync(d_y.MotorNo, DtReady, ct);
                await _sequenceHelper.MoveAsync(H_X.MotorNo, DtReady, ct);
                await _sequenceHelper.MoveAsync(H_Z.MotorNo, DtReady, ct);

                await Task.Delay(3000, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Die Ready Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                return;
            }
            finally
            {
                _logger.Information("Die Ready End");
            }
        }

        public async Task PTable2DMappingOn()
        {
            var pmac = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            
            await pmac.SendCommand("CompTable[0].sf[0]=1");
            await pmac.SendCommand("CompTable[1].sf[0]=1");
            await pmac.SendCommand("CompTable[2].sf[0]=0");
            await pmac.SendCommand("CompTable[3].sf[0]=0");
            await pmac.SendCommand("sys.Compenable=2");
        }


        public async Task WTable2DMappingOn()
        {
            var pmac = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

            await pmac.SendCommand("CompTable[0].sf[0]=0");
            await pmac.SendCommand("CompTable[1].sf[0]=0");
            await pmac.SendCommand("CompTable[2].sf[0]=1");
            await pmac.SendCommand("CompTable[3].sf[0]=1");
            await pmac.SendCommand("sys.Compenable=4");

        }

        public async Task MappingOff()
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

            var pcHcroXParam = _paramService.FindByName(MotionExtensions.HCRO_PC_X);
            var pcHcroYParam = _paramService.FindByName(MotionExtensions.HCRO_PC_Y);
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
                data.Hc1Rad = ParseDouble(hc1T.Value);
                data.Hc2Rad = ParseDouble(hc2T.Value);
                data.PcTRad = ParseDouble(pcT.Value);
                data.Hcro = Point2D.of(ParseDouble(hcroXParam.Value), ParseDouble(hcroYParam.Value));
                data.PcHcro = Point2D.of(ParseDouble(pcHcroXParam.Value), ParseDouble(pcHcroYParam.Value));
                data.Hc2Offset = Point2D.of(ParseDouble(hc2XParam.Value), ParseDouble(hc2YParam.Value));
            }
            else
            {
                throw new Exception("데이터를 찾을 수 없습니다");
            }
        }

        /// <summary>
        /// TopHighAlign/BtmHighAlign에서 raw로만 수집한 측정값에 대해 계산(보정)을 일괄 수행한다.
        ///   1) Top Align 좌표 → Fid Z 평면 투영 (PC 계수 × ΔZ 를 DxCam/DyCam에 가산)
        ///   2) Btm Fid 좌표  → Align Z 평면 투영 (HC 계수 × ΔZ 를 BtmFidRaw에 가산)
        ///   3) (Manual) HcRO 회전중심 계산 (CameraDist로 확정된 Hc2Offset + raw 회전점 + tilt)
        /// tilt 계수는 레시피에서 읽고, ΔZ는 측정 단계에서 기록한 값을 사용한다.
        /// </summary>
        private void ApplyMeasurementCorrections(AlignData data)
        {
            if (data == null) return;

            // 1) Top: Align → Fid Z 평면 투영
            double PC_L_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.PC_L_HZ_TILT_X);
            double PC_L_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.PC_L_HZ_TILT_Y);
            double PC_R_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.PC_R_HZ_TILT_X);
            double PC_R_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.PC_R_HZ_TILT_Y);

            if (data.TopRightAlignRaw != null)
            {
                data.TopRightAlignRaw.DxCamToMark += PC_R_HZ_TILT_X * data.TopRightDz;
                data.TopRightAlignRaw.DyCamToMark += PC_R_HZ_TILT_Y * data.TopRightDz;
            }
            if (data.TopLeftAlignRaw != null)
            {
                data.TopLeftAlignRaw.DxCamToMark += PC_L_HZ_TILT_X * data.TopLeftDz;
                data.TopLeftAlignRaw.DyCamToMark += PC_L_HZ_TILT_Y * data.TopLeftDz;
            }

            // 2) Btm: Fid → Align Z 평면 투영 (Right=HC2, Left=HC1)
            double HC1_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.HC1_HZ_TILT_X);
            double HC1_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.HC1_HZ_TILT_Y);
            double HC2_HZ_TILT_X = _recipeService.FindByParamDouble(MotionExtensions.HC2_HZ_TILT_X);
            double HC2_HZ_TILT_Y = _recipeService.FindByParamDouble(MotionExtensions.HC2_HZ_TILT_Y);

            Point2D lFidTilt = Point2D.of(HC1_HZ_TILT_X * data.BtmDz, HC1_HZ_TILT_Y * data.BtmDz);
            Point2D rFidTilt = Point2D.of(HC2_HZ_TILT_X * data.BtmDz, HC2_HZ_TILT_Y * data.BtmDz);

            if (data.BtmRightFidRaw != null)
                data.BtmRightFidRaw = Point2D.of(data.BtmRightFidRaw.X + rFidTilt.X, data.BtmRightFidRaw.Y + rFidTilt.Y);
            if (data.BtmLeftFidRaw != null)
                data.BtmLeftFidRaw = Point2D.of(data.BtmLeftFidRaw.X + lFidTilt.X, data.BtmLeftFidRaw.Y + lFidTilt.Y);

            // 3) (Manual) HcRO 회전중심 — CameraDist로 확정된 Hc2Offset + raw 회전점 + tilt
            if (data.TracingMode == TracingMode.Manual && data.Hc1RoRaw != null && data.Hc2RoRaw != null)
            {
                ComputeHcroCenter(data, data.Hc1RoRaw, data.Hc2RoRaw, lFidTilt, rFidTilt);
            }

            _logger.Information(
                "ApplyMeasurementCorrections — TopΔZ(R={TRz:F4},L={TLz:F4}), BtmΔZ={BDz:F4}, " +
                "BtmFidTilt R({RDx:F5},{RDy:F5})/L({LDx:F5},{LDy:F5})",
                data.TopRightDz, data.TopLeftDz, data.BtmDz,
                rFidTilt.X, rFidTilt.Y, lFidTilt.X, lFidTilt.Y);
        }

        /// <summary>
        /// Hc2Offset(HC1↔HC2 카메라 거리)이 확정된 뒤 호출한다.
        /// raw로 수집한 HC1/HC2 피듀셜을 공통 좌표계로 결합해 카메라거리 의존 값을 일괄 계산한다.
        ///   - M2FidTheta / FidCurrentDist : P-Table HC 피듀셜(Hc1FidCurrent / Hc2FidCurrent)
        ///   - M3FidTheta                  : W-Table HC 피듀셜(BtmLeftFidRaw / BtmRightFidRaw, tilt 투영 후)
        /// 좌표 규약: 절대 = (−HC1) , (Hc2Offset − HC2). (LogMeasurement2/3와 동일)
        /// </summary>
        private void ComputeHc2OffsetDependent(AlignData data)
        {
            if (data?.Hc2Offset == null) return;
            var offset = data.Hc2Offset;

            if (data.Hc1FidCurrent != null && data.Hc2FidCurrent != null)
            {
                double lfX = -data.Hc1FidCurrent.X, lfY = -data.Hc1FidCurrent.Y;
                double rfX = offset.X - data.Hc2FidCurrent.X, rfY = offset.Y - data.Hc2FidCurrent.Y;
                var r = CalibrationMath.CalcRelative(lfX, lfY, rfX, rfY);
                data.M2FidTheta = r.theta;
                data.FidCurrentDist = CalibrationMath.Dist(Point2D.of(rfX, rfY), Point2D.of(lfX, lfY));
            }

            if (data.BtmLeftFidRaw != null && data.BtmRightFidRaw != null)
            {
                double lfX = -data.BtmLeftFidRaw.X, lfY = -data.BtmLeftFidRaw.Y;
                double rfX = offset.X - data.BtmRightFidRaw.X, rfY = offset.Y - data.BtmRightFidRaw.Y;
                var r = CalibrationMath.CalcRelative(lfX, lfY, rfX, rfY);
                data.M3FidTheta = r.theta;
            }

            _logger.Information(
                "ComputeHc2OffsetDependent — Hc2Offset=({OX:F4},{OY:F4}), M2FidTheta={M2:F4}°, M3FidTheta={M3:F4}°, FidCurrentDist={D:F6}",
                offset.X, offset.Y, data.M2FidTheta, data.M3FidTheta, data.FidCurrentDist);
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

            double dLfX = d.BtmLeftFidRaw.X - refLfDx;
            double dLfY = d.BtmLeftFidRaw.Y - refLfDy;
            double dRfX = d.BtmRightFidRaw.X - refRfDx;
            double dRfY = d.BtmRightFidRaw.Y - refRfDy;

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

        /// <summary>
        /// HC1/HC2 카메라 간 거리(Hc2Offset)를 측정한다.
        /// HC1으로 좌측 마크를 측정한 뒤 스테이지를 이동해 HC2로 동일 마크를 측정,
        /// 두 카메라 중심 좌표 차이를 Hc2Offset에 저장한다.
        /// </summary>
        private async Task CameraDist(AlignData d, CancellationToken ct)
        {
            try
            {
                double HcCenterErrorX = await GetRecipe("HcCenterErrorX");
                double HcCenterErrorY = await GetRecipe("HcCenterErrorY");

                var hc1 = await VisionResult(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, DirectType.LEFT, MotionExtensions.W_Y, ct);
                await Task.WhenAll(
                    RelativeMotionsMove(MotionExtensions.H_X, -12.5, ct),
                    RelativeMotionsMove(MotionExtensions.W_Y, 7,ct));

                var hc2 = await VisionResult(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, DirectType.RIGHT, MotionExtensions.W_Y, ct);
                d.Hc2Offset = Point2D.of(hc1.CenterX - hc2.CenterX, hc1.CenterY - hc2.CenterY);

                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_X, "PLACE_CENTER", HcCenterErrorX, ct),
                    MotionsMove(MotionExtensions.W_Y, "PLACE_CENTER", HcCenterErrorY, ct)
                );

                _logger.Information("CameraDist — Hc2Offset=({Hc2X:F4}, {Hc2Y:F4})",
                    d.Hc2Offset.X, d.Hc2Offset.Y);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Error(e, "CameraDist 실패");
                throw;
            }
        }

        /// <summary>
        /// H_T를 0°/±0.75°로 회전시키며 HC1/HC2 피듀셜을 측정하고, 피듀셜 위치 보정만 적용한
        /// raw 측정 점을 HC1/HC2로 나누어 반환한다. 좌표계 통합(부호 반전, Hc2Offset 적용)은
        /// <see cref="ComputeHcroCenter"/>에서 수행한다.
        /// </summary>
        private async Task<(List<Point2D> hc1Raw, List<Point2D> hc2Raw)>
            MeasureHcroPoints(AlignData d, CancellationToken ct)
        {
            try
            {
                var hc1Raw = new List<Point2D>();
                var hc2Raw = new List<Point2D>();

                // 0도: 이미 측정된 BtmLeftFidRaw(HC1), BtmRightFidRaw(HC2) 사용
                hc1Raw.Add(Point2D.of(d.BtmLeftFidRaw.X, d.BtmLeftFidRaw.Y));
                hc2Raw.Add(Point2D.of(d.BtmRightFidRaw.X, d.BtmRightFidRaw.Y));

                // -0.75도, +0.75도: 회전 후 측정
                double[] angles = { -0.75, 0.75 };
                for (int i = 0; i < angles.Length; i++)
                {
                    // Hc1X: 0.00361, Hc1Y: -0.00112, Hc2X: 0.00807, Hc2Y: -0.00269

                    ct.ThrowIfCancellationRequested();
                    await MotionsMove(MotionExtensions.H_T, angles[i], ct);

                    await communicationService.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
                    var v1 = await communicationService.RequestVisionMarkPosition(
                        MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                    if (v1.Result == Result.NG)
                        throw new Exception($"Hc1 {angles[i]}° 피듀셜 측정 실패");
                    v1.X = v1.X;
                    v1.Y = v1.Y;

                    await communicationService.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
                    var v2 = await communicationService.RequestVisionMarkPosition(
                        MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                    if (v2.Result == Result.NG)
                        throw new Exception($"Hc2 {angles[i]}° 피듀셜 측정 실패");
                    v2.X = v2.X;
                    v2.Y = v2.Y;
                    hc1Raw.Add(Point2D.of(v1.X, v1.Y));
                    hc2Raw.Add(Point2D.of(v2.X, v2.Y));
                }

                // H_T 복귀
                await MotionsMove(MotionExtensions.H_T, 0, ct);

                _logger.Information("MeasureHcroPoints — Hc1={Hc1Count}, Hc2={Hc2Count}",
                    hc1Raw.Count, hc2Raw.Count);
                return (hc1Raw, hc2Raw);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                _logger.Error(e, "MeasureHcroPoints 실패");
                throw;
            }
        }

        /// <summary>
        /// <see cref="MeasureHcroPoints"/>가 반환한 raw 측정 점을 통합 좌표계로 변환(HC1 부호 반전,
        /// HC2에 Hc2Offset 적용)한 뒤 원 피팅하여 회전 중심(Hcro)을 계산하고 <paramref name="d"/>에 저장한다.
        /// <paramref name="hc1Tilt"/>/<paramref name="hc2Tilt"/>가 주어지면 각 측정 점에 카메라별
        /// H_Z 수직도 보정량을 더해 Fid/Align을 동일 평면으로 맞춘 뒤 피팅한다(미지정 시 0).
        /// </summary>
        private void ComputeHcroCenter(
            AlignData d,
            List<Point2D> hc1Raw,
            List<Point2D> hc2Raw,
            Point2D hc1Tilt = null,
            Point2D hc2Tilt = null)
        {
            if (hc1Raw == null || hc2Raw == null || hc1Raw.Count == 0 || hc2Raw.Count == 0)
                throw new Exception("회전 중심 계산용 측정 점이 없습니다");

            var hc2XOffset = d.Hc2Offset.X;
            var hc2YOffset = d.Hc2Offset.Y;

            // 카메라별 H_Z 수직도(tilt) 보정량 (미지정 시 0)
            double h1tx = hc1Tilt?.X ?? 0.0, h1ty = hc1Tilt?.Y ?? 0.0;
            double h2tx = hc2Tilt?.X ?? 0.0, h2ty = hc2Tilt?.Y ?? 0.0;

            var allPoints = new System.Collections.Generic.List<Point2D>();
            foreach (var p in hc1Raw)
                allPoints.Add(Point2D.of(-(p.X + h1tx), -(p.Y + h1ty)));
            foreach (var p in hc2Raw)
                allPoints.Add(Point2D.of(hc2XOffset - (p.X + h2tx), hc2YOffset - (p.Y + h2ty)));

            var hcRO = CalibrationMath.FitCircleCenter(allPoints);
            d.Hcro = Point2D.of(hcRO.X, hcRO.Y);

            _logger.Information("ComputeHcroCenter — Hc2Offset=({Hc2X:F4}, {Hc2Y:F4}), tilt(HC1={H1x:F5},{H1y:F5} / HC2={H2x:F5},{H2y:F5}), HcRO=({RoX:F4}, {RoY:F4}), Points={Count}",
                hc2XOffset, hc2YOffset, h1tx, h1ty, h2tx, h2ty, hcRO.X, hcRO.Y, allPoints.Count);
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