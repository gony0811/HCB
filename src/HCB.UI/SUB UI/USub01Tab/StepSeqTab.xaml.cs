using System.Windows.Controls;
using HCB.IoC;

namespace HCB.UI
{
    /// <summary>
    /// StepSeqTab.xaml에 대한 상호 작용 논리
    /// </summary>
    [View(Lifetime.Scoped)]
    public partial class StepSeqTab : UserControl
    {
        public StepSeqTab(StepSeqTabViewModel stepSeqTabViewModel)
        {
            this.DataContext = stepSeqTabViewModel;
            InitializeComponent();
        }
    }
}
