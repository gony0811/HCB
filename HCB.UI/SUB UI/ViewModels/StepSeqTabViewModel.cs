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
        private readonly SequenceHelper _sequenceHelper;
        public SequenceServiceVM SequenceServiceVM { get; }
        public StepSeqTabViewModel(SequenceServiceVM sequenceServiceVM, SequenceService sequenceService, SequenceHelper sequenceHelper)
        {
            this.SequenceServiceVM = sequenceServiceVM;
            this.SequenceService = sequenceService;
            _sequenceHelper = sequenceHelper;
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

        [RelayCommand]
        public async Task ServoAllOn(CancellationToken ct)
        {
            await SequenceService.Init_ServoAllOn(ct);
        }

        [RelayCommand]
        public async Task ServoAllOff(CancellationToken ct)
        {
            await SequenceService.Init_ServoAllOff(ct);
        }

        [RelayCommand]
        public async Task WaferPinUp(CancellationToken ct)
        {
            await _sequenceHelper.WTableLiftPin(eUpDown.Up, ct); // W-Table 리프트 핀 다운
        }
        [RelayCommand]
        public async Task WaferPinDown(CancellationToken ct)
        {
            await _sequenceHelper.WTableLiftPin(eUpDown.Down, ct); // W-Table 리프트 핀 다운
        }
    }
}
