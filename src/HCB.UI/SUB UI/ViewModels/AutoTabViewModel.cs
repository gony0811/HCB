using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using Serilog;
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

        private readonly ILogger _logger;
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

        private bool _isSettingUseRecipe;

        public SequenceServiceVM SequenceServiceVM => _sequenceServiceVM;

        public AutoTabViewModel(RunInformation runInformation, RunningStatus runningStatus, OperationService operationService, SequenceService sequenceService, AlarmService alarmService, RecipeService recipeService, SequenceServiceVM sequenceServiceVM, ILogger logger)
        {
            RunInformation = runInformation;
            RunningStatus = runningStatus;
            _sequenceService = sequenceService;
            _operationService = operationService;
            _cancellationTokenSource.TryReset();
            AlarmService = alarmService;
            RecipeService = recipeService;
            _sequenceServiceVM = sequenceServiceVM;
            _logger = logger.ForContext<AutoTabViewModel>();

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
        public async Task MachineInit()
        {
            if (IsInitializing) return;
            IsInitializing = true;
            try
            {
                SequenceServiceVM.ResetInitProgress();
                await _sequenceService.MachineInitAsync(_cancellationTokenSource.Token);
                IsInitialize = true;
                _logger.Information("Machine Init 완료");
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Machine Init 취소됨");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Machine Init Failed");
            }
            finally
            {
                IsInitializing = false;
            }
        }

        [RelayCommand]
        public async Task MachineRun()
        {
            var tcs = new TaskCompletionSource<bool>();
            var dialog = new VacuumSelector();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            dialog.Closed += (s, e) => tcs.SetResult(dialog.DialogResult == true);
            dialog.ShowDialog();

            bool confirmed = await tcs.Task;
            if (!confirmed) return;

            var topList = dialog.TopDieVacuums;
            var botList = dialog.BotDieVacuums;

            if (topList.Count == 0 || botList.Count == 0)
            {
                _logger.Warning("Die 선택이 없습니다");
                return;
            }

            IsRunning = true;
            var ct = _cancellationTokenSource.Token;
            try
            {
                foreach (var (topDie, btmDie) in topList.Zip(botList))
                {
                    ct.ThrowIfCancellationRequested();

                    // Bottom: 저배율 보정 + Pickup + Place
                    var btmAlign = await _sequenceService.BtmCarrierAlign(btmDie, MarkType.DIE_CENTER_BOTTOM, ct);
                    await _sequenceService.DTablePickup(DieType.BOTTOM, btmDie, btmAlign, ct);
                    await _sequenceService.BtmDieDrop(1, ct);

                    // Top: 저배율 보정 + Pickup
                    var topAlign = await _sequenceService.TopLowAlign(topDie, ct);
                    await _sequenceService.DTablePickup(DieType.TOP, topDie, topAlign, ct);

                    // Top: 고배율 측정 (Top → Btm)
                    var data = new AlignData { AvgMove = true, Use2DMapping = true };
                    data = await _sequenceService.TopHighAlign(data, ct);
                    data = await _sequenceService.BtmHighAlign(data, ct);

                    // 보정 + 본딩
                    await _sequenceService.TopPlace(data, ct);
                    await _sequenceService.BondingCorr(data, ct);
                    var bondingData = new ObservableCollection<BondingDataPoint>();
                    await _sequenceService.BondingPress(bondingData, ct);

                    await _sequenceService.Init_Head(ct);
                    _logger.Information("Auto Run 완료 — Top:{Top} Btm:{Btm}", topDie, btmDie);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Auto Run 취소됨");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Auto Run Failed");
            }
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

        partial void OnSelectedRecipeChanged(RecipeDto value)
        {
            if (value == null) return;
            _ = SetUseRecipeAsync(value);
        }

        private async Task SetUseRecipeAsync(RecipeDto recipe)
        {
            if (_isSettingUseRecipe) return;

            _isSettingUseRecipe = true;
            try
            {
                await RecipeService.SetUseRecipeAsync(recipe);
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => RadWindow.Alert(ex.Message));
            }
            finally
            {
                _isSettingUseRecipe = false;
            }
        }

    }
}
