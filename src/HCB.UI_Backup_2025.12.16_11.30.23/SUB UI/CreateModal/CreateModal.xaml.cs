using System;
using System.Windows;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Data.PropertyGrid;

namespace HCB.UI
{
    public partial class CreateModal : RadWindow
    {
        public CreateModal()
        {
            InitializeComponent();
        }


        private void Save_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        private void MainGrid_AutoGeneratingPropertyDefinition(
                    object sender,
                    AutoGeneratingPropertyDefinitionEventArgs e)
        {
            // 실제 C# 속성명
            var propName = e.PropertyDefinition.SourceProperty?.Name;

            // 숨기고 싶은 속성
            if (string.Equals(propName, "ExtraSetting", StringComparison.Ordinal) ||
                string.Equals(propName, "Id", StringComparison.Ordinal))
            {
                e.Cancel = true;   // UI에서 숨기기
            }
        }
    }
}
