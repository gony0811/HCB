using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
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
        public void MachineRun()
        {
            Task.Run(async () => { 
                IsRunning = true;
                try
                {
                    await this._sequenceService.MachineStartAsync(1, 5, _cancellationTokenSource.Token);
                }catch(Exception e)
                {
                    
                }
                
            });
            IsRunning = false;
        }

        [RelayCommand]
        public void MachineStop()
        {
            IsStopping = true;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new();
            IsStopping = false;
        }

        [RelayCommand]
        public void MachineReset()
        {
            Task.Run(async () => await AlarmService.ResetAllAlarms());
        }
    }
}
