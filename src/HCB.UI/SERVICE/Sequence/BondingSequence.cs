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
        public async Task BondingAlign(CancellationToken ct)
        {

            try
            {
                _logger.Information("Bonding Align Start");
                //EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                string[] xy = { MotionExtensions.W_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.BONDING_ALIGN_1, ct);
                await MotionsMove(z, MotionExtensions.BONDING_ALIGN_1, ct);

                // TODO : VISION I/F & BOND ALIGN

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.BONDING_ALIGN_2, ct);
                await MotionsMove(z, MotionExtensions.BONDING_ALIGN_2, ct);

                // TODO : VISION I/F & BOND ALIGN
            }
            catch (Exception e)
            {

            }
        }

        public async Task Bonding(CancellationToken ct)
        {

            try
            {
                _logger.Information("Bonding Start");
                //EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                string[] xy = { MotionExtensions.W_Y, MotionExtensions.H_X };
                //string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };
                string[] z = { MotionExtensions.H_Z};

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.BONDING, ct);
                
                // TODO : BONDING 
            }
            catch (Exception e)
            {

            }finally
            {
                _logger.Information("Bonding End");
            }
        }
    }
}
