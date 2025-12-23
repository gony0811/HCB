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
        /// STEP 1 : Wafer Align
        /// Wafer Center Move 완료 여부
        /// </summary>
        [ObservableProperty]
        private StepState stepWaferCenterMoveCompleted;
        [ObservableProperty]
        private string stepWaferCenterMoveElapsedTime;

        /// <summary>
        /// STEP 1: Wafer Align
        /// Wafer Macro Align 완료 여부
        /// </summary>
        [ObservableProperty]
        private StepState stepWaferMacroAlignCompleted;
        [ObservableProperty]
        private string stepWaferMacroAlignElapsedTime;

        /// <summary>
        /// STEP 1 : Wafer Align
        /// Wafer Micro Align 완료 여부
        /// </summary>
        [ObservableProperty]
        private StepState stepWaferMicroAlignCompleted;

        [ObservableProperty]
        private string stepWaferMicroAlignElapsedTime;


        public SequenceServiceVM()
        {
            IsMachineInitialized = false;
            RunStop = RunStop.Stop;
            Alarm = AlarmState.NO_ALARM;
            Availability = Availability.Up;
            OperationMode = OperationMode.Manual;

            StepWaferCenterMoveCompleted = StepState.Idle;
            StepWaferCenterMoveElapsedTime = "00:00:00";
            StepWaferMacroAlignCompleted = StepState.Idle;
            StepWaferMacroAlignElapsedTime = "00:00:00";
            StepWaferMicroAlignCompleted = StepState.Idle;
            StepWaferMicroAlignElapsedTime = "00:00:00";

        }
        
        public async Task MeasureStepWaferCenterMove(CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {        
                this.StepWaferCenterMoveElapsedTime = string.Format("{0:hh\\:mm\\:ss}", stopwatch.Elapsed);

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                await Task.Delay(10);
            }
        }

        public async Task MeasureStepWaferMacroAlign(CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                this.StepWaferMacroAlignElapsedTime = string.Format("{0:hh\\:mm\\:ss}", stopwatch.Elapsed);

                if (StepWaferMacroAlignCompleted == StepState.Completed)
                {
                    break;
                }

                await Task.Delay(10);
            }
        }

        public async Task MeasureStepWaferMicroAlign(CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                this.StepWaferMicroAlignElapsedTime = string.Format("{0:hh\\:mm\\:ss}", stopwatch.Elapsed);

                if (StepWaferMicroAlignCompleted == StepState.Completed)
                {
                    break;
                }

                await Task.Delay(10);
            }
        }
    }
}
