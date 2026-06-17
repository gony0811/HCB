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
            
        }

        [RelayCommand]
        public async Task MachineRun()
        {
        }

        [RelayCommand]
        public async Task MachineStop()
        {
           
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
