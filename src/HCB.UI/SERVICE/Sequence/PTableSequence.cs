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
        public async Task BottomVision(CancellationToken ct)
        {

            try
            {
                _logger.Information("Bottom Vision Start");
                //EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                
                string[] xy = { MotionExtensions.P_Y, MotionExtensions.H_X };
                //string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };
                string[] z = { MotionExtensions.H_Z };

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.BTM_VISION_LOW, ct);
                await MotionsMove(z, MotionExtensions.BTM_VISION_LOW, ct);

                // TODO : 오토 포커스
                // TODO: VISION I/F & PICKUPED DIE 자세 확인

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.BTM_VISION_HIGH, ct);
                await MotionsMove(z, MotionExtensions.BTM_VISION_HIGH, ct);

                // TODO : 오토 포커스
                // TODO: VISION I/F & PICKUPED DIE 자세 확인
            }
            catch (Exception e)
            {

            }
        }
    }
}
