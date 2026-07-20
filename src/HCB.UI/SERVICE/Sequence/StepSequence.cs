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
            double fidAlignGap = await GetRecipe(MotionExtensions.FID_ALIGN_GAP);
            string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
            string[] z = { MotionExtensions.H_Z };

            await Init_Head(ct);
            
            await Task.WhenAll(
                MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct),
                RelativeMotionsMove(MotionExtensions.h_z, fidAlignGap, ct),
                MotionsMove(xy, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct)
            );
            await MotionsMove(z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);

            return await MeasureWithRetry(MarkType.FIDUCIAL, CameraType.PC_HIGH, DirectType.RIGHT,
                MotionExtensions.P_Y, AvgMode, ct);
        }

        public async Task<VisionMarkResult> TopDieVisionRightAlign(bool AvgMode, CancellationToken ct)
        {
            _logger.Information("Top Die Vision (Right Align) Start");
            EQStatusCheck();
            string name = "RightAlignHeight";

            var param = _recipeService.UseRecipe?.ParamList
               .FirstOrDefault(p => p.Name == name);
            if (param == null)
                throw new DBException(DBErrorCode.NOT_FOUND, $"사용하는 레시피에 {name} 이 없습니다.");
            double zPosition = double.Parse(param.Value);

            await MotionsMove(MotionExtensions.H_Z, zPosition, ct);

            return await MeasureWithRetry(MarkType.ALIGN_MARK, CameraType.PC_HIGH, DirectType.RIGHT,
                MotionExtensions.P_Y, AvgMode, ct);
        }

        public async Task<VisionMarkResult> TopDieVisionLeftFid(bool AvgMode, CancellationToken ct)
        {
            _logger.Information("Top Die Vision (Left Fid) Start");
            EQStatusCheck();

            string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
            string[] z = { MotionExtensions.H_Z };

            await MotionsMove(xy, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);
            await MotionsMove(z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);

            return await MeasureWithRetry(MarkType.FIDUCIAL, CameraType.PC_HIGH, DirectType.LEFT,
                MotionExtensions.P_Y, AvgMode, ct);
        }

        public async Task<VisionMarkResult> TopDieVisionLeftAlign(bool AvgMode, CancellationToken ct)
        {
            _logger.Information("Top Die Vision (Left Align) Start");
            EQStatusCheck();
            string name = "LeftAlignHeight";

            var param = _recipeService.UseRecipe?.ParamList
               .FirstOrDefault(p => p.Name == name);
            if (param == null)
                throw new DBException(DBErrorCode.NOT_FOUND, $"사용하는 레시피에 {name} 이 없습니다.");

            double zPosition = double.Parse(param.Value);
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


        public async Task TopDieSet(CancellationToken ct)
        {
            try
            {
                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = _paramService.GetDouble("ShankToWaferOffset");
                double HcCenterErrorX = await GetRecipe("HcCenterErrorX");
                double HcCenterErrorY = await GetRecipe("HcCenterErrorY");

                await Init_Head(ct);
                await Task.WhenAll(
                    MotionsMove(MotionExtensions.H_X, "PLACE_CENTER", HcCenterErrorX, ct),
                    MotionsMove(MotionExtensions.W_Y, "PLACE_CENTER", HcCenterErrorY, ct)
                );
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
                    await Task.Delay(100);
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