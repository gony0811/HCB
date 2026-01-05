using System.Windows;
using System.Windows.Controls;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.GridView; // 이 줄을 추가하세요.

namespace HCB.UI
{
    /// <summary>
    /// Interaction logic for PositionTable.xaml
    /// </summary>
    public partial class PositionTable : UserControl
    {
        public PositionTable()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. 현재 버튼이 포함된 GridView를 찾습니다.
            var gridView = this.FindChildByType<RadGridView>(); // Telerik 확장 메서드 또는 VisualTreeHelper 사용

            if (gridView != null)
            {
                // 2. 현재 편집 중인 모든 내용을 강제로 커밋(Commit)합니다.
                gridView.CommitEdit();
            }
        }
    }
}
