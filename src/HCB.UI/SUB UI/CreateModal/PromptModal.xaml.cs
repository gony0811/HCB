using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    public partial class PromptModal : RadWindow
    {
        public PromptModal()
        {
            InitializeComponent();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // 편집 중인 칸의 값이 아직 반영되지 않았으면 먼저 반영 (터치/Enter 없이 저장을 누른 경우)
            if (Keyboard.FocusedElement is TextBox tb)
                tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            MainGrid.CommitEdit();

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
