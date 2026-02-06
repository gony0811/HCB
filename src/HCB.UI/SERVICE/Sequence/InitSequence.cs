using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telerik.Windows.Controls;

namespace HCB.UI
{

    /// <summary>
    /// Initialize Sequence Service
    /// </summary>
    public partial class SequenceService : BackgroundService
    {
        public const string READY_POSITION = "READY";


        
        public bool Init_PreCheck(CancellationToken ct)
        {
            bool dieVacResult = false;
            bool waferVacResult = false;

            string[] dieVac =
            {
                IoExtensions.DO_DTABLE_VAC_1_ON, IoExtensions.DO_DTABLE_VAC_2_ON, IoExtensions.DO_DTABLE_VAC_3_ON, IoExtensions.DO_DTABLE_VAC_4_ON, IoExtensions.DO_DTABLE_VAC_5_ON,
                IoExtensions.DO_DTABLE_VAC_6_ON, IoExtensions.DO_DTABLE_VAC_7_ON, IoExtensions.DO_DTABLE_VAC_8_ON, IoExtensions.DO_DTABLE_VAC_9_ON,
            };
            string[] waferVac =
            {
                IoExtensions.DO_WTABLE_VAC_1_ON, IoExtensions.DO_WTABLE_VAC_2_ON, IoExtensions.DO_WTABLE_VAC_3_ON, IoExtensions.DO_WTABLE_VAC_4_ON, IoExtensions.DO_WTABLE_VAC_5_ON
            };

            try
            {
                this._logger.Debug("초기화 사전 점검 시작");
                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

                // ----------------- Die 확인 -------------------------
                for(int i=0; i < dieVac.Length; i++)
                {
                    // Die에 잔류 자재 유무 확인
                    var result = ioDevice.GetDigital(dieVac[i]);
                    if (result) _logger.Information($"[Pre-Check] #{i} Die에 잔류한 자재가 있습니다");
                    dieVacResult = dieVacResult || result;
                }

                // Die 잔류 자재 유무 결과 확인
                if (dieVacResult) throw new Exception("Die에 잔류한 자재가 있습니다");

                // ----------------- Wafer 확인 -------------------------
                for (int i = 0; i < waferVac.Length; i++)
                {
                    // Die에 잔류 자재 유무 확인
                    var result = ioDevice.GetDigital(waferVac[i]);
                    if (result) _logger.Information($"[Pre-Check] #{i} Wafer에 잔류한 자재가 있습니다");
                    waferVacResult = waferVacResult || result;
                }

                // Die 잔류 자재 유무 결과 확인
                if (waferVacResult) throw new Exception("Die에 잔류한 자재가 있습니다");

                // ------------------ Picker -----------------------------------
                if (ioDevice.GetDigital(IoExtensions.DI_HEADER_VAC_EJECTOR))
                {
                    _logger.Information("[Pre-Check] Head가 Die를 Pickup하고 있습니다");
                    throw new Exception("Head가 Die를 Pickup하고 있습니다.");
                }

                // 모두 통과 시 
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Information("[Pre-Check] 초기화 사전 점검이 취소되었습니다.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[Pre-Check] 초기화 사전 점검 중 오류 발생");
                return false;
            }
        }

        // 전체 서보온 
        public async Task<bool> Init_ServoAllOn(CancellationToken ct)
        {
            try
            {
                this._logger.Debug("전체 서보온");
                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var motionList = motionDevice.MotionList;
                var tasks = motionList.Select(item => item.ServoOn());
                var results = await Task.WhenAll(tasks);
                await Task.Delay(1000);
                return results.All(r => r == true);
            }
            catch(Exception e)
            {
                this._logger.Error(e, "전체 서보온 중 오류 발생");
                return false;
            }
        }

        public async Task Init_ServoAllOff(CancellationToken ct)
        {
            try
            {
                this._logger.Debug("전체 서보오프");
                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var motionList = motionDevice.MotionList;
                var tasks = motionList.Select(item => item.ServoOff());
                var results = await Task.WhenAll(tasks);
                // (옵션) 하나라도 실패했는지 확인하려면
                bool isAllSuccess = results.All(r => r == true);
            }
            catch (Exception e)
            {
                this._logger.Error(e, "전체 서보온 중 오류 발생");
            }
        }

        public async Task<bool> SensorOnOff(string sensorName, CancellationToken ct)
        {
            var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

            return await ioDevice.SetDigitalAsync(sensorName, false);
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

                if (H_Z is null || !H_Z.IsEnabled || !H_Z.IsHomeDone) throw new Exception("H_Z축이 준비되지 않았습니다. H_Z축 Servo On, Home 실행여부를 확인하십시요.");
                if (h_z is null || !h_z.IsEnabled || !h_z.IsHomeDone) throw new Exception("h_z축이 준비되지 않았습니다. h_z축 Servo On, Home 실행여부를 확인하십시요.");

                if (H_Z.IsBusy || h_z.IsBusy) throw new Exception("Head 초기화 실패: HEAD 모션이 움직이고 있습니다.");

                // Head Z축 안전 위치로 이동
                Task HZ = _sequenceHelper.MoveAsync(H_Z.MotorNo, MotionExtensions.HEAD_SAFETY, ct);
                Task hz = _sequenceHelper.MoveAsync(h_z.MotorNo, MotionExtensions.HEAD_SAFETY, ct);
                await Task.WhenAll(HZ, hz);
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

        public async Task MotionsMove(string motionName, string positionName, CancellationToken ct)
        {
            var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            var tasks = new List<Task>();

            var motion = motionDevice?.FindMotionByName(motionName);
            if (motion == null)
                throw new KeyNotFoundException($"[Motion Error] '{motionName}' 축을 찾을 수 없습니다.");

            await _sequenceHelper.MoveAsync(motion.MotorNo, positionName, ct);
        }

        public async Task MotionsMove(string[] motions, string positionName, CancellationToken ct)
        {
            var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            var tasks = new List<Task>();

            foreach (var item in motions)
            {
                var motion = motionDevice?.FindMotionByName(item);
                if (motion == null)
                    throw new KeyNotFoundException($"[Motion Error] '{item}' 축을 찾을 수 없습니다.");

                tasks.Add(_sequenceHelper.MoveAsync(motion.MotorNo, positionName, ct));
            }

            await Task.WhenAll(tasks);
        }

        public async Task MotionsMove(IEnumerable<(string Motion, string Position)> motionPosition, CancellationToken ct)
        {
            var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            var tasks = new List<Task>();

            foreach (var (motionName, positionName) in motionPosition)
            {
                var motion = motionDevice?.FindMotionByName(motionName);
                if (motion == null)
                    throw new KeyNotFoundException($"[Motion Error] '{motionName}' 축을 찾을 수 없습니다.");

                tasks.Add(_sequenceHelper.MoveAsync(motion.MotorNo, positionName, ct));
            }

            await Task.WhenAll(tasks);
        }

        public void EQStatusCheck()
        {
            var status = _operationService.Status;
            if (status.Availability == Availability.Down || status.Run == RunStop.Run || status.Operation == OperationMode.Auto || status.Alarm == AlarmState.HEAVY)
            {
                _logger.Warning("Cannot execute WTableLoading: Sequence Service is not in Manual Standby Status.");
                throw new Exception("Cannot execute WTableLoading");
            }
        }

        public async Task MachineInitAsync(CancellationToken ct)
        {
            try
            {
                _sequenceServiceVM.InitializeProgress = 0;
                var status = _operationService.Status;

                if (status.Availability == Availability.Down || status.Run == RunStop.Run || status.Operation == OperationMode.Manual || status.Alarm == AlarmState.HEAVY)
                {
                    this._logger.Warning("MachineInitAsync를 실행할 수 없습니다: 설비 상태가 다운 상태 입니다. 알람을 조치하고 설비를 리셋하십시요.");
                    return;
                }

                this._logger.Debug("MachineInitAsync 시작");

                var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
                var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

                // 1. PreCheck 
                _sequenceServiceVM.SystemCheck = StepState.InProgress;
                bool preCheck = Init_PreCheck(ct);
                if (preCheck == false)
                {
                    _sequenceServiceVM.SystemCheck = StepState.Failed;
                    throw new Exception("[Initialize] PRE-CHECK 실패");
                }
                _sequenceServiceVM.SystemCheck = StepState.Completed;
                _sequenceServiceVM.InitializeProgress = 15;

                // 2. Wafer pin down
                await _sequenceHelper.WTableLiftPin(eUpDown.Down, ct);
                _sequenceServiceVM.InitializeProgress = 30;
                // 3. 전체 Servo On 
                _sequenceServiceVM.ServoOn= StepState.InProgress;
                bool servoResult = await Init_ServoAllOn(ct);
                if (!servoResult)
                {
                    _sequenceServiceVM.ServoOn = StepState.Failed;
                    throw new Exception("[Initialize] 모든 축이 SERVO ON 되지 않았습니다");
                }
                _sequenceServiceVM.ServoOn = StepState.Completed;
                _sequenceServiceVM.InitializeProgress = 45;
                // 4. H-Z BREAK OFF
                _sequenceServiceVM.HZBreakOff = StepState.InProgress;
                bool breakResult = await ioDevice.SetDigitalAsync(IoExtensions.DO_ZIMM_SOL_ON, false);
                if (!breakResult)
                {
                    _sequenceServiceVM.HZBreakOff = StepState.Failed;
                    throw new Exception("[Initialize] H-Z축의 브레이크가 OFF 되지 않았습니다");
                }
                _sequenceServiceVM.HZBreakOff = StepState.Completed;
                _sequenceServiceVM.InitializeProgress = 60;
                // 5. Header Home Z축
                var HZ = motionDevice.FindMotionByName(MotionExtensions.H_Z);
                var hz = motionDevice.FindMotionByName(MotionExtensions.h_z);
                var head = new List<IAxis> { HZ, hz };

                _sequenceServiceVM.HZHome = StepState.InProgress;
                _sequenceServiceVM.HzHome = StepState.InProgress;
                var headHomeResult = await MotionExtensions.HomeAsync(_sequenceHelper, head, ct);
                if (!headHomeResult)
                {
                    _sequenceServiceVM.HZHome = StepState.Failed;
                    _sequenceServiceVM.HzHome = StepState.Failed;
                    throw new Exception("[Initialize] Header가 홈에 도착하지 않았습니다");
                }
                _sequenceServiceVM.HZHome = StepState.Completed;
                _sequenceServiceVM.HzHome = StepState.Completed;

                _sequenceServiceVM.InitializeProgress = 75;

                // 6. Header Home X, T 축
                var hx = motionDevice.FindMotionByName(MotionExtensions.H_X);
                var ht= motionDevice.FindMotionByName(MotionExtensions.H_T);
                var xt = new List<IAxis> { hx, ht };

                _sequenceServiceVM.HXHome = StepState.InProgress;
                _sequenceServiceVM.HTHome = StepState.InProgress;
                var hxtResult = await MotionExtensions.HomeAsync(_sequenceHelper, xt, ct);
                if (!hxtResult)
                {
                    _sequenceServiceVM.HXHome = StepState.Failed;
                    _sequenceServiceVM.HTHome = StepState.Failed;
                    throw new Exception("[Initialize] Header X,T가 홈에 도착하지 않았습니다");
                }
                _sequenceServiceVM.HXHome = StepState.Completed;
                _sequenceServiceVM.HTHome = StepState.Completed;

                _sequenceServiceVM.InitializeProgress = 85;

                // 7.  Y Axis Home 
                var dy = motionDevice.FindMotionByName(MotionExtensions.D_Y);
                var py = motionDevice.FindMotionByName(MotionExtensions.P_Y);
                var wy = motionDevice.FindMotionByName(MotionExtensions.W_Y);
                var wt = motionDevice.FindMotionByName(MotionExtensions.W_T);
                var yAxis = new List<IAxis> { dy, py, wy, wt };

                _sequenceServiceVM.DYHome = StepState.InProgress;
                _sequenceServiceVM.PYHome = StepState.InProgress;
                _sequenceServiceVM.WYHome = StepState.InProgress;
                _sequenceServiceVM.WTHome = StepState.InProgress;
                var yResult = await MotionExtensions.HomeAsync(_sequenceHelper, yAxis, ct);
                if (!yResult)
                {
                    _sequenceServiceVM.DYHome = StepState.Failed;
                    _sequenceServiceVM.PYHome = StepState.Failed;
                    _sequenceServiceVM.WYHome = StepState.Failed;
                    _sequenceServiceVM.WTHome = StepState.Failed;
                    throw new Exception("[Initialize] Header X,T가 홈에 도착하지 않았습니다");
                }
                _sequenceServiceVM.DYHome = StepState.Completed;
                _sequenceServiceVM.PYHome = StepState.Completed;
                _sequenceServiceVM.WYHome = StepState.Completed;
                _sequenceServiceVM.WTHome = StepState.Completed;
                _sequenceServiceVM.InitializeProgress = 100;
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
        }

        //public async Task Init_PTable(CancellationToken ct)
        //{
        //    try
        //    {
        //        this._logger.Debug("P-Table 초기화 시작");

        //        var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
        //        var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
        //        // P Table 초기화 로직 구현
        //        var p_y = motionDevice?.FindMotionByName(MotionExtensions.P_Y); // P Table Y축 (예시)
        //        var H_Z = motionDevice?.FindMotionByName(MotionExtensions.H_Z); // Head Z축 (예시)
        //        var headSafetyHeight = H_Z?.PositionList.FirstOrDefault((m) => m.Name == "READY")?.Position;

        //        if (p_y is null)
        //        {
        //            throw new Exception("P_Y axis not found in motion device.");
        //        }

        //        // 초기화 전에 서보 온, 홈 완료, 정지상태 확인
        //        if (p_y.IsEnabled == false || p_y.IsHomeDone != true || p_y.IsBusy)
        //        {
        //            throw new Exception("P-Table 초기화 실패: 서보가 켜져 있지 않거나, 홈이 완료되지 않았거나, 축이 움직이고 있습니다.");
        //        }
        //        else if (H_Z?.CurrentPosition <= (headSafetyHeight - H_Z?.InpositionRange)) // Head가 안전 높이보다 낮은 경우 (0에 가까우면 높은 위치)
        //        {
        //            throw new Exception("P-Table 초기화 실패: Head Z축이 안전 높이 이상에 있지 않습니다.");
        //        }
        //        else
        //        {
        //            await _sequenceHelper.MoveAsync(p_y.MotorNo, READY_POSITION, ct);
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.Information("P-Table 초기화가 취소되었습니다.");
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(ex, "P-Table 초기화 중 오류 발생");
        //        throw;
        //    }
        //}

        //public async Task Init_WTable(CancellationToken ct)
        //{
        //    try
        //    {
        //        this._logger.Debug("W-Table 초기화 시작");

        //        var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
        //        var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

        //        // Die Table 초기화 로직 구현
        //        var w_y = motionDevice?.FindMotionByName(MotionExtensions.W_Y); // Die Table Y축 (예시)

        //        if (w_y is null)
        //        {
        //            throw new Exception("w_y axis not found in motion device.");
        //        }

        //        // 서보 온
        //        if (!w_y.IsEnabled)
        //            await _sequenceHelper.Servo(w_y.MotorNo, true, ct);

        //        // Wafer Table 홈 위치로 이동 (예시 위치명: "InitPosition")
        //        if (!w_y.IsHomeDone && w_y.InPosition)
        //        {
        //            await _sequenceHelper.HomeAsync(w_y.MotorNo, ct);
        //        }

        //        // 대기 위치로 이동

        //        if (w_y.InPosition)
        //        {
        //            await _sequenceHelper.MoveAsync(w_y.MotorNo, READY_POSITION, ct);
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.Information("W-Table 초기화가 취소되었습니다.");
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(ex, "W-Table 초기화 중 오류 발생");
        //        throw;
        //    }
        //}

        //public async Task Init_DTable(CancellationToken ct)
        //{
        //    try
        //    {
        //        this._logger.Debug("D-Table 초기화 시작");

        //        var motionDevice = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
        //        var ioDevice = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);

        //        // Die Table 초기화 로직 구현
        //        var d_y = motionDevice?.FindMotionByName(MotionExtensions.D_Y); // Die Table Y축 (예시)

        //        if (d_y is null)
        //        {
        //            throw new Exception("d_y axis not found in motion device.");
        //        }

        //        // 서보 온
        //        if (!d_y.IsEnabled)
        //            await _sequenceHelper.Servo(d_y.MotorNo, true, ct);

        //        // Die Table 홈 위치로 이동 (예시 위치명: "InitPosition")
        //        if (!d_y.IsHomeDone && d_y.InPosition)
        //        {
        //            await _sequenceHelper.HomeAsync(d_y.MotorNo, ct);
        //        }

        //        // 대기 위치로 이동

        //        if (d_y.InPosition)
        //        {
        //            await _sequenceHelper.MoveAsync(d_y.MotorNo, READY_POSITION, ct);
        //        }
        //    }
        //    catch (OperationCanceledException)
        //    {
        //        _logger.Information("DTableSequece: InitializeAsync가 취소되었습니다.");
        //        throw;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(ex, "DTableSequece: InitializeAsync 중 오류 발생");
        //        throw;
        //    }
        //}
    }
}
