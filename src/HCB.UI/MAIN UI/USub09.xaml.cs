using System.Windows.Controls;
using HCB.IoC;
using Serilog;

namespace HCB.UI
{
    /// <summary>
    /// USub09.xaml에 대한 상호 작용 논리
    /// </summary>
    [View(Lifetime.Singleton)]
    public partial class USub09 : Page
    {
        private ILogger logger;
        private readonly USub09ViewModel vm;

        public USub09(ILogger logger, USub09ViewModel vm)
        {
            this.logger = logger;
            this.vm = vm;

            this.DataContext = vm;
            InitializeComponent();
        }
    }
}
