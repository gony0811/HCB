using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Spreadsheet.Expressions.Functions;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        public const string LOAD_POSITION = "LOAD";

        public async Task DTableLoadComplete(CancellationToken ct)
        {
            try
            {
                await _sequenceHelper.DTableVacuumAll(eOnOff.On, ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Error(e, e.Message);
            }
        }

        public async Task DTableReady(CancellationToken ct)
        {
            string DtReady = "D_READY";
            try
            {
                var status = _operationService.Status;
                if (status.Availability == Availability.Down || status.Run == RunStop.Run || status.Operation == OperationMode.Auto || status.Alarm == AlarmState.HEAVY)
                {
                    _logger.Warning("Cannot execute DTableLoading: Sequence Service is not in Manual Standby Status.");
                    return;
                }

                _logger.Information("Die Ready Start");

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                var d_y = motionDevice?.FindMotionByName(MotionExtensions.D_Y); // D Table Y축 (예시)
                var H_X = motionDevice?.FindMotionByName(MotionExtensions.H_X); // H Table X축 (예시)
                var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // H Table Z축 (예시)

                if (d_y == null || H_X == null || H_Z == null)
                {
                    string errorMsg = "";
                    if (d_y == null) errorMsg += "[D_Y] ";
                    if (H_X == null) errorMsg += "[H_X] ";
                    if (H_Z == null) errorMsg += "[H_Z] ";
                    throw new Exception(errorMsg + "축을 찾을 수 없습니다");
                }

                await _sequenceHelper.MoveAsync(d_y.MotorNo, DtReady, ct);
                await _sequenceHelper.MoveAsync(H_X.MotorNo, DtReady, ct);
                await _sequenceHelper.MoveAsync(H_Z.MotorNo, DtReady, ct);

                await Task.Delay(3000, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Die Ready Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                return;
            }
            finally
            {
                _logger.Information("Die Ready End");
            }
        }

        

        

        private bool CheckDiePresentOnDTable()
        {
            // Implement the logic to check if the die is present on the D-Table.
            // This could involve reading a sensor value or a status flag from the hardware.
            // Throw an exception or return a boolean value based on the check.

            var ioDevice = this._deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            return this._sequenceHelper.GetDigital(IoExtensions.DI_DTABLE_VAC_PRESSURE_SWITCH);
        }

        private bool CheckHeadPickerVacuum()
        {
            var ioDevice = this._deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            return this._sequenceHelper.GetDigital(IoExtensions.DI_HEADER_VAC_EJECTOR);
        }


        /// <summary>
        /// Die 픽업 시퀀스
        /// </summary>
        /// <param name="index">Die가 위치한 위치 인덱스 (1 ~ 9)</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task DiePickup(int index, CancellationToken ct)
        {
            try
            {
                var status = _operationService.Status;
                if (status.Availability == Availability.Down || status.Run == RunStop.Run || status.Operation == OperationMode.Auto || status.Alarm == AlarmState.HEAVY)
                {
                    _logger.Warning("Cannot execute DiePickup: Sequence Service can not execute die pickup sequence");
                    // Alarm 정의 필요
                    throw new Exception("ALARM");
                }

                // 현재 D-Table에 die가 하나라도 존재하는지 확인
                // 진공 압력 On 여부 확인
                if (!CheckDiePresentOnDTable())
                {
                    _logger.Error("D-Table에 DIE가 하나이상 존재하지 않습니다.");
                    // Alarm 정의 필요
                    throw new Exception("ALARM");
                }

                // 현재 Head의 picker에 die가 존재한다면 알람 발생
                if (CheckHeadPickerVacuum())
                {
                    _logger.Error("Head가 이미 Picking 중입니다.");
                    // Alarm 정의 필요
                    throw new Exception("ALARM");
                }

                _logger.Information("Die Pickup Start");

                // Picking 대상 Die가 위치한 Y 축 이동
                var diePositionName = $"DIE_{index}_PICK";

                if (_sequenceHelper.GetDigital(IoExtensions.DO_DTABLE_VAC_1_ON) == false)
                {
                    _logger.Error("D-Table {0}번에 DIE가 존재하지 않습니다.");
                    // Alarm 정의 필요
                    throw new Exception("ALARM");
                }

                var device = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                await _sequenceHelper.MoveAsync(MotionExtensions.D_Y, diePositionName, ct);

                await Task.WhenAll(
                    _sequenceHelper.MoveAsync(MotionExtensions.H_Z, MotionExtensions.READY_POSITION, ct),
                    _sequenceHelper.MoveAsync(MotionExtensions.h_z, MotionExtensions.READY_POSITION, ct)
                );

                await Task.WhenAll(
                    _sequenceHelper.MoveAsync(MotionExtensions.H_X, diePositionName, ct),
                    _sequenceHelper.MoveAsync(MotionExtensions.D_Y, diePositionName, ct)
                );   
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Die Pickup Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
            finally
            {
                _logger.Information("Die Pickup End");
            }
        }
    }
}
