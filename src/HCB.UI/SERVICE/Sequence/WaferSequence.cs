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
                //EQStatusCheck();    // 장비 상태 체크 => 실패시 error 발생

                var motionDevice = this._deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);

                await Init_Head(ct);        // Head Z 축을 안전한 위치로 이동
                string[] motions = { MotionExtensions.W_Y };
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
                // Vacuum Off
                await _sequenceHelper.WTableVacuumAll(eOnOff.On, ct);

                // Wafer Pin UP
                await _sequenceHelper.WTableLiftPin(eUpDown.Down, ct);
            }
            catch (Exception e)
            {
                _logger.Error(e, e.Message);
            }

        }

        // TODO : 지우기. 
        public async Task WTablePinControll(eUpDown eUpDown, CancellationToken ct)
        {
            try
            {
                await _sequenceHelper.WTableLiftPin(eUpDown, ct);
            }catch(Exception e)
            {
                _logger.Error(e, e.Message);
            }
        }



        public async Task WTableAlign(CancellationToken ct)
        {
            try
            {
                _logger.Information("Wafer Align Start");

                //EQStatusCheck();

                // 추가: Wafer & Die 존재 여부 확인 
                // 1. 있을 경우 정상 진행 
                // 2. 없을 경우 에러 발생

                string[] xy = { MotionExtensions.W_Y, MotionExtensions.H_X };
                string[] z = { MotionExtensions.H_Z};

                // Wafer Align 촬영 전 Z축을 저배율 촬영 가능한 위치까지 옮긴다. 이후 고정된 상태에서 촬영을 실시한다. 
                // 주의: H_Z축 NAME: WAFER_ALIGN_LOW의 위치는 WAFER 척과 부딪히지 않는 선에서 설정하도록 한다.
                await MotionsMove(z, MotionExtensions.WAFER_ALIGN_LOW, ct);
                
                // WAFER ALIGN은 4번의 촬영을 실시한다.
                // 1 2 
                // 3 4
                // 1번축 
                await MotionsMove(xy, MotionExtensions.WAFER_ALIGN_1, ct);
                // TODO: 이미지촬영 1
                
                // 2번축
                await MotionsMove(MotionExtensions.H_X, MotionExtensions.WAFER_ALIGN_2, ct);
                // TODO: 이미지촬영 2

                // 4번축
                await MotionsMove(xy, MotionExtensions.WAFER_ALIGN_2, ct);
                // TODO: 이미지촬영 4

                // 3번축
                await MotionsMove(MotionExtensions.H_X, MotionExtensions.WAFER_ALIGN_1, ct);
                // TODO: 이미지촬영 3


                await ExecuteStepAsync(
                    async () =>
                    {
                        // TODO: 오차 보정 계산 + 이동
                    },
                    s => _sequenceServiceVM.WaferAlign = s,
                    "Wafer Final Align",
                    ct
                );
            }
            catch(Exception e)
            {
                _logger.Warning("Wafer Align 실패");
                _logger.Warning(e.Message);
            }
            finally
            {
                _sequenceServiceVM.IsWaferAlign = true;
                _logger.Information("Wafer Align End");
            }
        }

    }
}
