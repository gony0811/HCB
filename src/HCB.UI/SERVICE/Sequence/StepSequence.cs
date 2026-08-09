using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        public async Task<VisionMarkResult> BtmDieVisionRightFid(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Btm Die Vision (Right Fid) Start");
                EQStatusCheck();

                var result = false;
                VisionMarkResult fid = new VisionMarkResult
                {
                    CameraType = CameraType.HC2_HIGH,  
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.W_Y, ct),
                };

                result = await communicationService.RequestAFStart(CameraType.HC2_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new VisionException(VisionErrorCode.AF_FAIL);
                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC2_HIGH, "RIGHT", AvgMode);
                VisionResult(rFidXY);
                fid.DxCamToMark = rFidXY.X;
                fid.DyCamToMark = rFidXY.Y;
                return fid;
            }
            catch (VisionException ex)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> BtmDieVisionLeftFid(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Btm Die Vision (Left Fid) Start");
                EQStatusCheck();

                var result = false;
                VisionMarkResult fid = new VisionMarkResult
                {
                    CameraType = CameraType.HC1_HIGH,   // ★ W-Table 좌측 = HC1
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.LEFT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.W_Y, ct),
                };

                result = await communicationService.RequestAFStart(CameraType.HC1_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");
                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC1_HIGH, "LEFT", AvgMode);
                VisionResult(rFidXY);
                fid.DxCamToMark = rFidXY.X;
                fid.DyCamToMark = rFidXY.Y;
                return fid;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> BtmDieVisionRightAlign(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Btm Die Vision (Right Align) Start");
                EQStatusCheck();

                var result = false;
                VisionMarkResult fid = new VisionMarkResult
                {
                    CameraType = CameraType.HC2_HIGH,  
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.W_Y, ct),
                };

                result = await communicationService.RequestAFStart(CameraType.HC2_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC2_HIGH, "RIGHT", AvgMode);
                VisionResult(rFidXY);
                fid.DxCamToMark = rFidXY.X;
                fid.DyCamToMark = rFidXY.Y;
                return fid;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> BtmDieVisionLeftAlign(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Btm Die Vision (Left Align) Start");
                EQStatusCheck();

                var result = false;
                VisionMarkResult fid = new VisionMarkResult
                {
                    CameraType = CameraType.HC1_HIGH,   // ★ W-Table 좌측 = HC1
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.LEFT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.W_Y, ct),
                };

                result = await communicationService.RequestAFStart(CameraType.HC1_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC1_HIGH, "LEFT", AvgMode);
                VisionResult(rFidXY);
                fid.DxCamToMark = rFidXY.X;
                fid.DyCamToMark = rFidXY.Y;
                return fid;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<BtmMarkResponse> BtmDieVisionAlign(DirectType directType = DirectType.BOTH, bool avgMode = true)
        {
            var result = await communicationService.RequestHeadAlign(directType, avgMode);

            return result;  
        }


        public async Task<VisionMarkResult> TopDieVisionRightFid(bool AvgMode, CancellationToken ct)
        {
            _logger.Information("Top Die Vision (Right Fid) Start");
            EQStatusCheck();
            await MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);
            return await MeasureWithRetry(MarkType.FIDUCIAL, CameraType.PC_HIGH, DirectType.RIGHT,
                MotionExtensions.P_Y, AvgMode, ct);
        }

        public async Task<VisionMarkResult> TopDieVisionRightAlign(bool AvgMode, string size, CancellationToken ct)
        {
            _logger.Information("Top Die Vision (Right Align) Start");
            EQStatusCheck();
            string name = "RightAlignHeight";

            var param = _recipeService.UseRecipe?.ParamList
               .FirstOrDefault(p => p.Name == name);
            if (param == null)
                throw new DBException(DBErrorCode.NOT_FOUND, $"사용하는 레시피에 {name} 이 없습니다.");
            double zPosition = double.Parse(param.Value);
            
            await Task.WhenAll(
                MotionsMove(MotionExtensions.H_X, MotionExtensions.P_RIGHT_ALIGN_HIGH + size,  ct),
                MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_ALIGN_HIGH + size, ct)
            );

            await MotionsMove(MotionExtensions.H_Z, zPosition, ct);

            return await MeasureWithRetry(MarkType.ALIGN_MARK, CameraType.PC_HIGH, DirectType.RIGHT,
                MotionExtensions.P_Y, AvgMode, ct);
        }

        public async Task<VisionMarkResult> TopDieVisionLeftFid(bool AvgMode, CancellationToken ct)
        {
            _logger.Information("Top Die Vision (Left Fid) Start");
            EQStatusCheck();

            await MotionsMove(MotionExtensions.H_Z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);

            return await MeasureWithRetry(MarkType.FIDUCIAL, CameraType.PC_HIGH, DirectType.LEFT,
                MotionExtensions.P_Y, AvgMode, ct);
        }

        public async Task<VisionMarkResult> TopDieVisionLeftAlign(bool AvgMode, string size, CancellationToken ct)
        {
            _logger.Information("Top Die Vision (Left Align) Start");
            EQStatusCheck();
            string name = "LeftAlignHeight";

            var param = _recipeService.UseRecipe?.ParamList
               .FirstOrDefault(p => p.Name == name);
            if (param == null)
                throw new DBException(DBErrorCode.NOT_FOUND, $"사용하는 레시피에 {name} 이 없습니다.");

            double zPosition = double.Parse(param.Value);
            await Task.WhenAll(
                MotionsMove(MotionExtensions.H_X, MotionExtensions.P_LEFT_ALIGN_HIGH + size, ct),
                MotionsMove(MotionExtensions.P_Y, MotionExtensions.P_LEFT_ALIGN_HIGH + size, ct)
            );
            await MotionsMove(MotionExtensions.H_Z, zPosition, ct);
            return await MeasureWithRetry(MarkType.ALIGN_MARK, CameraType.PC_HIGH, DirectType.LEFT,
                MotionExtensions.P_Y, AvgMode, ct);
        }

        public async Task<VisionMarkResult> VisionResult(
            CameraType cameraType, MarkType markType, DirectType directType,
            string yName, CancellationToken ct)
        {
            try
            {
                _logger.Information($"Vision ({cameraType} / {markType} / {directType}) Start");
                EQStatusCheck();

                var result = false;

                VisionMarkResult visionResult = new VisionMarkResult
                {
                    CameraType = cameraType,
                    MarkType = markType,
                    DirectType = directType,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(yName, ct)
                };

                result = await communicationService.RequestAFStart(cameraType, markType, ct);
                if (result == false) throw new Exception("AF 실패");

                var xy = await communicationService.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                VisionResult(xy);
                visionResult.DxCamToMark = xy.X;
                visionResult.DyCamToMark = xy.Y;

                return visionResult;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }


        // placeCenter == null : 기존 동작(PLACE_CENTER + HcCenterError 로 이동)
        // placeCenter != null : Wafer 본딩 등에서 지정한 Die Center(고배 절대좌표, HcCenterError 이미 포함)로 절대 이동
        public async Task TopDieSet(CancellationToken ct, Point2D placeCenter = null)
        {
            try
            {
                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _paramService.GetDouble("ShankToWaferOffset");

                await Init_Head(ct);

                if (placeCenter != null)
                {
                    // 클릭한 Die의 Center(고배 절대좌표)로 이동
                    await Task.WhenAll(
                        MotionsMove(MotionExtensions.H_X, placeCenter.X, ct),
                        MotionsMove(MotionExtensions.W_Y, placeCenter.Y, ct)
                    );
                }
                else
                {
                    double HcCenterErrorX = await GetRecipe("HcCenterErrorX");
                    double HcCenterErrorY = await GetRecipe("HcCenterErrorY");
                    await Task.WhenAll(
                        MotionsMove(MotionExtensions.H_X, "PLACE_CENTER", HcCenterErrorX, ct),
                        MotionsMove(MotionExtensions.W_Y, "PLACE_CENTER", HcCenterErrorY, ct)
                    );
                }

                await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
       

        public async Task BondingCorr(AlignData data, CancellationToken ct)
        {
            try
            {
                _logger.Information("BondingAlign Start | ResultX={X}, ResultY={Y}, ResultT={T}",
                    data.ResultX, data.ResultY, data.ResultT);

                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _paramService.GetDouble("ShankToWaferOffset");
                double readyPosition = await GetRecipe("READY_POSITION");
                
                await Task.WhenAll(
                    RelativeMotionsMove(MotionExtensions.H_X, -data.ResultX, ct),
                    RelativeMotionsMove(MotionExtensions.W_Y, -data.ResultY, ct),
                    RelativeMotionsMove(MotionExtensions.H_T, data.ResultT, ct)
                );

                // H_T 회전 보정 InPosition 후 목표/실제 위치 검증 (1% 초과 시 알람 + 중단)
                await VerifyHtRotationAsync(ct);

                await MotionsMove(MotionExtensions.H_Z,
                    shankToWaferOffset - topDieThickness - btmDieThickness - readyPosition, ct);

                _logger.Information("BondingAlign 완료");
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("BondingAlign 취소됨");
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "BondingAlign 실패");
                throw;
            }
        }

        /// <summary>
        /// H_T 회전 보정 후 InPosition 시점의 명령 목표 위치(CommandPosition) 대비
        /// 실제 측정 위치(CurrentPosition)를 로그로 남기고, 오차율이 1%를 초과하면
        /// 전용 알람(E0036)을 발생시키고 예외를 던져 본딩을 중단한다.
        /// </summary>
        private async Task VerifyHtRotationAsync(CancellationToken ct)
        {
            var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            var ht = motionDevice?.FindMotionByName(MotionExtensions.H_T);
            if (ht == null)
                throw new DBException(DBErrorCode.NOT_FOUND, $"[Motion Error] '{MotionExtensions.H_T}' 축을 찾을 수 없습니다.");

            double commanded = ht.CommandPosition;   // 명령 목표 위치 (DesPos)
            double actual = ht.CurrentPosition;      // 실제 측정 위치 (ActPos)
            double diff = actual - commanded;
            // 명령 목표 위치 기준 오차율. 목표각이 0 근처면 분모가 0에 수렴하므로 가드.
            double errorRatio = Math.Abs(commanded) > 1e-6 ? Math.Abs(diff) / Math.Abs(commanded) : 0.0;

            _logger.Information(
                "H_T 회전 보정 검증 | 목표(Command)={Cmd:F6}, 실제(Current)={Act:F6}, 오차={Diff:F6}, 오차율={Ratio:P3}",
                commanded, actual, diff, errorRatio);

            if (Math.Abs(commanded) > 1e-6 && errorRatio > 0.01)
            {
                _logger.Error(
                    "H_T 위치 오차 {Ratio:P3} 가 허용치(1%)를 초과 | 목표={Cmd:F6}, 실제={Act:F6}",
                    errorRatio, commanded, actual);

                // 호출부(StepSeqTabViewModel)가 ErrorException→SetAlarm 브리지를 거치지 않으므로 직접 발생시킨다.
                await _alarmService.SetAlarm(PmacErrorCode.HT_POSITION_ERROR);

                throw new PmacException(
                    PmacErrorCode.HT_POSITION_ERROR,
                    $"[H_T 회전 보정 오차] 목표={commanded:F6}, 실제={actual:F6}, 오차율={errorRatio:P3} (허용 1%)");
            }
        }

        /// <summary>
        /// 2단계: 가압 본딩 (PMAC 가압 + 폴링 + 진공 해제)
        /// </summary>
        public async Task BondingPress(ObservableCollection<BondingDataPoint> bondingDataPoints, CancellationToken ct)
        {
            var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            try
            {
                _logger.Information("BondingPress Start");

                var step = _recipeService.FindStepByName("TOP PRESS");

                await Task.Delay(200, ct);

                // 이전 상태 클리어
                await device.SendCommand(MotionExtensions.BONDING_START + "=0");
                await device.SendCommand(MotionExtensions.BONDING_INIT + "=1");
                await Task.Delay(100);
                await device.SendCommand(MotionExtensions.BONDING_INIT + "=0");
                await Task.Delay(50);

                string preCheck = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                int preStatus = int.TryParse(preCheck.Trim(), out int ps) ? ps : -1;
                _logger.Information("BondingPress 시작 전 상태: {Status}", preStatus);
                if (preStatus != 0)
                    _logger.Warning("STATUS_COMPLETE가 0으로 초기화되지 않음: {Status}", preStatus);

                // 파라미터 설정 + 시작
                await device.SendCommand("Y029=1");
                await Task.Delay(300);
                await device.SendCommand("Y029=0");
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={step.AccTime}");
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME2 + $"={step.AccTime2}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={step.ContTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={step.DecTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={step.LoadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={step.Current}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT2 + $"={step.Current2}");
                await device.SendCommand(MotionExtensions.BONDING_START + "=1");

                _logger.Information("BONDING Step={StepName}: ACC={Acc}, ACC2={Acc2}, CONT={Cont}, DEC={Dec}, LOADCELL={Load}, CURRENT={Cur}, CURRENT2={Cur2}",
                    step.Name, step.AccTime, step.AccTime2, step.ContTime, step.DecTime, step.LoadCell, step.Current, step.Current2);

                const int pollingIntervalMs = 100;
                int timeoutMs = step.AccTime + step.AccTime2 + step.ContTime + step.DecTime + 2000;
                var sw = Stopwatch.StartNew();
                bool bondingComplete = false;
                bool vacuumOff = false;

                bondingDataPoints.Clear();

                while (!bondingComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    long elapsed = sw.ElapsedMilliseconds;

                    // 설정 시점에 Vacuum OFF
                    if (!vacuumOff && elapsed >= step.VacOffTime)
                    {
                        await HVacOnOff(false, ct);
                        vacuumOff = true;
                        _logger.Information("Vacuum OFF ({Elapsed}ms, 설정={VacOffMs}ms)",
                            elapsed, step.VacOffTime);
                    }

                    double forceValue = 0;
                    string analog = await device.SendCommand<string>(MotionExtensions.ANALOG_INPUT);
                    if (double.TryParse(analog.Trim(), out forceValue))
                    {
                        bondingDataPoints.Add(new BondingDataPoint
                        {
                            TimeS = sw.Elapsed.TotalSeconds,
                            ForceN = forceValue * 0.00373
                        });
                    }
                    else
                    {
                        _logger.Warning("AnalogInput 파싱 실패: {Response}", analog);
                    }

                    string strResponse = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                    var values = strResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length > 0 && int.TryParse(values[0].Trim(), out int statusCode))
                    {
                        bondingComplete = statusCode == 6;
                        _logger.Information("Bonding 상태: {Code} (complete={Complete}) | Force: {Force:F3}N (경과: {Elapsed}ms)",
                            statusCode, bondingComplete, forceValue * 0.00373, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.Warning("Bonding 상태 응답 파싱 실패: {Response}", strResponse);
                    }

                    if (!bondingComplete)
                    {
                        if (sw.ElapsedMilliseconds > timeoutMs)
                            throw new TimeoutException($"Bonding 완료 대기 시간 초과 ({timeoutMs}ms)");

                        await Task.Delay(pollingIntervalMs, ct);
                    }
                }

                sw.Stop();
                _logger.Information("BondingPress 완료 (총 소요: {Elapsed}ms, 수집 포인트: {Count}개)",
                    sw.ElapsedMilliseconds, bondingDataPoints.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("BondingPress 취소됨");
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.Error(ex, "BondingPress 타임아웃");
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "BondingPress 실패");
                throw;
            }
            finally
            {
                try
                {
                    await device.SendCommand(MotionExtensions.BONDING_START + "=0");
                    await device.SendCommand(MotionExtensions.BONDING_INIT + "=1");
                    await Task.Delay(5000);
                    await device.SendCommand(MotionExtensions.BONDING_INIT + "=0");
                    _logger.Information("BondingPress 초기화 완료");
                    await MappingOff();
                 
            }
                catch (Exception ex)
                {
                    _logger.Error(ex, "BondingPress 초기화 실패");
                }
            }
        }

        public async Task BondingTest(ObservableCollection<BondingDataPoint> bondingDataPoints, CancellationToken ct)
        {
            var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            try
            {
                _logger.Information("BondingPress Start (No Vacuum Off)");

                var step = _recipeService.FindStepByName("TOP PRESS");
                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _paramService.GetDouble("ShankToWaferOffset");
                double readyPosition = await GetRecipe("READY_POSITION");

                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_X, "PLACE_CENTER", ct),
                    MotionsMove(MotionExtensions.W_Y, "PLACE_CENTER", ct)
                    );

                await MotionsMove(MotionExtensions.H_Z,
                    shankToWaferOffset - topDieThickness - btmDieThickness - readyPosition, ct);

                await Task.Delay(200, ct);

                // 이전 상태 클리어
                await device.SendCommand(MotionExtensions.BONDING_START + "=0");
                await device.SendCommand(MotionExtensions.BONDING_INIT + "=1");
                await Task.Delay(100);
                await device.SendCommand(MotionExtensions.BONDING_INIT + "=0");
                await Task.Delay(50);

                string preCheck = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                int preStatus = int.TryParse(preCheck.Trim(), out int ps) ? ps : -1;
                _logger.Information("BondingPress 시작 전 상태: {Status}", preStatus);
                if (preStatus != 0)
                    _logger.Warning("STATUS_COMPLETE가 0으로 초기화되지 않음: {Status}", preStatus);  

                // 파라미터 설정 + 시작
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={step.AccTime}");
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME2 + $"={step.AccTime2}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={step.ContTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={step.DecTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={step.LoadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={step.Current}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT2 + $"={step.Current2}");
                await device.SendCommand(MotionExtensions.BONDING_START + "=1");

                _logger.Information("BONDING Step={StepName}: ACC={Acc}, ACC2={Acc2}, CONT={Cont}, DEC={Dec}, LOADCELL={Load}, CURRENT={Cur}, CURRENT2={Cur2}",
                    step.Name, step.AccTime, step.AccTime2, step.ContTime, step.DecTime, step.LoadCell, step.Current, step.Current2);

                const int pollingIntervalMs = 100;
                int timeoutMs = step.AccTime + step.AccTime2 + step.ContTime + step.DecTime + 2000;
                var sw = Stopwatch.StartNew();
                bool bondingComplete = false;

                bondingDataPoints.Clear();

                while (!bondingComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    double forceValue = 0;
                    string analog = await device.SendCommand<string>(MotionExtensions.ANALOG_INPUT);
                    if (double.TryParse(analog.Trim(), out forceValue))
                    {
                        bondingDataPoints.Add(new BondingDataPoint
                        {
                            TimeS = sw.Elapsed.TotalSeconds,
                            ForceN = forceValue * 0.00373
                        });
                    }
                    else
                    {
                        _logger.Warning("AnalogInput 파싱 실패: {Response}", analog);
                    }

                    string strResponse = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                    var values = strResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length > 0 && int.TryParse(values[0].Trim(), out int statusCode))
                    {
                        bondingComplete = statusCode == 6;
                        _logger.Information("Bonding 상태: {Code} (complete={Complete}) | Force: {Force:F3}N (경과: {Elapsed}ms)",
                            statusCode, bondingComplete, forceValue * 0.00373, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.Warning("Bonding 상태 응답 파싱 실패: {Response}", strResponse);
                    }

                    if (!bondingComplete)
                    {
                        if (sw.ElapsedMilliseconds > timeoutMs)
                            throw new TimeoutException($"Bonding 완료 대기 시간 초과 ({timeoutMs}ms)");

                        await Task.Delay(pollingIntervalMs, ct);
                    }
                }

                sw.Stop();
                _logger.Information("BondingPress 완료 (총 소요: {Elapsed}ms, 수집 포인트: {Count}개)",
                    sw.ElapsedMilliseconds, bondingDataPoints.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("BondingPress 취소됨");
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.Error(ex, "BondingPress 타임아웃");
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "BondingPress 실패");
                throw;
            }
            finally
            {
                try
                {
                    await device.SendCommand(MotionExtensions.BONDING_START + "=0");
                    await device.SendCommand(MotionExtensions.BONDING_INIT + "=1");
                    await Task.Delay(100);
                    await device.SendCommand(MotionExtensions.BONDING_INIT + "=0");
                    _logger.Information("BondingPress 초기화 완료");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "BondingPress 초기화 실패");
                }
            }
        }
        public async Task<double> GetRecipe(string name, CancellationToken ct = default)
        {
            var value = _recipeService.FindByParam(name).Value;

            if (!double.TryParse(value, out double result))
                throw new InvalidCastException($"레시피 {name}값이 Double타입이 아닙니다");

            return result;
        }

        public async Task<int> GetRecipeInt(string name, CancellationToken ct = default)
        {
            var value = _recipeService.FindByParam(name).Value;

            if (!int.TryParse(value, out int result))
                throw new InvalidCastException($"레시피 {name}값이 INT타입이 아닙니다");

            return result;
        }

        public void VisionResult(VisionMarkPositionResponse response)
        {
            if (response.Result == Result.NG) throw new VisionException(VisionErrorCode.MEASUREMENT_FAIL);
        }

        private async Task<VisionMarkResult> MeasureWithRetry(
            MarkType markType, CameraType cameraType, DirectType directType,
            string yAxisName, bool avgMode, CancellationToken ct)
        {
            int retryMax = GetEcParamInt("VisionRetryMax", 3);
            double retryStep = GetEcParamDouble("VisionRetryStepMm", 0.005);

            VisionMarkResult mark = new VisionMarkResult
            {
                CameraType = cameraType,
                MarkType = markType,
                DirectType = directType,
                StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                StageY = await GetCurrentPosition(yAxisName, ct)
            };

            for (int attempt = 0; attempt <= retryMax; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var xy = await communicationService.RequestVisionMarkPosition(
                        markType, cameraType, directType.ToString(), avgMode);
                    VisionResult(xy);
                    mark.DxCamToMark = xy.X;
                    mark.DyCamToMark = xy.Y;
                    return mark;
                }
                catch (VisionException) when (attempt < retryMax)
                {
                    _logger.Warning("비전 측정 실패 ({Camera}/{Mark}/{Direct}) — 재시도 {Attempt}/{Max}, H_Z -{Step}mm",
                        cameraType, markType, directType, attempt + 1, retryMax, retryStep);
                    await RelativeMotionsMove(MotionExtensions.H_Z, -retryStep, ct);
                    mark.StageX = await GetCurrentPosition(MotionExtensions.H_X, ct);
                    mark.StageY = await GetCurrentPosition(yAxisName, ct);
                }
            }

            throw new VisionException(VisionErrorCode.MEASUREMENT_FAIL);
        }
    }
}