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
using System.Windows.Navigation;
using System.Windows.Shapes;
using HCB.IoC;

namespace HCB.UI
{
    /// <summary>
    /// USub03.xaml에 대한 상호 작용 논리
    /// </summary>
    [View(Lifetime.Singleton)]
    public partial class USub03 : Page
    {
        public USub03(USub03ViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
