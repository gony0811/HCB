using MediaFoundation;
using Microsoft.Extensions.Hosting;
using SharpDX;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public async Task<VisionMarkPositionResponse> DTableCarrierAlign(int vacNum, MarkType markType, CancellationToken ct)
        {
            try
            {
                _logger.Information("Die Align 요청 Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                string[] xy = { MotionExtensions.D_Y, MotionExtensions.H_X };
                //string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };

                // 안전한 위치 셋업
                await Init_Head(ct);
                _logger.Information("Die Align 시작");
                await MotionsMove(xy, $"DIE_ALIGN_{vacNum}", ct);
                await MotionsMove(MotionExtensions.H_Z, MotionExtensions.DIE_VISION_LOW, ct);
                
                var diePickupAlign = await communicationService.RequestVisionMarkPosition(markType, CameraType.HC_LOW, "");
                
                _logger.Information("Die Align 종료");
                return diePickupAlign;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkPositionResponse> BtmCarrierAlign(int vacNum, MarkType markType, CancellationToken ct)
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
                    MotionsMove(MotionExtensions.H_X, $"DIE_BTM", ct),
                    MotionsMove(MotionExtensions.D_Y, $"DIE_ROW_{vacNum}", ct)
                );
                await MotionsMove(MotionExtensions.H_Z, MotionExtensions.DIE_VISION_LOW, ct);

                var diePickupAlign = await communicationService.RequestVisionMarkPosition(markType, CameraType.HC_LOW, "");

                _logger.Information("Die Align 종료");
                return diePickupAlign;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<VisionMarkPositionResponse> TopCarrierAlign(int vacNum, MarkType markType, CancellationToken ct)
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

                var diePickupAlign = await communicationService.RequestVisionMarkPosition(markType, CameraType.HC_LOW, "");

                _logger.Information("Die Align 종료");
                return diePickupAlign;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        //public async Task Pickup(int vacNum, string dieType, VisionMarkPositionResponse? correction, CancellationToken ct)
        //{
        //    _logger.Information("Die pickup Start");
        //    EQStatusCheck();
        //    if (double.TryParse(_recipeService.FindByParam("ShankLowOffsetX").Value, out double xOffset))
        //    { }
        //    else
        //    {
        //        throw new Exception("레시피 ShankLowOffsetX값이 Double타입이 아닙니다");
        //    }

        //    if (double.TryParse(_recipeService.FindByParam("ShankLowOffsetY").Value, out double yOffset))
        //    { }
        //    else
        //    {
        //        throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
        //    }
        //    if (double.TryParse(_recipeService.FindByParam("BtmDieThickness").Value, out double btmDieThickness))
        //    { }
        //    else
        //    {
        //        throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
        //    }
        //    if (double.TryParse(_recipeService.FindByParam("ShankToDieOffset").Value, out double ShankToDieOffset))
        //    { }
        //    else
        //    {
        //        throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
        //    }

        //    var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);


        //    try
        //    {
        //        double xOffset = await GetRecipe("ShankLowOffsetX");
        //        double yOffset = await GetRecipe("ShankLowOffsetY");
        //        double topDieThickness = await GetRecipe("TopDieThickness");
        //        double btmDieThickness = await GetRecipe("BtmDieThickness");
        //        double shankToWaferOffset = await GetRecipe("ShankToWaferOffset");
        //        double readyPosition = await GetRecipe("READY_POSITION");
        //        int accTime = await GetRecipeInt("ACC_TIME");
        //        int contTime = await GetRecipeInt("CONT_TIME");
        //        int decTime = await GetRecipeInt("DEC_TIME");
        //        double loadCell = await GetRecipe("LOADCELL");
        //        double current = await GetRecipe("CURRENT");


        //        await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
        //        var goPickup = await Task.WhenAll(
        //            _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, xOffset, ct),
        //            _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, yOffset, ct)
        //        );

        //        await MotionsMove(MotionExtensions.H_Z, shankToWaferOffset - topDieThickness - btmDieThickness - readyPosition, ct);
        //        await Task.Delay(200, ct);
        //        await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={accTime}");
        //        await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={contTime}");
        //        await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={decTime}");
        //        await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={loadCell}");
        //        await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={current}");
        //        await device.SendCommand(MotionExtensions.BONDING_START + $"=1");

        //        // Polling으로 본딩 완료 상태 + LoadCell 데이터 추적
        //        const int pollingIntervalMs = 100;
        //        int timeoutMs = accTime + contTime + decTime + 2000; // 폴링 오버헤드 마진
        //        var sw = Stopwatch.StartNew();
        //        bool bondingComplete = false;
        //        bool vacuumOff = false;

        //        bondingDataPoints.Clear();

        //        while (!bondingComplete)
        //        {
        //            ct.ThrowIfCancellationRequested();

        //            // AccTime 중간 시점에 Vacuum OFF
        //            if (!vacuumOff && sw.ElapsedMilliseconds >= accTime / 2)
        //            {
        //                await HVacOnOff(false, ct);
        //                vacuumOff = true;
        //                _logger.Information("AccTime 중간 → Vacuum OFF ({Elapsed}ms)", sw.ElapsedMilliseconds);
        //            }

        //            // LoadCell 아날로그 값 읽기
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

        //            // 본딩 완료 상태 확인
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

        //            // 완료되지 않았을 때만 타임아웃 체크 + 대기
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
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.Error(ex, "Bonding 초기화 실패");
        //        }
        //    }
        //}


        public async Task DTableBTMPickup(int vacNum, VisionMarkPositionResponse? correction, CancellationToken ct)
        {
            try
            {
                _logger.Information("Die pickup Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);;
                string[] xy = { MotionExtensions.H_X, MotionExtensions.D_Y };
                if (double.TryParse(_recipeService.FindByParam("ShankLowOffsetX").Value, out double xOffset))
                { }
                else
                {
                    throw new Exception("레시피 ShankLowOffsetX값이 Double타입이 아닙니다");
                }

                if (double.TryParse(_recipeService.FindByParam("ShankLowOffsetY").Value, out double yOffset))
                { }
                else
                {
                    throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
                }
                if (double.TryParse(_recipeService.FindByParam("BtmDieThickness").Value, out double btmDieThickness))
                { }
                else
                {
                    throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
                }
                if (double.TryParse(_recipeService.FindByParam("ShankToDieOffset").Value, out double ShankToDieOffset))
                { }
                else
                {
                    throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
                }

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                var goPickup = await Task.WhenAll(
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, xOffset, ct),
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, yOffset, ct)
                );
                await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
                if (!goPickup.All(r => r)) throw new Exception("픽업 위치로 이동 실패");

                // 보정값만큼 상대 이동
                var results = await Task.WhenAll(
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_T, 0, -correction?.Theta ?? 0, ct),
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, -correction?.X ?? 0, ct),
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, -correction?.Y ?? 0, ct)
                );

                if (!results.All(r => r)) throw new Exception("보정 실패");

                await MotionsMove(MotionExtensions.H_Z, ShankToDieOffset-btmDieThickness, ct);

                var headPicker = await _sequenceHelper.HeadPickerVacuum(eOnOff.On, ct);
                await Task.Delay(1000);
                await _sequenceHelper.BTMVac(vacNum, eOnOff.Off, ct);
                await Task.Delay(2000);
                if (!headPicker) throw new Exception("Head에 Pick된 Die가 없습니다");
                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task DTableTOPPickup(int vacNum, VisionMarkPositionResponse? correction, CancellationToken ct)
        {
            try
            {
                _logger.Information("Die pickup Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                string[] xy = { MotionExtensions.H_X, MotionExtensions.D_Y };
                int accTime = await GetRecipeInt("ACC_TIME");
                int contTime = await GetRecipeInt("CONT_TIME");
                int decTime = await GetRecipeInt("DEC_TIME");
                double loadCell = await GetRecipe("LOADCELL");
                double current = await GetRecipe("CURRENT");
                int vacOffMs = await GetRecipeInt("VAC_OFF_TIME");
                double xOffset = await GetRecipe("ShankLowOffsetX");
                double yOffset = await GetRecipe("ShankLowOffsetY");
                double topDieThickness = await GetRecipe("TopDieThickness");
                double ShankToDieOffset = await GetRecipe("ShankToDieOffset");


                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                var goPickup = await Task.WhenAll(
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, xOffset - correction.X, ct),
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, yOffset - correction.Y, ct)
                );

                await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
                if (!goPickup.All(r => r)) throw new Exception("픽업 위치로 이동 실패");
                await _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_T, 0, -correction?.Theta ?? 0, ct);

                // pick up
                //await Task.Delay(200, ct);
                //var device = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                //await device.SendCommand(MotionExtensions.BONDING_ACC_TIME + $"={accTime}");
                //await device.SendCommand(MotionExtensions.BONDING_CONT_TIME + $"={contTime}");
                //await device.SendCommand(MotionExtensions.BONDING_DEC_TIME + $"={decTime}");
                //await device.SendCommand(MotionExtensions.BONDING_LOADCELL + $"={loadCell}");
                //await device.SendCommand(MotionExtensions.BONDING_CURRENT + $"={current}");
                //await device.SendCommand(MotionExtensions.BONDING_START + $"=1");
                //// Polling으로 본딩 완료 상태 + LoadCell 데이터 추적
                //const int pollingIntervalMs = 100;
                //int timeoutMs = accTime + contTime + decTime + 2000; // 폴링 오버헤드 마진
                //var sw = Stopwatch.StartNew();
                //bool bondingComplete = false;
                //bool vacuumOff = false;

                //while (!bondingComplete)
                //{
                //    ct.ThrowIfCancellationRequested();

                //    // 설정 시점에 Vacuum OFF
                //    if (!vacuumOff && sw.ElapsedMilliseconds >= vacOffMs)
                //    {
                //        await HVacOnOff(false, ct);
                //        vacuumOff = true;
                //        _logger.Information("Vacuum OFF ({Elapsed}ms, 설정={VacOffMs}ms)",
                //            sw.ElapsedMilliseconds, vacOffMs);
                //    }

                //    double forceValue = 0;
                //    string analog = await device.SendCommand<string>(MotionExtensions.ANALOG_INPUT);


                //    string strResponse = await device.SendCommand<string>(MotionExtensions.BONDING_STATUS_COMPLETE);
                //    var values = strResponse.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                //    if (values.Length > 0 && bool.TryParse(values[0], out bool result))
                //    {
                //        _logger.Information("Bonding 상태: {Result} | Force: {Force:F3}N (경과: {Elapsed}ms)",
                //            result, forceValue, sw.ElapsedMilliseconds);
                //        bondingComplete = result;
                //    }
                //    else
                //    {
                //        _logger.Warning("Bonding 상태 응답 파싱 실패: {Response}", strResponse);
                //    }

                //    if (!bondingComplete)
                //    {
                //        if (sw.ElapsedMilliseconds > timeoutMs)
                //            throw new TimeoutException($"Bonding 완료 대기 시간 초과 ({timeoutMs}ms)");

                //        await Task.Delay(pollingIntervalMs, ct);
                //    }
                //}

                await MotionsMove(MotionExtensions.H_Z, ShankToDieOffset-topDieThickness, ct);

                var headPicker = await _sequenceHelper.HeadPickerVacuum(eOnOff.On, ct);
                await _sequenceHelper.TopVac(vacNum, eOnOff.Off, ct);
                await Task.Delay(2000);
                if (!headPicker) throw new Exception("Head에 Pick된 Die가 없습니다");

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

        }

        //public async Task DTableCarrierAlign(CancellationToken ct)
        //{
        //    try
        //    {
        //        _logger.Information("Die Carrier Align Start");
        //        //EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

        //        var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

        //        string[] xy = { MotionExtensions.D_Y, MotionExtensions.H_X };
        //        string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };
        //        //string[] z = { MotionExtensions.H_Z};

        //        // DIE CARRIER ALIGN 1  위치로 이동 

        //        await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_1, ct);
        //        await MotionsMove(z, MotionExtensions.DIE_CARRIER_ALIGN_LOW, ct);

        //        // TODO: 비전 측정

        //        // DIE CARRIER ALIGN 2  위치로 이동 
        //        await Init_Head(ct);
        //        await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_2, ct);
        //        await MotionsMove(z, MotionExtensions.DIE_CARRIER_ALIGN_LOW, ct);

        //        // TODO: 비전 측정

        //        // DIE CARRIER ALIGN 2  위치로 이동 
        //        await Init_Head(ct);
        //        await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_3, ct);
        //        await MotionsMove(z, MotionExtensions.DIE_CARRIER_ALIGN_LOW, ct);
        //        // TODO: 비전 측정
        //        await Init_Head(ct);

        //        // TODO: 오차 보정

        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception(e.Message);
        //    }

        //}

    }
}
