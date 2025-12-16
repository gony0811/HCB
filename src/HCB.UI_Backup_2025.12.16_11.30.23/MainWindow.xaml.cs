using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HCB.IoC;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    [View(Lifetime.Singleton)]
    public partial class MainWindow : RadWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
