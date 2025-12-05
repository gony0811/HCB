using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Navigation;
using Telerik.Windows.Controls.SplashScreen;

namespace HCB.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static public string Project = "EGGPLANT";
        private IHost _host;
        private Mutex _mutex;

        public App()
        {
            this.InitializeComponent();
        }

        protected void SplashScreenUpdate(string content, double progressValue)
        {
            var context = (SplashScreenDataContext)RadSplashScreenManager.SplashScreenDataContext;
            context.IsIndeterminate = false;
            context.Content = content;
            context.ProgressValue = progressValue;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            SQLitePCL.Batteries_V2.Init();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);

            // ① 단일 실행 보장: 뮤텍스는 필드로 잡고, 종료 때까지 유지(+Release는 OnExit에서)
            _mutex = new Mutex(true, Project, out bool isNew);
            if (!isNew)
            {
                MessageBox.Show($"이미 {Project} 이(가) 실행 중입니다.", "에러",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 1. 스플래시 스크린 시작
            RadSplashScreenManager.Show<SplashView>();

            // 2. 초기화 로직 실행
            // 이 위치에서 다음 단계의 이벤트를 사용하여 초기화 함수를 호출해야 합니다.
            SplashScreenUpdate("애플리케이션 구동 시작...", 0);
            

            _host = StartUp.BuildHost(e.Args);
            SplashScreenUpdate("호스트 빌드 완료.", 10);

            SplashScreenUpdate("데이터베이스 연결 및 초기화...", 11);
            await StartUp.InitDatabaseAsync(_host);

            SplashScreenUpdate("데이터베이스 연결 및 초기화 완료", 20);

            SplashScreenUpdate("어플리케이션 초기화 및 구동 시작", 21);

            await InitializeApplicationAsync();

            SplashScreenUpdate("어플리케이션 초기화 완료", 100);
            
            RadSplashScreenManager.Close();

            // 5. 메인 창 표시 (선택 사항)
            // ③ 메인 윈도우 실행
            var mainWindow = _host.Services.GetRequiredService<UMain>();

            FluentPalette.LoadPreset(FluentPalette.ColorVariation.Dark);
            FluentPalette palette = FluentPalette.Palette;
            // 2. 원하는 색상으로 강조 색상 (Accent Color) 변경
            // 예: Telerik의 기본 파란색 대신 진한 주황색으로 변경
            palette.AccentColor = Color.FromRgb(0x00, 0x80, 0x80); // 주황색 (Dark Orange)
            RadWindowInteropHelper.SetShowInTaskbar(mainWindow, true);

            mainWindow.Show();
            //mainWindow.Activate();
        }

        private async Task InitializeApplicationAsync()
        {
            SplashScreenUpdate("백그라운드 서비스 시작", 30);
            await _host.StartAsync();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            using (_mutex)
            {
                _mutex.ReleaseMutex();
            }

            using (_host)
            {
                _host.StopAsync().GetAwaiter().GetResult();
            }
                try { _mutex?.ReleaseMutex(); } catch { /* ignore */ }
            _mutex?.Dispose();
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
