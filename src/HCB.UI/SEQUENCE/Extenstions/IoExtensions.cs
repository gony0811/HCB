using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public static class IoExtensions
    {
        public const string IoDeviceName = "PmacIO";


        #region Digital Input Names
        public const string DI_EMO_1_SWITCH = "DI_EMO_1_SWITCH";
        public const string DI_EMO_2_SWITCH = "DI_EMO_2_SWITCH";
        public const string DI_START_SWITCH = "DI_START_SWITCH";
        public const string DI_STOP_SWITCH = "DI_STOP_SWITCH";
        public const string DI_RESET_SWITCH = "DI_RESET_SWITCH";
        #endregion


        public static async Task Servo(this ISequenceHelper helper, int axisId, bool ready, CancellationToken ct)
        {
            //var data = helper.DeviceManager.GetDevice<PmacIoDevice>(IoDeviceName).FindIoDataByName();
            //if (data == null)
            //{
            //    helper.Log(LogLevel.Critical, $"Axis with ID {axisId} not found.");
            //}
            //if (ready)
            //{
            //    await axis.ServoReady(ready);
            //    await helper.DelayAsync(100, ct); // Small delay to ensure the servo on command is processed
            //    await helper.WaitUntilAsync(
            //        () => axis.IsEnabled,
            //        3000,
            //        ct,
            //        $"Axis {axis.Name} Servo On Timeout"
            //    );
            //}
            //else
            //{
            //    await axis.ServoReady(ready);
            //    await helper.DelayAsync(100, ct); // Small delay to ensure the servo off command is processed
            //    await helper.WaitUntilAsync(
            //        () => !axis.IsEnabled,
            //        3000,
            //        ct,
            //        $"Axis {axis.Name} Servo Off Timeout"
            //    );
            //}
        }
    }
}
