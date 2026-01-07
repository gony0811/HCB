using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class StepSeqTabViewModel : ObservableObject
    {
        private CancellationTokenSource cts;
        private readonly SequenceService SequenceService;
        public SequenceServiceVM SequenceServiceVM { get; }
        public StepSeqTabViewModel(SequenceServiceVM sequenceServiceVM, SequenceService sequenceService)
        {
            this.SequenceServiceVM = sequenceServiceVM;
            this.SequenceService = sequenceService;

        }

        [RelayCommand]
        private async Task Step1Start()
        {
            cts = new CancellationTokenSource();
            await SequenceService.StepMoveWaferCenter(cts.Token);
            await SequenceService.StepWaferAlign(cts.Token);
        }

        [RelayCommand]
        private void Step1Stop()
        {
            cts?.Cancel();
        }

        [RelayCommand]
        private async Task Step2Start()
        {
            cts = new CancellationTokenSource();
            await SequenceService.StepMoveDTableCenter(cts.Token);
            await SequenceService.StepDieCarrierAlign(cts.Token);
            await SequenceService.StepDiePickUp(cts.Token);
        }

        [RelayCommand]
        private void Step2Stop()
        {
            cts?.Cancel();
        }

        [RelayCommand]
        private async Task Step3Start()
        {
            cts = new CancellationTokenSource();
            await SequenceService.StepMovePTableCenter(cts.Token);
            await SequenceService.StepLeftFiducialMarkAlign(cts.Token);
            await SequenceService.StepRightFiducialMarkAlign(cts.Token);
            await SequenceService.StepCalculateFiducialMarkPosition(cts.Token);
            await SequenceService.StepLeftDieMarkDetect(cts.Token);
            await SequenceService.StepRightDieMarkDetect(cts.Token);
            await SequenceService.StepDieAlignment(cts.Token);
        }

        [RelayCommand]
        private void Step3Stop()
        {
            cts?.Cancel();
        }

        [RelayCommand]
        private async Task Step4Start()
        {
            cts = new CancellationTokenSource();
            await SequenceService.StepMoveBondingPosition(cts.Token);
            await SequenceService.StepWaferLogicMarkDetecting(cts.Token);
            await SequenceService.StepDieFinalAlign(cts.Token);
            await SequenceService.StepBondingProcess(cts.Token);
        }

        [RelayCommand]
        private void Step4Stop()
        {
            cts?.Cancel();
        }

    }
}
