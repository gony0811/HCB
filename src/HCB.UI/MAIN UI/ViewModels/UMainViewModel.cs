
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Repository;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class UMainViewModel : ObservableObject
    {

        private ILogger logger;
        private LogRepository logRepository;

        private Page mainPage;
        private Page parameterPage;
        private Page userPage;
        private Page logPage;
        private Page alarmPage;
        private Page motionPage;
        private Page ioPage;
        private Page devicePage;

        private UserService UserService;

        private NavigationViewModel NavVM;

        [ObservableProperty] public ObservableCollection<LogModel> logs = new();

        [ObservableProperty] public Page currentPage;

        [ObservableProperty] private string selectedPageKey;
        //public string selectedPageKey { get; set; }

        public string LogText => string.Join("", Logs);

        public UMainViewModel(
            ILogger logger,
            LogRepository logRepository,
            USub01 uSub01, 
            USub08 uSub08,
            UserService userService, 
            NavigationViewModel navVM)
        {
            this.mainPage = uSub01;
            this.devicePage = uSub08;
            this.UserService = userService;
            this.NavVM = navVM;
            this.logger = logger.ForContext<UMainViewModel>();
            this.logRepository = logRepository;
            _ = UserService.InitializeAsync();

            GridLogSink.LogReceived += OnLogReceived;

            Navigate("Main");   
        }

        private void OnLogReceived(LogModel log)
        {
            // UI 스레드에서 컬렉션 업데이트 (필수!)
            App.Current.Dispatcher.InvokeAsync(async () =>
            {
                Logs.Insert(0, log);

                // 로그가 너무 많이 쌓이면 메모리 관리 (예: 1000줄 유지)
                if (Logs.Count > 1000) Logs.RemoveAt(Logs.Count - 1);

                try
                {                     
                    await logRepository.AddAsync(log);
                }
                catch (Exception ex)
                {
                    logger.Error("로그 저장 중 오류 발생: {ErrorMessage}", ex.Message);
                }
            });
        }

        [RelayCommand]
        public void Navigate(string key)
        {
            key = key.ToUpper();
            SelectedPageKey = key;

            switch(key)
            {
                case "MAIN":
                    CurrentPage = mainPage;

                    logger.Information("애플리케이션 시작됨.");
                    break;
                case "PARAMETER":
                    //CurrentPage = App.Container.Resolve<USub02>();
                    break;
                case "USER":
                    //CurrentPage = App.Container.Resolve<USub03>();
                    break;
                case "LOG":
                    //CurrentPage = App.Container.Resolve<USub04>();
                    break;
                case "ALARM":
                    //CurrentPage = App.Container.Resolve<USub05>();
                    break;
                case "MOTION":
                    //CurrentPage = App.Container.Resolve<USub06>();
                    break;
                case "IO":
                    //CurrentPage = App.Container.Resolve<USub07>();
                    break;
                case "DEVICE":
                    CurrentPage = devicePage;
                    break;
                default:
                    //CurrentPage = App.Container.Resolve<USub01>();
                    break;
            }
        }

        [RelayCommand]
        public void ProgramExit()
        {
            App.Current.Shutdown();
        }
    }
}
