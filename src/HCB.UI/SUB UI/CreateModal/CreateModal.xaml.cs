using System.Windows;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    public partial class CreateModal : Window
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
    }
}
