using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class AutoTabViewModel : ObservableObject
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly SequenceService _sequenceService;
        private readonly OperationService _operationService;
        public readonly SequenceServiceVM _sequenceServiceVM;

        public RunInformation RunInformation { get; }
        public RunningStatus RunningStatus { get; }
        public AlarmService AlarmService { get; }

        [ObservableProperty]
        private bool isInitializing;

        [ObservableProperty]
        private bool isRunning;

        [ObservableProperty]
        private bool isStopping;

        [ObservableProperty]
        private bool isInitialize;

        [ObservableProperty]
        private int pressureTime = 1000;

        [ObservableProperty]
        private int blowTime = 1000;

        [ObservableProperty]
        private int waitTime = 8000;

        public AutoTabViewModel(RunInformation runInformation, RunningStatus runningStatus, OperationService operationService, SequenceService sequenceService, AlarmService alarmService)
        {
            RunInformation = runInformation;
            RunningStatus = runningStatus;
            _sequenceService = sequenceService;
            _operationService = operationService;
            _cancellationTokenSource.TryReset();
            AlarmService = alarmService;
        }

        [RelayCommand]
        public void Loaded()
        {
            _operationService.Status.Operation = OperationMode.Auto;
        }

        [RelayCommand]      
        public void Unloaded() 
        {
            _operationService.Status.Operation = OperationMode.Manual;
        }

        [RelayCommand]
        public void Running()
        {
            RunningStatus.RunningTimeRange.StartTimer();
        }

        [RelayCommand]
        public void Loading()
        {
            RunningStatus.LoadingTimeRange.StartTimer();
        }

        [RelayCommand]
        public void MachineInit()
        {
            Task.Run(async () =>
            {
                IsInitializing = true;
                try
                {
                    await this._sequenceService.MachineInitAsync(_cancellationTokenSource.Token);
                    await this._sequenceService.Init_Load(_cancellationTokenSource.Token);
                    await this._sequenceService.WaferAndDieLoading(eOnOff.Off, _cancellationTokenSource.Token);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RadWindow.Confirm(new DialogParameters
                        {
                            Header = "로딩중",
                            Content = "로딩 완료 후 확인을 눌러주세요",
                            Closed = async (s, e) =>
                            {
                                if (e.DialogResult == true)
                                {
                                    await _sequenceService.WaferAndDieLoading(eOnOff.On, _cancellationTokenSource.Token);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        RadWindow.Alert("로딩이 완료되었습니다");
                                    });
                                }
                                else
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        RadWindow.Alert("로딩이 취소되었습니다");
                                    });
                                }
                            }
                        });
                    });
                }
                catch(Exception e)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RadWindow.Alert(e.Message);
                    });
                }

                IsInitializing = false;
            });
        }

        [RelayCommand]
        public async Task MachineRun()
        {
            // RelayCommand는 이미 UI 스레드에서 실행됨
            // Dispatcher 불필요 — 그냥 직접 열면 됨
            var tcs = new TaskCompletionSource<bool>();
            int top = 0, bot = 0;

            var dialog = new VacuumSelector();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            dialog.Closed += (s, e) =>
            {
                if (dialog.DialogResult == true
                    && dialog.TopDieVacuum.HasValue
                    && dialog.BotDieVacuum.HasValue)
                {
                    top = dialog.TopDieVacuum.Value;
                    bot = dialog.BotDieVacuum.Value;
                    tcs.SetResult(true);
                }
                else
                {
                    tcs.SetResult(false);
                }
            };

            dialog.ShowDialog(); // UI 스레드에서 직접 호출

            bool confirmed = await tcs.Task;
            if (!confirmed) return;

            IsRunning = true;
            try
            {
                int[] delayTimes = { PressureTime ,BlowTime, WaitTime };
                await _sequenceService.MachineStartAsync(top, bot, delayTimes, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { }
            finally
            {
                IsRunning = false;
            }
        }

        [RelayCommand]
        public async Task MachineStop()
        {
            if (IsStopping) return; // 중복 호출 방어

            IsStopping = true;

            var oldCts = _cancellationTokenSource;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                oldCts.Cancel();
                await _sequenceService.StopAsync(oldCts.Token);
            }
            finally
            {
                oldCts.Dispose();
                IsStopping = false;
            }
        }

        [RelayCommand]
        public void MachineReset()
        {
            Task.Run(async () => await AlarmService.ResetAllAlarms());
        }

    }
}
