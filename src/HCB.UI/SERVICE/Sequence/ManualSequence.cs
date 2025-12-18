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
        public const string LOAD_POSITION = "LOAD";
        public async Task DTableLoading(CancellationToken ct)
        {
            try
            {
                if (this._sequenceServiceVM.IsMachineInitialized == false)
                {
                    throw new Exception("Machine is not initialized. Please initialize the machine before DTable Loading.");
                }

                this._sequenceServiceVM.StatusMessage = "DTable Loading Start";

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                var d_y = motionDevice?.FindMotionByName(MotionExtensions.D_Y); // D Table Y축 (예시)
                var H_X = motionDevice?.FindMotionByName(MotionExtensions.H_X); // H Table X축 (예시)
                var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // H Table Z축 (예시)

                if (d_y == null) throw new Exception("D Table Y axis not found in motion device.");           
                if (H_X == null) throw new Exception("H Table X axis not found in motion device.");
                if (H_Z == null) throw new Exception("H Table Z axis not found in motion device.");

                await _sequenceHelper.MoveAsync(H_Z.MotorNo, LOAD_POSITION, ct);

                await Task.Run(async () => 
                {
                    await _sequenceHelper.MoveAsync(H_X.MotorNo, LOAD_POSITION, ct);
                    await _sequenceHelper.MoveAsync(d_y.MotorNo, LOAD_POSITION, ct);
                });


                await Task.Delay(3000, ct);
                this._sequenceServiceVM.StatusMessage = "DTable Loading Completed";
            }
            catch (OperationCanceledException)
            {
                _logger.Information("DTable Loading Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }

        
    }
}
