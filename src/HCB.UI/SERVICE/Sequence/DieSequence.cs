using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        public async Task DTableLoading(CancellationToken ct)
        {
            try
            {
                _logger.Information("Die Loading Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동

                var DY = motionDevice?.FindMotionByName(MotionExtensions.D_Y);
                var HX = motionDevice?.FindMotionByName(MotionExtensions.H_X);

                if (DY == null) throw new Exception("D Table Y axis not found in motion device.");
                if (HX == null) throw new Exception("H Table X axis not found in motion device.");

                Task moveHX = _sequenceHelper.MoveAsync(HX.MotorNo, MotionExtensions.DIE_LOADING, ct);
                Task moveDY = _sequenceHelper.MoveAsync(DY.MotorNo, MotionExtensions.DIE_LOADING, ct);

                // 작업 동시에 수행
                await Task.WhenAll(moveHX, moveDY);

                await Task.Delay(3000, ct);

                // Vacuum Off
                await _sequenceHelper.DTableVacuumAll(eOnOff.Off, ct);

            }
            catch (OperationCanceledException)
            {
                _logger.Information("Die Loading Canceled");
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                return;
            }
            finally
            {
                _logger.Information("Die Loading End");
            }
        }

        public async Task DTableCarrierAlign(CancellationToken ct)
        {
            try
            {
                _logger.Information("Die Carrier Align Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                string[] xy = { MotionExtensions.D_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };

                // DIE CARRIER ALIGN 1  위치로 이동 
                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_1, ct);
                await MotionsMove(z, MotionExtensions.DIE_CARRIER_ALIGN_LOW, ct);

                // TODO: 비전 측정

                // DIE CARRIER ALIGN 2  위치로 이동 
                await Init_Head(ct);
                await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_2, ct);
                await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);

                // TODO: 비전 측정

                // DIE CARRIER ALIGN 2  위치로 이동 
                await Init_Head(ct);
                await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_3, ct);
                await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);
                // TODO: 비전 측정
                await Init_Head(ct);

                // TODO: 오차 보정

            }
            catch (Exception e)
            {

            }

        }

        public async Task DTablePickup(CancellationToken ct)
        {
            //try
            //{
            //    _logger.Information("Die pickup Start");
            //    EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

            //    var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

            //    string[] xy = { MotionExtensions.D_Y, MotionExtensions.H_X };
            //    string[] t = { MotionExtensions.H_T };
            //    string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };

            //    // DIE CARRIER ALIGN 1  위치로 이동 
            //    await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
            //    await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_1, ct);
            //    await MotionsMove(z, MotionExtensions.DIE_CARRIER_ALIGN_LOW, ct);

            //    // TODO: 비전 측정

            //    // DIE CARRIER ALIGN 2  위치로 이동 
            //    await Init_Head(ct);
            //    await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_2, ct);
            //    await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);

            //    // TODO: 비전 측정

            //    // DIE CARRIER ALIGN 2  위치로 이동 
            //    await Init_Head(ct);
            //    await MotionsMove(xy, MotionExtensions.DIE_CARRIER_ALIGN_3, ct);
            //    await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);
            //    // TODO: 비전 측정
            //    await Init_Head(ct);

            //    // TODO: 오차 보정

            //}
            //catch (Exception e)
            //{

            //}

        }

    }
}
