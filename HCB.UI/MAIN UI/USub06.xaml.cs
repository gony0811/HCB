
using Autofac;
using HCB.IoC;
using System.Windows.Controls;
using System.Windows.Input;
using Telerik.Windows.Controls.GridView;

namespace HCB.UI
{
    [View(Lifetime.Singleton)]
    public partial class USub06 : Page
    {
        public USub06(USub06ViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }

        private void OnRowRightClickSelection(object sender, MouseButtonEventArgs e)
        {
            if (sender is GridViewRow row)
            {
                // 1. 해당 행을 선택 상태로 변경
                row.IsSelected = true;

                // 2. 포커스를 주어 그리드가 해당 아이템을 SelectedItem으로 인식하게 함
                row.Focus();

                // (선택 사항) 만약 여러 개가 선택되는 모드라면 기존 선택을 해제하고 싶을 때:
                // PositionDataGrid.SelectedItem = row.DataContext;
            }
        }
    }
}
