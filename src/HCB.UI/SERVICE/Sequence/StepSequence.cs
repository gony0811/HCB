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
        public async Task StepSequenceInitialize(CancellationToken ct)
        {
            try
            {
                _logger.Information("Step Sequence Initialize Start");
                await Task.Run(() =>
                {
                    // 여기에 초기화 로직을 구현하세요.
                    this._sequenceServiceVM.InitializeAllSteps();
                });

            }
            catch (OperationCanceledException)
            {
                _logger.Information("Step Sequence Initialize Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
            finally
            {
                _logger.Information("Step Sequence Initialize End");
            }
        }

        public async Task StepMoveWaferCenter(CancellationToken ct)
        {
            try
            {
                _logger.Information("Step Move Wafer Center Start");
                // 여기에 Wafer Center로 이동하는 로직을 구현하세요.

                this._sequenceServiceVM.StepWaferCenterMoveCompleted = StepState.InProgress;

                var startTime = DateTime.Now;
                var timespan = new TimeSpan(0, 0, 10); // 예시: 10초 후 종료


                // MoveAsync가 완료되면 시간 측정도 멈추기 위해 내부 토큰 생성
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    // 1. 실제 이동 작업 (이동이 완료되면 토큰을 취소시킴)
                    var moveTask = Task.Run(async () =>
                    {
                        try
                        {
                            if (this._simulation)
                            {
                                // 시뮬레이션 모드에서는 실제 이동을 수행하지 않음
                                await Task.Delay(5000, linkedCts.Token); // 시뮬레이션을 위한 대기 시간
                            }
                            else
                            {
                                await this._sequenceHelper.MoveAsync(MotionExtensions.W_T, MotionExtensions.WAFER_CENTER_POSITION, linkedCts.Token);
                            }

                        }
                        finally
                        {
                            linkedCts.Cancel(); // 이동 완료(또는 에러) 시 시간 측정 루프 종료 신호
                        }
                    }, ct);

                    // 2. 시간 측정 작업 (토큰이 취소될 때까지 경과 시간을 UI에 업데이트)
                    var measureTask = this._sequenceServiceVM.MeasureElapsedStepWaferCenterMove(linkedCts.Token);

                    // 두 작업이 끝날 때까지 대기
                    await Task.WhenAll(moveTask, measureTask);
                }

                this._sequenceServiceVM.StepWaferCenterMoveCompleted = StepState.Completed;
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Step Move Wafer Center Canceled");
                this._sequenceServiceVM.StepWaferCenterMoveCompleted = StepState.Aborted;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                this._sequenceServiceVM.StepWaferCenterMoveCompleted = StepState.Failed;
            }
            finally
            {
                _logger.Information("Step Move Wafer Center End");
            }
        }

        public async Task StepWaferAlign(CancellationToken ct)
        {
            try
            {
                _logger.Information("Step Wafer Align Start");

                this._sequenceServiceVM.StepWaferAlignCompleted = StepState.InProgress;

                var startTime = DateTime.Now;
                var timespan = new TimeSpan(0, 0, 10); // 예시: 10초 후 종료


                // MoveAsync가 완료되면 시간 측정도 멈추기 위해 내부 토큰 생성
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    // 1. 실제 이동 작업 (이동이 완료되면 토큰을 취소시킴)
                    var moveTask = Task.Run(async () =>
                    {
                        try
                        {
                            if (this._simulation)
                            {
                                // 시뮬레이션 모드에서는 실제 이동을 수행하지 않음
                                await Task.Delay(5000, linkedCts.Token); // 시뮬레이션을 위한 대기 시간
                            }
                            else
                            {
                                /// 여기에 Wafer Align 이동 로직 구현
                            }

                        }
                        finally
                        {
                            linkedCts.Cancel(); // 이동 완료(또는 에러) 시 시간 측정 루프 종료 신호
                        }
                    }, ct);

                    // 2. 시간 측정 작업 (토큰이 취소될 때까지 경과 시간을 UI에 업데이트)
                    var measureTask = this._sequenceServiceVM.MeasureElapsedStepWaferAlign(linkedCts.Token);

                    // 두 작업이 끝날 때까지 대기
                    await Task.WhenAll(moveTask, measureTask);

                    this._sequenceServiceVM.StepWaferAlignCompleted = StepState.Completed;
                }
            }
            catch (OperationCanceledException)
            {
                this._sequenceServiceVM.StepWaferAlignCompleted = StepState.Aborted;
                _logger.Information("Step Wafer Align Canceled");
            }
            catch (Exception ex)
            {
                this._sequenceServiceVM.StepWaferAlignCompleted = StepState.Failed;
                _logger.Error(ex, ex.Message);
            }
            finally
            {
                _logger.Information("Step Wafer Align End");

            }
        }

        //public async Task StepWaferAlign(CancellationToken ct)
        //{

        //}
    }
}
