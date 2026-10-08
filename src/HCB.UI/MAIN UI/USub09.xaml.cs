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

            // 파라미터 표 하단/행 버튼 → VM 명령 (XAML 상대 바인딩 대신 직접 연결)
            ParamEditorView.CreateCommand = vm.CreateParamCommand;
            ParamEditorView.DetailCommand = vm.UpdateParamCommand;
            ParamEditorView.DeleteCommand = vm.DeleteParamCommand;
            ParamEditorView.SaveCommand = vm.SaveChangesCommand;
        }
    }
}
