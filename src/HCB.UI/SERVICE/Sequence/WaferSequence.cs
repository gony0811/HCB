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
        public async Task WTableLoading(CancellationToken ct)
        {
            try
            {
                _logger.Information("Wafer Loading Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                string[] motions = { MotionExtensions.W_Y, MotionExtensions.H_X };
                await MotionsMove(motions, MotionExtensions.WAFER_LOADING, ct);
                await Task.Delay(3000, ct);

                // Vacuum Off
                await _sequenceHelper.WTableVacuumAll(eOnOff.Off, ct);

                // Wafer Pin UP
                await _sequenceHelper.WTableLiftPin(eUpDown.Up, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Wafer Loading Canceled");
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                return;
            }
            finally
            {
                _logger.Information("Wafer Loading End");
            }
        }

        public async Task WTableLoadComplete(CancellationToken ct)
        {
            try
            {
                // 1. Wafer pin down 
                await _sequenceHelper.WTableLiftPin(eUpDown.Up, ct);

                // 2. wafer vacuum on
                await _sequenceHelper.WTableVacuumAll(eOnOff.On, ct);
            }
            catch (Exception e)
            {
                _logger.Error(e, e.Message);
            }

        }


        public async Task WTableAlign(CancellationToken ct)
        {
            try
            {
                _logger.Information("Wafer Align Start");
                EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                string[] xy = { MotionExtensions.W_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z, MotionExtensions.h_z };

                // Wafer Center Align
                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                await MotionsMove(xy, MotionExtensions.WAFER_CENTER_POSITION , ct);
                await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);

                // TODO: 비전 측정

                // Wafer Left Align
                await Init_Head(ct);
                await MotionsMove(xy, MotionExtensions.WAFER_LEFT_POSITION, ct);
                await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);

                // TODO: 비전 측정

                //Wafer Right Align
                await Init_Head(ct);
                await MotionsMove(xy, MotionExtensions.WAFER_RIGHT_POSITION, ct);
                await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);

                // TODO: 비전 측정
                await Init_Head(ct);
                // TODO: 오차 보정

            }
            catch (Exception e)
            {

            }
            finally
            {
                _logger.Information("Wafer Align End");
            }

        }
    }
}
