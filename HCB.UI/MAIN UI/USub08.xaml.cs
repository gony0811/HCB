using HCB.IoC;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HCB.UI
{
    [View(Lifetime.Singleton)]
    public partial class USub08 : Page
    {
        public USub08(USub08ViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }

        private void DataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid == null)
                return;

            var dep = e.OriginalSource as DependencyObject;
            var row = VisualUpwardSearch<DataGridRow>(dep);
            if (row != null)
            {
                row.IsSelected = true;
                grid.SelectedItem = row.Item;  // ✅ 핵심: 우클릭한 행을 ViewModel로 즉시 반영
            }
        }

        private static T VisualUpwardSearch<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null && !(source is T))
                source = VisualTreeHelper.GetParent(source);
            return source as T;
        }
    }
}
