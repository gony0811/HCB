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
                await MotionsMove(z, MotionExtensions.BONDING, ct);
                
                // TODO : BONDING 
            }
            catch (Exception e)
            {

            }finally
            {
                _logger.Information("Bonding End");
            }
        }

        //public async Task TopDieDrop(CancellationToken ct)
        //{
        //    bool result = false;
        //    VisionMarkPositionResponse waferLeftAlign;
        //    VisionMarkPositionResponse waferRightAlign;

        //    await Init_Head(ct);            
        //    await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_LEFT", ct);
        //    await MotionsMove(MotionExtensions.H_Z, "W_TABLE_CORNER_SHOT", ct);
        //    result = await communicationService.RequestAFStart(CameraType.HC1_HIGH, ct);
        //    if (!result) throw new Exception("AF Failed");
        //    waferLeftAlign = await communicationService.RequestVisionMarkPosition(MarkType.CORNER, CameraType.HC1_HIGH,);

        //    await Init_Head(ct);
        //    await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_RIGHT", ct); 
        //    await MotionsMove(MotionExtensions.H_Z, "W_TABLE_CORNER_SHOT", ct);
        //    result = await communicationService.RequestAFStart(CameraType.HC2_HIGH, ct);
        //    if (!result) throw new Exception("AF Failed");
        //    waferRightAlign = await communicationService.RequestVisionMarkPosition(MarkType.CORNER, CameraType.HC2_HIGH);

        //    await Init_Head(ct);
        //    await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);

        //    // TODO: 보정
        //    await MotionsMove(MotionExtensions.H_Z, "TOP_DIE_DROP", ct);
        //    await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, ct);
        //}

        public async Task BtmDieDrop(int vacNum, CancellationToken ct)
        {
            // W-Table로 이동
            await Init_Head(ct);
            await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
            await MotionsMove(MotionExtensions.H_Z, "PLACE_STANBY", ct);
            await MotionsMove(MotionExtensions.H_Z, "BTM_DIE_PLACE", ct);
            await _sequenceHelper.WTableVacuum(vacNum, eOnOff.On, ct);
            bool result = await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, ct);
            if (!result) throw new Exception("HeadPicker를 확인해주세요");
            
        }
    }
}
