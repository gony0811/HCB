using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Collections.ObjectModel;
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
        public RecipeService RecipeService { get; }

        public ObservableCollection<LabelValue> RunInfo { get; }

        [ObservableProperty]
        private RecipeDto selectedRecipe;

        [ObservableProperty]
        private bool isInitializing;

        [ObservableProperty]
        private bool isRunning;

        [ObservableProperty]
        private bool isStopping;

        [ObservableProperty]
        private bool isInitialize;

        public SequenceServiceVM SequenceServiceVM => _sequenceServiceVM;

        public AutoTabViewModel(RunInformation runInformation, RunningStatus runningStatus, OperationService operationService, SequenceService sequenceService, AlarmService alarmService, RecipeService recipeService, SequenceServiceVM sequenceServiceVM)
        {
            RunInformation = runInformation;
            RunningStatus = runningStatus;
            _sequenceService = sequenceService;
            _operationService = operationService;
            _cancellationTokenSource.TryReset();
            AlarmService = alarmService;
            RecipeService = recipeService;
            _sequenceServiceVM = sequenceServiceVM;

            RunInfo = new ObservableCollection<LabelValue>
            {
                new LabelValue("Operator ID", RunInformation.OperatorId),
                new LabelValue("Lot ID", RunInformation.LotId),
                new LabelValue("Wafer Size", RunInformation.WaferSize.ToString()),
                new LabelValue("BTM Die Count", RunInformation.TopDieCount.ToString()),
                new LabelValue("Top Die Count", RunInformation.BottomDieCount.ToString())
            };
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
            var tcs = new TaskCompletionSource<bool>();
            var dialog = new VacuumSelector();
            dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;

            dialog.Closed += (s, e) => tcs.SetResult(dialog.DialogResult == true);
            dialog.ShowDialog();

            bool confirmed = await tcs.Task;
            if (!confirmed) return;

            var topList = dialog.TopDieVacuums;  // List
            var botList = dialog.BotDieVacuums;  // List

            IsRunning = true;
            try
            {
                foreach (var (top, bot) in topList.Zip(botList))
                    await _sequenceService.MachineStartAsync(top, bot, _cancellationTokenSource.Token);
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

        [RelayCommand]
        public void ShowAccuracyData()
        {
            // TODO: 실시간 Accuracy Data 팝업 or 네비게이션
        }

    }
}
