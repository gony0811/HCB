using HCB.Data.Entity.Type;
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
                if (EQStatus.Availability == Availability.Down || EQStatus.Run == RunStop.Run || EQStatus.Operation == OperationMode.Auto || EQStatus.Alarm == AlarmLevel.HEAVY)
                {
                    _logger.Warning("Cannot execute DTableLoading: Sequence Service is not in Manual Standby Status.");
                    return;
                }

                _logger.Information("Die Loading Start");
               
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
            }
            catch (OperationCanceledException)
            {              
                _logger.Information("Die Loading Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
            finally
            {
                _logger.Information("Die Loading End");
            }
        }


        public async Task WTableLoading(CancellationToken ct)
        {
            try
            {
                if (EQStatus.Availability == Availability.Down || EQStatus.Run == RunStop.Run || EQStatus.Operation == OperationMode.Auto || EQStatus.Alarm == AlarmLevel.HEAVY)
                {
                    _logger.Warning("Cannot execute WTableLoading: Sequence Service is not in Manual Standby Status.");
                    return;
                }

                _logger.Information("Wafer Loading Start");

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                var w_y = motionDevice?.FindMotionByName(MotionExtensions.W_Y); // W Table Y축 (예시)
                var H_X = motionDevice?.FindMotionByName(MotionExtensions.H_X); // H Table X축 (예시)
                var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // H Table Z축 (예시)

                if (w_y == null) throw new Exception("W Table Y axis not found in motion device.");
                if (H_X == null) throw new Exception("H Table X axis not found in motion device.");
                if (H_Z == null) throw new Exception("H Table Z axis not found in motion device.");

                await _sequenceHelper.MoveAsync(H_Z.MotorNo, LOAD_POSITION, ct);

                await Task.Run(async () =>
                {
                    await _sequenceHelper.MoveAsync(H_X.MotorNo, LOAD_POSITION, ct);
                    await _sequenceHelper.MoveAsync(w_y.MotorNo, LOAD_POSITION, ct);
                });


                await Task.Delay(3000, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Wafer Loading Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
            finally
            {
                _logger.Information("Wafer Loading End");
            }
        }
        
    }
}
