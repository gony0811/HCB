using System.Windows.Controls;

namespace HCB.UI
{
    // StepSeq / WaferSeq 탭이 공유하는 우측 사이드바.
    // DataContext는 StepSeqTabViewModel (Wafer 탭은 {Binding StepSeqTab}로 지정).
    public partial class SeqSidebar : UserControl
    {
        public SeqSidebar()
        {
            InitializeComponent();
        }
    }
}
