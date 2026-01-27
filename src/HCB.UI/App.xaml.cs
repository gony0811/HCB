using Autofac;
using HCB.Data.Entity;
using HCB.Data.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.ConversationalUI;
using Telerik.Windows.Controls.Navigation;
using Telerik.Windows.Controls.SplashScreen;

namespace HCB.UI
{
    public partial class App : Application
    {
        static public string Project = "EGGPLANT";
        private IHost? _host;
        private Mutex? _mutex;

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

            var result = MessageBox.Show("운영모드로 실행하시겠습니까? 아니오를 선택하면 설정 모드로 진입합니다.", "모드선택", MessageBoxButton.YesNo, MessageBoxImage.Question);

            // 1. 스플래시 스크린 시작
            RadSplashScreenManager.Show<SplashView>();

            // 2. 초기화 로직 실행
            // 이 위치에서 다음 단계의 이벤트를 사용하여 초기화 함수를 호출해야 합니다.
            SplashScreenUpdate("애플리케이션 구동 시작...", 0);
            

            _host = StartUp.BuildHost(e.Args);

            SplashScreenUpdate("호스트 빌드 완료.", 10);

            SplashScreenUpdate("어플리케이션 초기화 및 구동 시작", 15);



            SplashScreenUpdate("데이터베이스 연결 및 초기화...", 20);
            await StartUp.InitDatabaseAsync(_host);

            var recipeService = _host.Services.GetRequiredService<RecipeService>();
            await recipeService.Initialize();

            var userService = _host.Services.GetRequiredService<UserService>();
            await userService.InitializeAsync();
            SplashScreenUpdate("데이터베이스 연결 및 초기화 완료", 25);

            if (result == MessageBoxResult.Yes)
            {
                await InitializeApplicationAsync();

                SplashScreenUpdate("어플리케이션 초기화 완료", 100);
            }
            
            RadSplashScreenManager.Close();

            // 5. 메인 창 표시 (선택 사항)
            // ③ 메인 윈도우 실행
            var mainWindow = _host.Services.GetRequiredService<UMain>();

            FluentPalette.LoadPreset(FluentPalette.ColorVariation.Dark);
            //FluentPalette palette = FluentPalette.Palette;
            //// 2. 원하는 색상으로 강조 색상 (Accent Color) 변경
            //// 예: Telerik의 기본 파란색 대신 진한 주황색으로 변경
            //palette.AccentColor = Color.FromRgb(0x00, 0x80, 0x80); // 주황색 (Dark Orange)
            RadWindowInteropHelper.SetShowInTaskbar(mainWindow, true);

            mainWindow.Show();
            //mainWindow.Activate();
        }

        private async Task InitializeApplicationAsync()
        {
            // _host가 null일 경우 명확한 예외를 던져 CS8602 경고 제거
            if (_host is null)
            {
                throw new InvalidOperationException("호스트가 초기화되지 않았습니다. StartUp.BuildHost가 null을 반환했습니다.");
            }

            SplashScreenUpdate("장치 연결 시도 중...", 30);

            var systemMainService = _host.Services.GetRequiredService<SystemMainService>();

            await systemMainService.StartAsync();

           

            var deviceManager = _host.Services.GetRequiredService<DeviceManager>();

            // 10초 타임아웃 설정
            var timeout = TimeSpan.FromSeconds(10);
            var startTime = DateTime.Now;
            bool bConnected = false;

            while ((DateTime.Now - startTime) < timeout)
            {
                bConnected = true;
                foreach (var device in deviceManager.Devices)
                {
                    if (!device.IsConnected)
                    {
                        bConnected = false;
                        break;
                    }
                }

                if (bConnected)
                {
                    break;
                }

                // 경과 시간에 따른 진행률 업데이트 (30% -> 90%)
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                int progress = Convert.ToInt32(30 + (elapsed / timeout.TotalMilliseconds) * 60);
                SplashScreenUpdate("장치 연결 시도 중...", Math.Min(progress, 90));

                await Task.Delay(100);

                
            }

            if (!bConnected)
            {
                // 타임아웃 발생 시 처리 (예: 로그 기록, 사용자 알림 등)
                // 필요에 따라 예외를 던지거나 경고 메시지를 표시할 수 있습니다.
                // throw new TimeoutException("장치 연결 시간이 초과되었습니다.");
                SplashScreenUpdate("장치 연결 실패", 90);
            }
            else
            {
                SplashScreenUpdate("장치 연결 완료", 90);
            }

            await _host.StartAsync();
        }
           

        protected override void OnExit(ExitEventArgs e)
        {
            if (_host is null)
            {
                throw new InvalidOperationException("호스트가 초기화되지 않았습니다. StartUp.BuildHost가 null을 반환했습니다.");
            }

            if (_mutex is null)
            {
                throw new InvalidOperationException("뮤텍스가 초기화되지 않았습니다.");
            }

            using (_mutex)
            {
                _mutex.ReleaseMutex();
            }

            using (_host)
            {
                _host.StopAsync();
                var systemMainService = _host.Services.GetRequiredService<SystemMainService>();
                _ = systemMainService.StopAsync();
            }
                try { _mutex?.ReleaseMutex(); } catch { /* ignore */ }
            _mutex?.Dispose();
            _host?.Dispose();
            base.OnExit(e);
        }
    }
}
