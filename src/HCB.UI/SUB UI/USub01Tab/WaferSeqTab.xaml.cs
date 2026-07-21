using System.Windows.Controls;
using HCB.IoC;

namespace HCB.UI
{
    [View(Lifetime.Scoped)]
    public partial class WaferSeqTab : UserControl
    {
        private readonly WaferSeqTabViewModel _viewModel;

        public WaferSeqTab(WaferSeqTabViewModel waferSeqTabViewModel, PreparationTab preparationTab)
        {
            _viewModel = waferSeqTabViewModel;
            this.DataContext = waferSeqTabViewModel;
            InitializeComponent();
            PreparationContent.Content = preparationTab;
            WaferMap.DieClicked += OnDieClicked;
        }

        private void OnDieClicked(object sender, DieData die)
        {
            _viewModel.SelectDie(die);
            WaferMap.HighlightDie(die, WaferSeqTabViewModel.SelectedDieBrush);
        }
    }
}
