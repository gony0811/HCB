using System.Windows.Controls;
using HCB.IoC;

namespace HCB.UI
{
    /// <summary>
    /// PreparationTab.xaml에 대한 상호 작용 논리
    /// </summary>
    [View(Lifetime.Transient)]
    public partial class PreparationTab : UserControl
    {
        public PreparationTab(StepSeqTabViewModel stepSeqTabViewModel)
        {
            this.DataContext = stepSeqTabViewModel;
            InitializeComponent();
        }
    }
}
