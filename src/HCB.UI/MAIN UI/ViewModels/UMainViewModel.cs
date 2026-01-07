
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Repository;
using HCB.IoC;
using Serilog;
using Serilog.Events;
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

        public UserService UserService { get; }

        #region Sub ViewModels

        [ObservableProperty]
        private NavigationViewModel navVM;
        public SequenceServiceVM SequenceServiceVM { get; }
        #endregion


        [ObservableProperty] public ObservableCollection<LogModel> logs = new();

        [ObservableProperty] public Page currentPage;

        [ObservableProperty] private string selectedPageKey;
        //public string selectedPageKey { get; set; }

        public string LogText => string.Join("", Logs);

        public UMainViewModel(
            ILogger logger,
            LogRepository logRepository,
            USub01 uSub01, 
            USub02 uSub02,
            USub03 uSub03,
            USub04 uSub04,
            USub05 uSub05,
            USub06 uSub06,
            USub07 uSub07,
            USub08 uSub08,
            UserService userService, 
            SequenceServiceVM sequenceServiceVM,
            NavigationViewModel navVM)
        {
            this.mainPage = uSub01;
            this.parameterPage = uSub02;
            this.userPage = uSub03;
            this.logPage = uSub04;
            this.alarmPage = uSub05;
            this.motionPage = uSub06;
            this.ioPage = uSub07;
            this.devicePage = uSub08;
            this.UserService = userService;
            this.SequenceServiceVM = sequenceServiceVM;
            this.NavVM = navVM;
            this.logger = logger.ForContext<UMainViewModel>();
            this.logRepository = logRepository;
            _ = UserService.InitializeAsync();

            GridLogSink.LogReceived += OnLogReceived;

            Navigate("Main");   
        }

        private void OnLogReceived(LogModel log)
        {
            // ==============================
            // ① DB 로그(EF Core) 필터링
            // ==============================

            // SourceContext가 EFCore인 경우 저장하지 않음
            if (log.SourceContext?.Contains("Microsoft.EntityFrameworkCore") == true)
                return;

            // 메시지가 명확하게 DBCommand 로그인 경우 저장하지 않음
            if (log.Message.StartsWith("Executed DbCommand")
                || log.Message.StartsWith("Executing DbCommand"))
                return;

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
                    break;
                case "PARAMETER":
                    CurrentPage = parameterPage;
                    break;
                case "USER":
                    CurrentPage = userPage;
                    break;
                case "LOG":
                    CurrentPage = logPage;
                    break;
                case "ALARM":
                    CurrentPage = alarmPage;
                    break;
                case "MOTION":
                    CurrentPage = motionPage;
                    break;
                case "IO":
                    CurrentPage = ioPage;
                    break;
                case "DEVICE":
                    CurrentPage = devicePage;
                    break;
                default:
                    //CurrentPage = App.Container.Resolve<USub01>();
                    break;
            }

            logger.Information(new UILog(page: key, user: UserService.CurrentAuthority.Name, message: "Navigate").ToString());
        }

        [RelayCommand]
        public void ProgramExit()
        {
            App.Current.Shutdown();
        }
    }
}
