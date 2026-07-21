using System.Windows.Controls;
using HCB.IoC;

namespace HCB.UI
{
    /// <summary>
    /// WaferSeqTab.xaml에 대한 상호 작용 논리
    /// </summary>
    [View(Lifetime.Scoped)]
    public partial class WaferSeqTab : UserControl
    {
        public WaferSeqTab(WaferSeqTabViewModel waferSeqTabViewModel, PreparationTab preparationTab)
        {
            this.DataContext = waferSeqTabViewModel;
            InitializeComponent();
            PreparationContent.Content = preparationTab;
        }
    }
}
