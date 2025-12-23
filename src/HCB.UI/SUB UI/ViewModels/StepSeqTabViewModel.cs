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
        private async Task StepStart()
        {
            cts = new CancellationTokenSource();
            await SequenceService.StepMoveWaferCenter(cts.Token);
        }
    }
}
