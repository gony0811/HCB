using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        public async Task BtmDieDrop(int vacNum, CancellationToken ct)
        {
            double btmDieThickness = await GetRecipe("BtmDieThickness");
            double shankToWaferOffset = await GetRecipe("ShankToWaferOffset");

            await Init_Head(ct);
            await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
            await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - btmDieThickness, ct);
            await _sequenceHelper.WTableVacuum(vacNum, eOnOff.On, ct);
            bool result = await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, ct);
            if (!result) throw new Exception("HeadPicker를 확인해주세요");
        }


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
                if (result == false) throw new Exception("AF 실패");
                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC2_HIGH, "RIGHT", AvgMode);
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



        public async Task<Dictionary<string, VisionMarkResult>> TopDieVision(CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision Start");

                EQStatusCheck();
                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                var result = false;

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };

                VisionMarkResult rightFid = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,    // ★ P-Table
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_RIGHT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_RIGHT_HIGH, ct),
                };

                VisionMarkResult rightAlign = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,    // ★ P-Table
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.RIGHT,
                    StageX = rightFid.StageX,
                    StageY = rightFid.StageY,
                };

                VisionMarkResult leftFid = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,    // ★ P-Table
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.LEFT,
                    StageX = await GetPosition(MotionExtensions.H_X, MotionExtensions.P_LEFT_HIGH, ct),
                    StageY = await GetPosition(MotionExtensions.P_Y, MotionExtensions.P_LEFT_HIGH, ct),
                };

                VisionMarkResult leftAlign = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,    // ★ P-Table
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.LEFT,
                    StageX = leftFid.StageX,
                    StageY = leftFid.StageY,
                };

                Dictionary<string, VisionMarkResult> visionResults = new Dictionary<string, VisionMarkResult>
                {
                    { "RIGHT_FID", rightFid },
                    { "RIGHT_ALIGN", rightAlign },
                    { "LEFT_FID", leftFid },
                    { "LEFT_ALIGN", leftAlign }
                };

                await Init_Head(ct);

                // 우측 피듀셜마크 이동
                await MotionsMove(xy, MotionExtensions.P_RIGHT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);

                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");

                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "RIGHT");
                VisionResult(rFidXY);
                rightFid.DxCamToMark = rFidXY.X;
                rightFid.DyCamToMark = rFidXY.Y;

                // 우측 얼라인마크 이동
                await MotionsMove(z, MotionExtensions.P_RIGHT_ALIGN_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var rAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "RIGHT");
                VisionResult(rAlignXY);
                rightAlign.DxCamToMark = rAlignXY.X;
                rightAlign.DyCamToMark = rAlignXY.Y;

                // 좌측 피듀셜마크 이동
                await MotionsMove(xy, MotionExtensions.P_LEFT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");
                var lFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "LEFT");
                VisionResult(lFidXY);
                leftFid.DxCamToMark = lFidXY.X;
                leftFid.DyCamToMark = lFidXY.Y;

                // 좌측 얼라인마크 이동
                await MotionsMove(z, MotionExtensions.P_LEFT_ALIGN_HIGH, ct);
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var lAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "LEFT");
                VisionResult(lAlignXY);
                leftAlign.DxCamToMark = lAlignXY.X;
                leftAlign.DyCamToMark = lAlignXY.Y;

                return visionResults;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> TopDieVisionRightFid(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision (Right Fid) Start");
                EQStatusCheck();

                var result = false;
                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };

                await Init_Head(ct);
                await MotionsMove(xy, MotionExtensions.P_RIGHT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_RIGHT_FIDUCIAL_HIGH, ct);
                VisionMarkResult rightFid = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,    
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.P_Y, ct)
                };
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");
                var rFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "RIGHT", AvgMode);
                VisionResult(rFidXY);
                rightFid.DxCamToMark = rFidXY.X;
                rightFid.DyCamToMark = rFidXY.Y;

                return rightFid;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> TopDieVisionRightAlign(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision (Right Align) Start");
                EQStatusCheck();

                var result = false;

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };

                await Init_Head(ct);
                await MotionsMove(xy, MotionExtensions.P_RIGHT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_RIGHT_ALIGN_HIGH, ct);

                VisionMarkResult rightAlign = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,   
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.RIGHT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.P_Y, ct)
                };
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var rAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "RIGHT",AvgMode);
                VisionResult(rAlignXY);
                rightAlign.DxCamToMark = rAlignXY.X;
                rightAlign.DyCamToMark = rAlignXY.Y;

                return rightAlign;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
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

        public async Task<(double x, double y)> VisionAFNoResult(
            CameraType cameraType, MarkType markType, DirectType directType, CancellationToken ct)
        {
            try
            {
                _logger.Information("Vision AF-No Start");

                var xy = await communicationService.RequestVisionMarkPosition(markType, cameraType, directType.ToString());
                return (xy.X, xy.Y);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<(double x, double y)> VisionAFResult(
            CameraType cameraType, MarkType markType, DirectType directType, CancellationToken ct)
        {
            try
            {
                _logger.Information("Vision AF Start");
                var result = false;
                result = await communicationService.RequestAFStart(cameraType, markType, ct);
                if (result == false) throw new Exception("AF 실패");

                var xy = await communicationService.RequestVisionMarkPosition(markType, cameraType, directType.ToString());

                return (xy.X, xy.Y);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }



        public async Task<VisionMarkResult> TopDieVisionLeftFid(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision (Left Fid) Start");
                EQStatusCheck();

                var result = false;

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };

                await Init_Head(ct);
                await MotionsMove(xy, MotionExtensions.P_LEFT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_LEFT_FIDUCIAL_HIGH, ct);
                VisionMarkResult leftFid = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,    // ★
                    MarkType = MarkType.FIDUCIAL,
                    DirectType = DirectType.LEFT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.P_Y, ct)
                };
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.FIDUCIAL, ct);
                if (result == false) throw new Exception("AF 실패");
                var lFidXY = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.PC_HIGH, "LEFT" ,AvgMode);
                VisionResult(lFidXY);
                leftFid.DxCamToMark = lFidXY.X;
                leftFid.DyCamToMark = lFidXY.Y;
                return leftFid;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkResult> TopDieVisionLeftAlign(bool AvgMode, CancellationToken ct)
        {
            try
            {
                _logger.Information("Top Die Vision (Left Align) Start");
                EQStatusCheck();

                var result = false;

                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z };

                await Init_Head(ct);

                await MotionsMove(xy, MotionExtensions.P_LEFT_HIGH, ct);
                await MotionsMove(z, MotionExtensions.P_LEFT_ALIGN_HIGH, ct);
                VisionMarkResult leftAlign = new VisionMarkResult
                {
                    CameraType = CameraType.PC_HIGH,
                    MarkType = MarkType.ALIGN_MARK,
                    DirectType = DirectType.LEFT,
                    StageX = await GetCurrentPosition(MotionExtensions.H_X, ct),
                    StageY = await GetCurrentPosition(MotionExtensions.P_Y, ct)
                };
                result = await communicationService.RequestAFStart(CameraType.PC_HIGH, markType: MarkType.ALIGN_MARK, ct);
                if (result == false) throw new Exception("AF 실패");
                var lAlignXY = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.PC_HIGH, "LEFT", AvgMode);
                VisionResult(lAlignXY);
                leftAlign.DxCamToMark = lAlignXY.X;
                leftAlign.DyCamToMark = lAlignXY.Y;
                return leftAlign;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task TopDiePlace(CancellationToken ct)
        {
            try
            {
                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = await GetRecipe("ShankToWaferOffset");

                await Init_Head(ct);
                await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
                await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - 0.1, ct);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task TCheck(CancellationToken ct)
        {
            var results = new List<(double angle, double hc1X, double hc1Y, double hc2X, double hc2Y)>();

            for (double angle = -1.5; angle <= 1.5; angle += 0.5)
            {
                await MotionsMove(MotionExtensions.H_T, angle, ct);

                var hc1 = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC1_HIGH, "");
                var hc2 = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC2_HIGH, "");

                results.Add((angle, hc1.X, hc1.Y, hc2.X, hc2.Y));
            }

            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TCheck_Result.csv");
            var sb = new StringBuilder();

            if (!File.Exists(path))
            {
                sb.AppendLine("Timestamp,Angle,HC1_X,HC1_Y,HC2_X,HC2_Y");
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            foreach (var r in results)
            {
                sb.AppendLine($"{timestamp},{r.angle:F1},{r.hc1X},{r.hc1Y},{r.hc2X},{r.hc2Y}");
            }

            await File.AppendAllTextAsync(path, sb.ToString(), ct);
        }
        //public async Task Bonding(ObservableCollection<BondingDataPoint> bondingDataPoints, CancellationToken ct)
        //{
        //    var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
        //    try
        //    {
        //        double topDieThickness = await GetRecipe("TopDieThickness");
        //        double btmDieThickness = await GetRecipe("BtmDieThickness");
        //        double shankToWaferOffset = await GetRecipe("ShankToWaferOffset");
        //        double readyPosition = await GetRecipe("READY_POSITION");
        //        int accTime = await GetRecipeInt("ACC_TIME");
        //        int contTime = await GetRecipeInt("CONT_TIME");
        //        int decTime = await GetRecipeInt("DEC_TIME");
        //        double loadCell = await GetRecipe("LOADCELL");
        //        double current = await GetRecipe("CURRENT");
        //        int vacOffMs = await GetRecipeInt("VAC_OFF_TIME");   // Vacuum OFF 시점 (ms)

        //        await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - readyPosition, ct);
        //        await Task.Delay(200, ct);
        //        await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={accTime}");
        //        await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={contTime}");
        //        await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={decTime}");
        //        await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={loadCell}");
        //        await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={current}");
        //        await device.SendCommand(MotionExtensions.BONDING_START + $"=1");

        //        const int pollingIntervalMs = 100;
        //        int timeoutMs = accTime + contTime + decTime + 2000;
        //        var sw = Stopwatch.StartNew();
        //        bool bondingComplete = false;
        //        bool vacuumOff = false;

        //        bondingDataPoints.Clear();

        //        while (!bondingComplete)
        //        {
        //            ct.ThrowIfCancellationRequested();

        //            // 설정 시점에 Vacuum OFF
        //            if (!vacuumOff && sw.ElapsedMilliseconds >= vacOffMs)
        //            {
        //                await HVacOnOff(false, ct);
        //                vacuumOff = true;
        //                _logger.Information("Vacuum OFF ({Elapsed}ms, 설정={VacOffMs}ms)",
        //                    sw.ElapsedMilliseconds, vacOffMs);
        //            }

        //            double forceValue = 0;
        //            string analog = await device.SendCommand<string>(MotionExtensions.ANALOG_INPUT);
        //            if (double.TryParse(analog.Trim(), out forceValue))
        //            {
        //                bondingDataPoints.Add(new BondingDataPoint
        //                {
        //                    TimeS = sw.Elapsed.TotalSeconds,
        //                    ForceN = forceValue * 0.00373
        //                });
        //            }
        //            else
        //            {
        //                _logger.Warning("AnalogInput 파싱 실패: {Response}", analog);
        //            }

        //            string strResponse = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
        //            var values = strResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        //            if (values.Length > 0 && bool.TryParse(values[0], out bool result))
        //            {
        //                _logger.Information("Bonding 상태: {Result} | Force: {Force:F3}N (경과: {Elapsed}ms)",
        //                    result, forceValue, sw.ElapsedMilliseconds);
        //                bondingComplete = result;
        //            }
        //            else
        //            {
        //                _logger.Warning("Bonding 상태 응답 파싱 실패: {Response}", strResponse);
        //            }

        //            if (!bondingComplete)
        //            {
        //                if (sw.ElapsedMilliseconds > timeoutMs)
        //                    throw new TimeoutException($"Bonding 완료 대기 시간 초과 ({timeoutMs}ms)");

        //                await Task.Delay(pollingIntervalMs, ct);
        //            }
        //        }

        //        sw.Stop();
        //        _logger.Information("Bonding 완료 (총 소요: {Elapsed}ms, 수집 포인트: {Count}개)",
        //            sw.ElapsedMilliseconds, bondingDataPoints.Count);
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.Warning("Bonding 작업이 취소되었습니다.");
        //        throw;
        //    }
        //    catch (TimeoutException ex)
        //    {
        //        _logger.Error(ex, "Bonding 타임아웃");
        //    }
        //    catch (Exception e)
        //    {
        //        _logger.Error(e, "Bonding 실패");
        //        throw;
        //    }
        //    finally
        //    {
        //        try
        //        {
        //            await device.SendCommand(MotionExtensions.BONDING_START + $"=0");
        //            await device.SendCommand(MotionExtensions.BONDING_INIT + $"=1");
        //            await Task.Delay(100);
        //            await device.SendCommand(MotionExtensions.BONDING_INIT + $"=0");
        //            await Init_Head(ct);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.Error(ex, "Bonding 초기화 실패");
        //        }
        //    }
        //}

        public async Task Bonding(AlignData data, ObservableCollection<BondingDataPoint> bondingDataPoints, CancellationToken ct)
        {
            var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            try
            {
                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = await GetRecipe("ShankToWaferOffset");
                double readyPosition = await GetRecipe("READY_POSITION");
                int accTime = await GetRecipeInt("ACC_TIME");
                int contTime = await GetRecipeInt("CONT_TIME");
                int decTime = await GetRecipeInt("DEC_TIME");
                double loadCell = await GetRecipe("LOADCELL");
                double current = await GetRecipe("CURRENT");
                int vacOffMs = await GetRecipeInt("VAC_OFF_TIME");   // Vacuum OFF 시점 (ms)

                await Task.WhenAll(
                    RelativeMotionsMove(MotionExtensions.H_X, -data.ResultX, ct),
                    RelativeMotionsMove(MotionExtensions.W_Y, -data.ResultY, ct),
                    RelativeMotionsMove(MotionExtensions.H_T, -data.ResultT, ct)
                );

                await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - readyPosition, ct);
                await Task.Delay(200, ct);
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={accTime}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={contTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={decTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={loadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={current}");
                await device.SendCommand(MotionExtensions.BONDING_START + $"=1");

                // Polling으로 본딩 완료 상태 + LoadCell 데이터 추적
                const int pollingIntervalMs = 100;
                int timeoutMs = accTime + contTime + decTime + 2000; // 폴링 오버헤드 마진
                var sw = Stopwatch.StartNew();
                bool bondingComplete = false;
                bool vacuumOff = false;

                bondingDataPoints.Clear();

                while (!bondingComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    // 설정 시점에 Vacuum OFF
                    if (!vacuumOff && sw.ElapsedMilliseconds >= vacOffMs)
                    {
                        await HVacOnOff(false, ct);
                        vacuumOff = true;
                        _logger.Information("Vacuum OFF ({Elapsed}ms, 설정={VacOffMs}ms)",
                            sw.ElapsedMilliseconds, vacOffMs);
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

                    if (values.Length > 0 && bool.TryParse(values[0], out bool result))
                    {
                        _logger.Information("Bonding 상태: {Result} | Force: {Force:F3}N (경과: {Elapsed}ms)",
                            result, forceValue, sw.ElapsedMilliseconds);
                        bondingComplete = result;
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
                _logger.Information("Bonding 완료 (총 소요: {Elapsed}ms, 수집 포인트: {Count}개)",
                    sw.ElapsedMilliseconds, bondingDataPoints.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Bonding 작업이 취소되었습니다.");
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.Error(ex, "Bonding 타임아웃");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Bonding 실패");
                throw;
            }
            finally
            {
                try
                {
                    await device.SendCommand(MotionExtensions.BONDING_START + $"=0");
                    await device.SendCommand(MotionExtensions.BONDING_INIT + $"=1");
                    await Task.Delay(100);
                    await device.SendCommand(MotionExtensions.BONDING_INIT + $"=0");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Bonding 초기화 실패");
                }
            }
        }

        public async Task BondingTest(CancellationToken ct)
        {
            var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            try
            {
                double topDieThickness = await GetRecipe("TopDieThickness");
                double btmDieThickness = await GetRecipe("BtmDieThickness");
                double shankToWaferOffset = await GetRecipe("ShankToWaferOffset");
                double readyPosition = await GetRecipe("READY_POSITION");
                int accTime = await GetRecipeInt("ACC_TIME");
                int contTime = await GetRecipeInt("CONT_TIME");
                int decTime = await GetRecipeInt("DEC_TIME");
                double loadCell = await GetRecipe("LOADCELL");
                double current = await GetRecipe("CURRENT");

                await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - readyPosition, ct);
                await Task.Delay(200, ct);
                await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={accTime}");
                await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={contTime}");
                await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={decTime}");
                await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={loadCell}");
                await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={current}");
                await device.SendCommand(MotionExtensions.BONDING_START + $"=1");

                // Polling으로 본딩 완료 상태 + LoadCell 데이터 추적
                const int pollingIntervalMs = 100;
                int timeoutMs = accTime + contTime + decTime + 2000; // 폴링 오버헤드 마진
                var sw = Stopwatch.StartNew();
                bool bondingComplete = false;
                bool vacuumOff = false;

                while (!bondingComplete)
                {
                    ct.ThrowIfCancellationRequested();

                    // AccTime 중간 시점에 Vacuum OFF
                    if (!vacuumOff && sw.ElapsedMilliseconds >= accTime / 2)
                    {
                        await HVacOnOff(true, ct);
                        vacuumOff = true;
                        _logger.Information("AccTime 중간 → Vacuum OFF ({Elapsed}ms)", sw.ElapsedMilliseconds);
                    }

                    // LoadCell 아날로그 값 읽기
                    double forceValue = 0;
                    string analog = await device.SendCommand<string>(MotionExtensions.ANALOG_INPUT);
                    if (double.TryParse(analog.Trim(), out forceValue))
                    {
                        
                    }
                    else
                    {
                        _logger.Warning("AnalogInput 파싱 실패: {Response}", analog);
                    }

                    // 본딩 완료 상태 확인
                    string strResponse = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                    var values = strResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length > 0 && bool.TryParse(values[0], out bool result))
                    {
                        _logger.Information("Bonding 상태: {Result} | Force: {Force:F3}N (경과: {Elapsed}ms)",
                            result, forceValue, sw.ElapsedMilliseconds);
                        bondingComplete = result;
                    }
                    else
                    {
                        _logger.Warning("Bonding 상태 응답 파싱 실패: {Response}", strResponse);
                    }

                    // 완료되지 않았을 때만 타임아웃 체크 + 대기
                    if (!bondingComplete)
                    {
                        if (sw.ElapsedMilliseconds > timeoutMs)
                            throw new TimeoutException($"Bonding 완료 대기 시간 초과 ({timeoutMs}ms)");

                        await Task.Delay(pollingIntervalMs, ct);
                    }
                }

                sw.Stop();
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Bonding 작업이 취소되었습니다.");
                throw;
            }
            catch (TimeoutException ex)
            {
                _logger.Error(ex, "Bonding 타임아웃");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Bonding 실패");
                throw;
            }
            finally
            {
                try
                {
                    await device.SendCommand(MotionExtensions.BONDING_START + $"=0");
                    await device.SendCommand(MotionExtensions.BONDING_INIT + $"=1");
                    await Task.Delay(100);
                    await device.SendCommand(MotionExtensions.BONDING_INIT + $"=0");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Bonding 초기화 실패");
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
            if (response.Result == Result.NG) throw new Exception("비전 통신 에러");
        }
    }
}