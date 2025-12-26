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
        private async Task ExecuteStepAsync(string stepName, Action<StepState> setStepState, Func<CancellationToken, Task> action, Func<CancellationToken, Task> measureAction, CancellationToken ct)
        {
            try
            {
                _logger.Information($"Step {stepName} Start");
                setStepState(StepState.InProgress);

                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    // 1. 실제 작업 (완료되면 토큰을 취소시킴)
                    var actionTask = Task.Run(async () =>
                    {
                        try
                        {
                            await action(linkedCts.Token);
                        }
                        finally
                        {
                            if (!linkedCts.IsCancellationRequested)
                            {
                                linkedCts.Cancel(); // 작업 완료(또는 에러) 시 시간 측정 루프 종료 신호
                            }
                        }
                    }, ct);

                    // 2. 시간 측정 작업 (토큰이 취소될 때까지 경과 시간을 UI에 업데이트)
                    var measureTask = measureAction(linkedCts.Token);

                    // 두 작업이 끝날 때까지 대기
                    await Task.WhenAll(actionTask, measureTask);
                }

                ct.ThrowIfCancellationRequested(); // 외부에서 취소 요청이 있었는지 확인
                setStepState(StepState.Completed);
            }
            catch (OperationCanceledException)
            {
                setStepState(StepState.Aborted);
                _logger.Information($"Step {stepName} Canceled");
            }
            catch (Exception ex)
            {
                setStepState(StepState.Failed);
                _logger.Error(ex, ex.Message);
            }
            finally
            {
                _logger.Information($"Step {stepName} End");
            }
        }

        public async Task StepSequenceInitialize(CancellationToken ct)
        {
            // 이 메서드는 다른 스텝들과 달리 상태 추적이나 시간 측정이 없지만,
            // 로깅 및 예외 처리의 일관성을 위해 ExecuteStepAsync를 사용합니다.
            await ExecuteStepAsync(
                "Sequence Initialize",
                _ => { }, // 상태 업데이트 없음
                async token =>
                {
                    // 여기에 초기화 로직을 구현하세요.
                    await Task.Run(() => this._sequenceServiceVM.InitializeAllSteps(), token);
                },
                _ => Task.CompletedTask, // 시간 측정 없음
                ct
            );
        }

        public async Task StepMoveWaferCenter(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Move Wafer Center",
                state => this._sequenceServiceVM.StepWaferCenterMoveCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        // 여기에 Wafer Center로 이동하는 로직을 구현합니다.
                        await this._sequenceHelper.MoveAsync(MotionExtensions.W_T, MotionExtensions.WAFER_CENTER_POSITION, token);
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepWaferCenterMove,
                ct
            );
        }

        public async Task StepWaferAlign(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Wafer Align",
                state => this._sequenceServiceVM.StepWaferAlignCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Wafer Align 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepWaferAlign,
                ct
            );
        }

        public async Task StepMoveDTableCenter(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Move D-Table Center",
                state => this._sequenceServiceVM.StepDTableCenterPositionMoveCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {

                        // 여기에 실제 DTable Center로 이동하는 로직을 구현합니다.
                        await this._sequenceHelper.MoveAsync(MotionExtensions.H_Z, MotionExtensions.DTABLE_CENTER_POSITION, token);
                        await this._sequenceHelper.MoveAsync(MotionExtensions.H_X, MotionExtensions.DTABLE_CENTER_POSITION, token);
                        await this._sequenceHelper.MoveAsync(MotionExtensions.D_Y, MotionExtensions.DTABLE_CENTER_POSITION, token);
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepDTableCenterPositionMove,
                ct
            );
        }

        public async Task StepDieCarrierAlign(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Die Carrier Align",
                state => this._sequenceServiceVM.StepDieCarrierAlignCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Die Carrier Align 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepDieCarrierAlign,
                ct
            );
        }

        public async Task StepDiePickUp(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Die PickUp",
                state => this._sequenceServiceVM.StepDiePickUpCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Die Pick 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepDiePickUp,
                ct
            );
        }

        public async Task StepMovePTableCenter(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Move P-Table Center",
                state => this._sequenceServiceVM.StepMovePTableCenterCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        // 여기에 실제 PTable Center로 이동하는 로직을 구현합니다.
                        await this._sequenceHelper.MoveAsync(MotionExtensions.H_Z, MotionExtensions.DTABLE_CENTER_POSITION, token);
                        await this._sequenceHelper.MoveAsync(MotionExtensions.H_X, MotionExtensions.DTABLE_CENTER_POSITION, token);
                        await this._sequenceHelper.MoveAsync(MotionExtensions.D_Y, MotionExtensions.DTABLE_CENTER_POSITION, token);
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepMovePTableCenter,
                ct
            );
        }

        public async Task StepLeftFiducialMarkAlign(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Left Fiducial Mark Align",
                state => this._sequenceServiceVM.StepLeftFiducialMarkAlignCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Fiducial Mark Align 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepLeftFiducialMarkAlign,
                ct
            );
        }

        public async Task StepRightFiducialMarkAlign(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Right Fiducial Mark Align",
                state => this._sequenceServiceVM.StepRightFiducialMarkAlignCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Fiducial Mark Align 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepRightFiducialMarkAlign,
                ct
            );
        }

        public async Task StepCalculateFiducialMarkPosition(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Calculate Fiducial Mark Position",
                state => this._sequenceServiceVM.StepCalculateFiducialMarkPositionCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Calculate Fiducia Mark Position 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepCalculateFiducialMarkPosition,
                ct
            );
        }

        public async Task StepLeftDieMarkDetect(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Left Die Mark Detect",
                state => this._sequenceServiceVM.StepLeftDieMarkDetectCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Left Die Mark Align 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepLeftDieMarkDetect,
                ct
            );
        }

        public async Task StepRightDieMarkDetect(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Right Die Mark Detect",
                state => this._sequenceServiceVM.StepRightDieMarkDetectCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Right Die Mark Align 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepRightDieMarkDetect,
                ct
            );
        }

        public async Task StepDieAlignment(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Die Alignment",
                state => this._sequenceServiceVM.StepDieAlignmentCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Die Alignment 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepDieAlignment,
                ct
            );
        }

        public async Task StepMoveBondingPosition(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Move Bonding Position",
                state => this._sequenceServiceVM.StepMoveBondingPositionCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Move Bonding Position 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepMoveBondingPosition,
                ct
            );
        }

        public async Task StepWaferLogicMarkDetecting(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Wafer Logic Mark Detecting",
                state => this._sequenceServiceVM.StepWaferLogicMarkDetectingCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Wafer Logic Mark Detect 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepWaferLogicMarkDetecting,
                ct
            );
        }

        public async Task StepDieFinalAlign(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Die Final Align",
                state => this._sequenceServiceVM.StepDieFinalAlignCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Die Final Align 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepDieFinalAlign,
                ct
            );
        }

        public async Task StepBondingProcess(CancellationToken ct)
        {
            await ExecuteStepAsync(
                "Bonding Process",
                state => this._sequenceServiceVM.StepBodingProcessCompleted = state,
                async token =>
                {
                    if (this._simulation)
                    {
                        // 시뮬레이션 모드에서는 5초간 대기합니다.
                        await Task.Delay(5000, token);
                    }
                    else
                    {
                        /// 여기에 Bonding Process 이동 로직 구현
                        await Task.CompletedTask; // 실제 로직 구현 전까지 임시로 추가
                    }
                },
                this._sequenceServiceVM.MeasureElapsedStepBodingProcess,
                ct
            );
        }


    }

}
