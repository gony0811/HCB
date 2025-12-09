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
using Serilog;

namespace HCB.UI
{
    /// <summary>
    /// USub02.xaml에 대한 상호 작용 논리
    /// </summary>
    [View(Lifetime.Singleton)]
    public partial class USub02 : Page
    {
        private ILogger logger;
        private readonly USub02ViewModel vm;

        public USub02(ILogger logger, USub02ViewModel vm)
        {
            this.logger = logger;
            this.vm = vm;

            this.DataContext = vm;
            InitializeComponent();
        }
    }
}
