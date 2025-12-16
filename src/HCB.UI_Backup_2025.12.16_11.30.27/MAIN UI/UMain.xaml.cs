
using HCB.IoC;
using System.Linq;
using System.Windows;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    [View(Lifetime.Singleton)]
    public partial class UMain : RadWindow
    {
        // [추가] 디자이너(미리보기)를 위한 기본 생성자
        public UMain()
        {
            InitializeComponent();

            // 디자인 타임에만 스타일 적용 (선택 사항)
            StyleManager.SetTheme(this, new Windows11Theme());
        }

        public UMain(UMainViewModel vm) : this()
        {        
            this.DataContext = vm;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            App.Current.Shutdown();
        }
    }
}
