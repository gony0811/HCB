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

        // Btm Die 저배율 측정
        public async Task<VisionMarkPositionResponse> BtmLowMeasure(int vacNum, MarkType markType, CancellationToken ct)
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
            try
            {
                data.TopRightFidRaw = await TopDieVisionRightFid(data.AvgMove, ct);
                if (data.Use2DMapping) await PTable2DMappingOn();
                data.TopRightAlignRaw = await TopDieVisionRightAlign(data.AvgMove, ct);
                data.TopLeftFidRaw = await TopDieVisionLeftFid(data.AvgMove, ct);
                data.TopLeftAlignRaw = await TopDieVisionLeftAlign(data.AvgMove, ct);
            }
            catch (ErrorException e)
            {
                throw;
            }
            finally
            {
                if (data.Use2DMapping) await PTable2DMappingOff();
            }
            return data;
        }

        #endregion

        #region Btm Die 고배율 측정

        public async Task<AlignData> BtmHighAlign(
            AlignData data, CancellationToken ct)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            await TopDieSet(ct);

            data.BtmRightFidRaw = await BtmDieVisionRightFid(data.AvgMove, ct);
            data.BtmRightAlignRaw = await BtmDieVisionRightAlign(data.AvgMove, ct);
            data.BtmLeftFidRaw = await BtmDieVisionLeftFid(data.AvgMove, ct);
            data.BtmLeftAlignRaw = await BtmDieVisionLeftAlign(data.AvgMove, ct);
            return data;
        }

        #endregion

    }
}
