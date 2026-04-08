using Microsoft.Extensions.Hosting;
using SharpDX;
using System;
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

        //public async Task DTablePickup(int vacNum, VisionMarkPositionResponse? correction, CancellationToken ct)
        //{
        //    try
        //    {
        //        _logger.Information("Die pickup Start");
        //        EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

        //        var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);                
        //        //string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };

        //        string[] xy = { MotionExtensions.H_X, MotionExtensions.D_Y};

        //        await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
        //        var goPickup = await Task.WhenAll(
        //            _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, 150.553, ct),
        //            _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, -44.575, ct)
        //            //MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct)
        //        );

        //        if (!goPickup.All(r => r)) throw new Exception("픽업 위치로 이동 실패");

        //        // 보정값만큼 상대 이동
        //        var results = await Task.WhenAll(
        //            _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, -correction?.X ?? 0, ct),
        //            _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, -correction?.Y ?? 0, ct),
        //            _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_T, 0, -correction?.Theta ?? 0, ct)
        //        );

        //        if (!results.All(r => r)) throw new Exception("보정 실패");

        //        await MotionsMove(MotionExtensions.H_Z, MotionExtensions.DIE_PICKUP, ct);
        //        await MotionsMove(MotionExtensions.h_z, MotionExtensions.DIE_PICKUP, ct);
        //        var headPicker = await _sequenceHelper.HeadPickerVacuum(eOnOff.On, ct);
        //        await _sequenceHelper.DTableVacuum(vacNum, eOnOff.Off, ct);
        //        if (!headPicker) throw new Exception("Head에 Pick된 Die가 없습니다");
        //        await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
        //        await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception(e.Message);
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

                if (double.TryParse(_recipeService.FindByParam("TopDieThickness").Value, out double topDieThickness))
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
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 200, xOffset - correction.X, ct),
                    _sequenceHelper.RelativeMoveAsync(MotionExtensions.D_Y, 200, yOffset - correction.Y, ct)
                );

                await MotionsMove(MotionExtensions.H_T, MotionExtensions.ORIGIN, ct);
                if (!goPickup.All(r => r)) throw new Exception("픽업 위치로 이동 실패");
                await _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_T, 0, -correction?.Theta ?? 0, ct);

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
