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
                var status = _operationService.Status;

                if (status.Availability == Availability.Down || status.Run == RunStop.Run || status.Operation == OperationMode.Manual || status.Alarm == AlarmState.HEAVY)
                {
                    this._logger.Warning("MachineInitAsync를 실행할 수 없습니다: 설비 상태가 다운 상태 입니다. 알람을 조치하고 설비를 리셋하십시요.");
                    return;
                }
                
                this._logger.Debug("MachineInitAsync 시작");

                await Init_PreCheck(ct);
                await Init_Head(ct);
                await Init_WTable(ct);
                await Init_PTable(ct);         
                await Init_DTable(ct);
            }
            catch (OperationCanceledException)
            {

                _logger.Information("MachineInitAsync가 취소되었습니다.");
                throw;
            }
            catch (ErrorException ex)
            {
                await _alarmService.SetAlarm(ex.ErrorCode);
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

        public async Task Init_PreCheck(CancellationToken ct)
        {
            try
            {
                this._logger.Debug("초기화 사전 점검 시작");
                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

                await _sequenceHelper.WTableLiftPin(eUpDown.Down, ct); // W-Table 리프트 핀 다운
            }
            catch (OperationCanceledException)
            {
                _logger.Information("초기화 사전 점검이 취소되었습니다.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "초기화 사전 점검 중 오류 발생");
                throw;
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
                var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // Head Z축 (예시)
                var headSafetyHeight = H_Z?.PositionList.FirstOrDefault((m) => m.Name == "READY")?.Position;

                if (p_y is null)
                {
                    throw new Exception("P_Y axis not found in motion device.");
                }

                // 초기화 전에 서보 온, 홈 완료, 정지상태 확인
                if (p_y.IsEnabled == false || p_y.IsHomeDone != true || p_y.IsBusy)
                { 
                    throw new Exception("P-Table 초기화 실패: 서보가 켜져 있지 않거나, 홈이 완료되지 않았거나, 축이 움직이고 있습니다.");
                }
                else if (H_Z?.CurrentPosition <= (headSafetyHeight - H_Z?.InpositionRange)) // Head가 안전 높이보다 낮은 경우 (0에 가까우면 높은 위치)
                {
                    throw new Exception("P-Table 초기화 실패: Head Z축이 안전 높이 이상에 있지 않습니다.");
                }
                else
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
                var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // Head Z(L)축 (예시)
                var h_z = motionDevice?.FindMotionByName(MotionExtensions.h_z); // Head Z(S)축 (예시)
                var H_X = motionDevice?.FindMotionByName(MotionExtensions.H_X); // Head X축 (예시)

                if (H_Z is null || !H_Z.IsEnabled || !H_Z.IsHomeDone) throw new Exception("H_Z축이 준비되지 않았습니다. H_Z축 Servo On, Home 실행여부를 확인하십시요.");
                if (h_z is null || !h_z.IsEnabled || !h_z.IsHomeDone) throw new Exception("h_z축이 준비되지 않았습니다. h_z축 Servo On, Home 실행여부를 확인하십시요.");
                if (H_X is null || !H_X.IsEnabled || !H_X.IsHomeDone) throw new Exception("H_X축이 준비되지 않았습니다. H_X축 Servo On, Home 실행여부를 확인하십시요.");              


                if (H_Z.IsBusy || h_z.IsBusy || H_X.IsBusy) throw new Exception("Head 초기화 실패: HEAD 모션이 움직이고 있습니다.");

                // Head Z축 대기 위치로 이동
                await _sequenceHelper.MoveAsync(H_Z.MotorNo, READY_POSITION, ct);
                await _sequenceHelper.MoveAsync(h_z.MotorNo, READY_POSITION, ct);
                await _sequenceHelper.MoveAsync(H_X.MotorNo, READY_POSITION, ct);

            }
            catch (OperationCanceledException)
            {
                _logger.Information("Head 초기화가 취소되었습니다.");
                throw;
            }
            catch (ErrorException ex)
            {
                await _alarmService.SetAlarm(ex.ErrorCode);
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
