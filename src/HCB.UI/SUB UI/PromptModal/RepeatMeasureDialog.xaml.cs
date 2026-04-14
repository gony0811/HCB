using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HCB.UI 
{
    public partial class RepeatMeasureDialog : Window
    {
        public int RepeatCount { get; private set; } = 5;

        public RepeatMeasureDialog(int defaultCount = 5)
        {
            InitializeComponent();
            RepeatCountBox.Text = defaultCount.ToString();
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(RepeatCountBox.Text, out int count) && count > 0)
            {
                RepeatCount = count;
                DialogResult = true;
            }
            else
            {
                RepeatCountBox.BorderBrush = Brushes.Red; // 간단한 validation
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
