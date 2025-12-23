using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public class StepSeqTabViewModel : ObservableObject
    {
        public SequenceServiceVM SequenceServiceVM { get; }
        public StepSeqTabViewModel(SequenceServiceVM sequenceServiceVM) 
        {
            this.SequenceServiceVM = sequenceServiceVM;

        }
    }
}
