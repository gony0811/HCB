using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.IoC;

namespace HCB.UI
{
    public enum StepState
    {
        Idle,
        InProgress,
        Completed,
        Aborted,
        Failed
    }

    [ViewModel(Lifetime.Singleton)]
    public partial class SequenceServiceVM : ObservableObject
    {
        /// <summary>
        /// 설비 초기화 여부 확인
        /// 설비를 초기화 해야 Auto 모드로 진입 가능
        /// </summary>
        [ObservableProperty]
        private bool isMachineInitialized;

        /// <summary>
        /// 설비 상태 - Run/Stop
        /// </summary>
        [ObservableProperty]
        private RunStop runStop;

        /// <summary>
        /// 설비 상태 - Alarm
        /// </summary>
        [ObservableProperty]
        private AlarmState alarm;


        /// <summary>
        /// 설비 상태 - Availability
        /// </summary>
        [ObservableProperty]
        private Availability availability;


        /// <summary>
        /// 설비 상태 - Operation Mode
        /// </summary>
        [ObservableProperty]
        private OperationMode operationMode;


        /// <summary>
        /// SEMI-AUTO SEQUENCE : WAFER ALIGN
        /// STEP 1 : Wafer Center Move
        /// Wafer Center Move 완료 여부
        /// </summary>
        [ObservableProperty]
        private StepState stepWaferCenterMoveCompleted;
        [ObservableProperty]
        private string stepWaferCenterMoveElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : WAFER ALIGN 
        /// STEP 2: Wafer Align
        /// Wafer Macro Align 완료 여부
        /// </summary>
        [ObservableProperty]
        private StepState stepWaferAlignCompleted;
        [ObservableProperty]
        private string stepWaferAlignElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : DIE ALIGN & PICUP
        /// STEP 1 : Die Center Position Move
        /// </summary>
        [ObservableProperty]
        private StepState stepDTableCenterPositionMoveCompleted;
        [ObservableProperty]
        private string stepDTableCenterPositionMoveElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : DIE ALIGN & PICUP
        /// STEP 2 : Die Carrier Align
        /// </summary>
        [ObservableProperty]
        private StepState stepDieCarrierAlignCompleted;
        [ObservableProperty]
        private string stepDieCarrierAlignElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : DIE ALIGN & PICUP
        /// STEP 3 : Die pickup
        /// </summary>
        [ObservableProperty]
        private StepState stepDiePickUpCompleted;
        [ObservableProperty]
        private string stepDiePickUpElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : FIDUCIAL MARK ALIGN
        /// STEP 1 : Move to P-Table Center Position
        /// </summary>
        [ObservableProperty]
        private StepState stepMovePTableCenterCompleted;
        [ObservableProperty]
        private string stepMovePTableCenterElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : FIDUCIAL MARK ALIGN
        /// STEP 2 : Left Fiducial Mark Align
        /// </summary>
        [ObservableProperty]
        private StepState stepLeftFiducialMarkAlignCompleted;
        [ObservableProperty]
        private string stepLeftFiducialMarkAlignElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : FIDUCIAL MARK ALIGN
        /// STEP 3 : Right Fiducial Mark Align
        /// </summary>
        [ObservableProperty]
        private StepState stepRightFiducialMarkAlignCompleted;
        [ObservableProperty]
        private string stepRightFiducialMarkAlignElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : FIDUCIAL MARK ALIGN
        /// STEP 4 : Calculate Fiducial Mark Position
        /// </summary>
        [ObservableProperty]
        private StepState stepCalculateFiducialMarkPositionCompleted;
        [ObservableProperty]
        private string stepCalculateFiducialMarkPositionElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : DIE MARK ALIGN
        /// STEP 5 : Left Die Mark Detect
        /// </summary>
        [ObservableProperty]
        private StepState stepLeftDieMarkDetectCompleted;
        [ObservableProperty]
        private string stepLeftDieMarkDetectElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : DIE MARK ALIGN
        /// STEP 6 : Right Die Mark Detect
        /// </summary>
        [ObservableProperty]
        private StepState stepRightDieMarkDetectCompleted;
        [ObservableProperty]
        private string stepRightDieMarkDetectElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : DIE MARK ALIGN
        /// STEP 7 : Die Alignment Complete
        /// </summary>
        [ObservableProperty]
        private StepState stepDieAlignmentCompleted;
        [ObservableProperty]
        private string stepDieAlignmentElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : W-TABLE BONDING
        /// STEP 1 : Move Bonding Position
        /// </summary>
        [ObservableProperty]
        private StepState stepMoveBondingPositionCompleted;
        [ObservableProperty]
        private string stepMoveBondingPositionElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : W-TABLE BONDING
        /// STEP 1 : Move Bonding Position
        /// </summary>
        [ObservableProperty]
        private StepState stepWaferLogicMarkDetectingCompleted;
        [ObservableProperty]
        private string stepWaferLogicMarkDetectingElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : W-TABLE BONDING
        /// STEP 2 : Die Final Align
        /// </summary>
        [ObservableProperty]
        private StepState stepDieFinalAlignCompleted;
        [ObservableProperty]
        private string stepDieFinalAlignElapsedTime;

        /// <summary>
        /// SEMI-AUTO SEQUENCE : W-TABLE BONDING
        /// STEP 3 : Move Bonding Position
        /// </summary>
        [ObservableProperty]
        private StepState stepBodingProcessCompleted;
        [ObservableProperty]
        private string stepBodingProcessElapsedTime;

        public SequenceServiceVM()
        {
            IsMachineInitialized = false;
            RunStop = RunStop.Stop;
            Alarm = AlarmState.NO_ALARM;
            Availability = Availability.Up;
            OperationMode = OperationMode.Manual;

        }

        public void InitializeAllSteps()
        {
            /// SEMI-AUTO SEQUENCE : WAFER ALIGN
            StepWaferCenterMoveCompleted = StepState.Idle;
            StepWaferCenterMoveElapsedTime = "00:00:00";
            StepWaferAlignCompleted = StepState.Idle;
            StepWaferAlignElapsedTime = "00:00:00";


            /// SEMI-AUTO SEQUENCE : DIE ALIGN & PICUP
            StepDTableCenterPositionMoveCompleted = StepState.Idle;
            StepDTableCenterPositionMoveElapsedTime = "00:00:00";
            StepDieCarrierAlignCompleted = StepState.Idle;
            StepDieCarrierAlignElapsedTime = "00:00:00";
            StepDiePickUpCompleted = StepState.Idle;
            StepDiePickUpElapsedTime = "00:00:00";

            /// SEMI-AUTO SEQUENCE : FIDUCIAL MARK ALIGN
            StepMovePTableCenterCompleted = StepState.Idle;
            StepMovePTableCenterElapsedTime = "00:00:00";
            StepLeftFiducialMarkAlignCompleted = StepState.Idle;
            StepLeftFiducialMarkAlignElapsedTime = "00:00:00";
            StepRightFiducialMarkAlignCompleted = StepState.Idle;
            StepRightFiducialMarkAlignElapsedTime = "00:00:00";
            StepCalculateFiducialMarkPositionCompleted = StepState.Idle;
            StepCalculateFiducialMarkPositionElapsedTime = "00:00:00";
            StepLeftDieMarkDetectCompleted = StepState.Idle;
            StepLeftDieMarkDetectElapsedTime = "00:00:00";
            StepRightDieMarkDetectCompleted = StepState.Idle;
            StepRightDieMarkDetectElapsedTime = "00:00:00";
            StepDieAlignmentCompleted = StepState.Idle;
            StepDieAlignmentElapsedTime = "00:00:00";

            /// SEMI-AUTO SEQUENCE : W-TABLE BONDING
            StepMoveBondingPositionCompleted = StepState.Idle;
            StepMoveBondingPositionElapsedTime = "00:00:00";
            StepWaferLogicMarkDetectingCompleted = StepState.Idle;
            StepWaferLogicMarkDetectingElapsedTime = "00:00:00";
            StepDieFinalAlignCompleted = StepState.Idle;
            StepDieFinalAlignElapsedTime = "00:00:00";
            StepBodingProcessCompleted = StepState.Idle;
            StepBodingProcessElapsedTime = "00:00:00";
        }

        public async Task MeasureElapsedTime(Action<string> setElapsedTimeAction, CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    setElapsedTimeAction(stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
                    await Task.Delay(10, ct);
                }
            }
            catch (TaskCanceledException)
            {
                // 작업 취소는 정상적인 동작입니다.
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public Task MeasureElapsedStepWaferCenterMove(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepWaferCenterMoveElapsedTime = elapsed, ct);
        }

        public Task MeasureElapsedStepWaferAlign(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepWaferAlignElapsedTime = elapsed, ct);
        }

        public Task MeasureElapsedStepDTableCenterPositionMove(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepDTableCenterPositionMoveElapsedTime = elapsed, ct);
        }

        public Task MeasureElapsedStepDieCarrierAlign(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepDieCarrierAlignElapsedTime = elapsed, ct);
        }


        public Task MeasureElapsedStepDiePickUp(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepDiePickUpElapsedTime = elapsed, ct);
        }

        public Task MeasureElapsedStepMovePTableCenter(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepMovePTableCenterElapsedTime = elapsed, ct);
        }

        public Task MeasureElapsedStepLeftFiducialMarkAlign(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepLeftFiducialMarkAlignElapsedTime = elapsed, ct);
        }


        public Task MeasureElapsedStepRightFiducialMarkAlign(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepRightFiducialMarkAlignElapsedTime = elapsed, ct);
        }


        public Task MeasureElapsedStepCalculateFiducialMarkPosition(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepCalculateFiducialMarkPositionElapsedTime = elapsed, ct);
        }

        public Task MeasureElapsedStepLeftDieMarkDetect(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepLeftDieMarkDetectElapsedTime = elapsed, ct);
        }


        public Task MeasureElapsedStepRightDieMarkDetect(CancellationToken ct)
        {
            return MeasureElapsedTime(elapsed => StepRightDieMarkDetectElapsedTime = elapsed, ct);
        }

    }
}