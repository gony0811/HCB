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

    /// <summary>
    /// Initialize Sequence Service
    /// </summary>
    public partial class SequenceService : BackgroundService
    {
        public const string READY_POSITION = "READY";


        public async Task MachineInitAsync(CancellationToken ct)
        {
            try
            {
                if (EQStatus.Availability == Availability.Down || EQStatus.Run == RunStop.Run || EQStatus.Operation == OperationMode.Manual || EQStatus.Alarm == AlarmState.HEAVY)
                {
                    this._logger.Warning("MachineInitAsync를 실행할 수 없습니다: 시퀀스 서비스가 자동 대기 상태가 아닙니다.");
                    return;
                }
                
                this._logger.Debug("MachineInitAsync 시작");

                await Init_PTable(ct);
                await Init_Head(ct);
                await Init_WTable(ct);
                await Init_DTable(ct);
            }
            catch (OperationCanceledException)
            {

                _logger.Information("MachineInitAsync가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MachineInitAsync 중 오류 발생");
                throw;
            }
            finally
            {
            }
        }

        public async Task Init_PTable(CancellationToken ct)
        {
            try
            {
                this._logger.Debug("P-Table 초기화 시작");

                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
                // P Table 초기화 로직 구현
                var p_y = motionDevice?.FindMotionByName(MotionExtensions.P_Y); // P Table Y축 (예시)
                // 서보 온
                
                if (p_y is null)
                {
                    throw new Exception("P_Y axis not found in motion device.");
                }

                if (!p_y.IsEnabled)
                    await _sequenceHelper.Servo(p_y.MotorNo, true, ct);
                // P Table 홈 위치로 이동 (예시 위치명: "InitPosition")
                if (!p_y.IsHomeDone && p_y.InPosition)
                {
                    await _sequenceHelper.HomeAsync(p_y.MotorNo, ct);
                }
                // 대기 위치로 이동
                if (p_y.InPosition)
                {
                    await _sequenceHelper.MoveAsync(p_y.MotorNo, READY_POSITION, ct);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("P-Table 초기화가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "P-Table 초기화 중 오류 발생");
                throw;
            }
        }

        public async Task Init_Head(CancellationToken ct)
        {
            try
            {
                this._logger.Debug("Head 초기화 시작");
                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
                // Head 초기화 로직 구현
                var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // Head Z축 (예시)
                
                if (H_Z is null)
                {
                    throw new Exception("H_Z axis not found in motion device.");
                }

                // 서보 온
                if (!H_Z.IsEnabled)
                    await _sequenceHelper.Servo(H_Z.MotorNo, true, ct);
                // Head 홈 위치로 이동 (예시 위치명: "InitPosition")
                if (!H_Z.IsHomeDone && H_Z.InPosition)
                {
                    await _sequenceHelper.HomeAsync(H_Z.MotorNo, ct);
                }
                // 대기 위치로 이동
                if (H_Z.InPosition)
                {
                    await _sequenceHelper.MoveAsync(H_Z.MotorNo, READY_POSITION, ct);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Head 초기화가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                throw;
            }
        }

        public async Task Init_WTable(CancellationToken ct)
        {
            try
            {
                this._logger.Debug("W-Table 초기화 시작");

                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

                // Die Table 초기화 로직 구현
                var w_y = motionDevice?.FindMotionByName(MotionExtensions.W_Y); // Die Table Y축 (예시)

                if (w_y is null)
                {
                    throw new Exception("w_y axis not found in motion device.");
                }

                // 서보 온
                if (!w_y.IsEnabled)
                    await _sequenceHelper.Servo(w_y.MotorNo, true, ct);

                // Wafer Table 홈 위치로 이동 (예시 위치명: "InitPosition")
                if (!w_y.IsHomeDone && w_y.InPosition)
                {
                    await _sequenceHelper.HomeAsync(w_y.MotorNo, ct);
                }

                // 대기 위치로 이동

                if (w_y.InPosition)
                {
                    await _sequenceHelper.MoveAsync(w_y.MotorNo, READY_POSITION, ct);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("W-Table 초기화가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "W-Table 초기화 중 오류 발생");
                throw;
            }
        }

        public async Task Init_DTable(CancellationToken ct)
        {
            try
            {
                this._logger.Debug("D-Table 초기화 시작");

                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

                // Die Table 초기화 로직 구현
                var d_y = motionDevice?.FindMotionByName(MotionExtensions.D_Y); // Die Table Y축 (예시)

                if (d_y is null)
                {
                    throw new Exception("d_y axis not found in motion device.");
                }

                // 서보 온
                if (!d_y.IsEnabled)
                    await _sequenceHelper.Servo(d_y.MotorNo, true, ct);

                // Die Table 홈 위치로 이동 (예시 위치명: "InitPosition")
                if (!d_y.IsHomeDone && d_y.InPosition)
                {
                    await _sequenceHelper.HomeAsync(d_y.MotorNo, ct);
                }

                // 대기 위치로 이동

                if (d_y.InPosition)
                {
                    await _sequenceHelper.MoveAsync(d_y.MotorNo, READY_POSITION, ct);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("DTableSequece: InitializeAsync가 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DTableSequece: InitializeAsync 중 오류 발생");
                throw;
            }
        }
    }
}
