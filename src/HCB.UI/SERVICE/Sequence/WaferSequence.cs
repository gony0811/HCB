using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Fixed.Model.Actions;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        public async Task<bool> WaferLoad(CancellationToken ct)
        {
            var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            string[] readyAxis = { "H_Z", "h_z", "H_X"};
            string loadingAxis = "W_Y";
            try
            {
                // Set Ready
                for(int i =0; i < readyAxis.Length; i++)
                {
                    var motion = motionDevice.FindMotionByName(readyAxis[i]);
                    var position = motion.PositionList.FirstOrDefault(x => x.Name == "READY");
                    if (position == null) throw new Exception($"{readyAxis[i]} Motion에 READY Position을 등록해주세요");
                    await position.AbsoluteMove();

                    await _sequenceHelper.WaitUntilAsync(() =>
                    {
                        return motion.InPosition;
                    }, 60000, ct, $"[Initialize] {motion.Name} 축이 READY Position에 도달하지 못했습니다");
                }

                // Set Load
                var wyAxis = motionDevice.FindMotionByName(loadingAxis);
                var wyLoadPosition = wyAxis.PositionList.FirstOrDefault(x => x.Name == "READY");
                if (wyLoadPosition == null) throw new Exception($"{loadingAxis} Motion에 READY Position을 등록해주세요");
                await wyLoadPosition.AbsoluteMove();
                await _sequenceHelper.WaitUntilAsync(() =>
                {
                    return wyAxis.InPosition;
                }, 60000, ct, $"[Initialize] {wyAxis.Name} 축이 READY Position에 도달하지 못했습니다");

                // Wafer Vacuum off
                string[] waferVac =
                {
                    IoExtensions.DO_WTABLE_VAC_1_ON, IoExtensions.DO_WTABLE_VAC_2_ON, IoExtensions.DO_WTABLE_VAC_3_ON, IoExtensions.DO_WTABLE_VAC_4_ON, IoExtensions.DO_WTABLE_VAC_5_ON
                };
                for (int i = 0; i < waferVac.Length; i++)
                {
                    ioDevice.SetDigital(waferVac[i], false);  
                }

                // Wafer Pin Up
                bool pinDown = await ioDevice.SetDigitalAsync(IoExtensions.DI_WTABLE_LIFT_PIN_DOWN, false);
                if (!pinDown) throw new Exception("[Initialize] Wafer Loading Pin DOWN 상태 해제 실패");

                bool pinUp = await ioDevice.SetDigitalAsync(IoExtensions.DI_WTABLE_LIFT_PIN_UP, true);
                if (!pinUp) throw new Exception("[Initialize] Wafer Loading Pin UP 동작 실패");

                return true;
            }
            catch(Exception e)
            {
                _logger.Error($"{e.Message}");
                _logger.Error("Wafer Load에 실패했습니다. 작업을 종료합니다");
                return false;
            }
        }
    }
}
