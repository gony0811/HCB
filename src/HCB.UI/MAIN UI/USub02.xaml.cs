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

            // 파라미터 표 하단/행 버튼 → VM 명령 (XAML 상대 바인딩 대신 직접 연결)
            ParamEditorView.CreateCommand = vm.CreateParamCommand;
            ParamEditorView.DetailCommand = vm.UpdateParamCommand;
            ParamEditorView.DeleteCommand = vm.DeleteParamCommand;
            ParamEditorView.SaveCommand = vm.SaveParamChangesCommand;
            ParamEditorView.AddMissingCommand = vm.AddMissingRequiredCommand;
        }
    }
}
