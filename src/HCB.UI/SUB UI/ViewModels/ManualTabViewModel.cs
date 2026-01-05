using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System.Threading.Tasks;
using Serilog;
using System.Threading;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class ManualTabViewModel : ObservableObject
    {
        private readonly ILogger _logger;
        private readonly SequenceHelper _sequenceHelper;
        private readonly SequenceService _sequenceService;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        // D-Table
        [ObservableProperty]
        private PositionTableViewModel dyAxisTable = new PositionTableViewModel("D-Y Axis");

        [ObservableProperty]
        private MotorStatusTableViewModel dyMotorStatusTable = new MotorStatusTableViewModel("Die DY");


        // P-Table
        [ObservableProperty]
        private PositionTableViewModel pyAxisTable = new PositionTableViewModel("P-Y Axis");

        [ObservableProperty]
        private MotorStatusTableViewModel pyMotorStatusTable = new MotorStatusTableViewModel("P-Y");



        // B-Head
        [ObservableProperty] private PositionTableViewModel bxAxisTable = new PositionTableViewModel("B-X Axis");
        [ObservableProperty] private MotorStatusTableViewModel bxMotorStatusTable = new MotorStatusTableViewModel("B-X");


        [ObservableProperty] private PositionTableViewModel bz1AxisTable = new PositionTableViewModel("B-Z1 Axis");
        [ObservableProperty] private MotorStatusTableViewModel bz1MotorStatusTable = new MotorStatusTableViewModel("B-Z1");

        [ObservableProperty] private PositionTableViewModel bz2AxisTable = new PositionTableViewModel("B-Z2 Axis");

        [ObservableProperty] private MotorStatusTableViewModel bz2MotorStatusTable = new MotorStatusTableViewModel("B-Z2");

        // W-Table
        [ObservableProperty] private PositionTableViewModel wyAxisTable = new PositionTableViewModel("W-Y Axis");
        [ObservableProperty] private MotorStatusTableViewModel wyMotorStatusTable = new MotorStatusTableViewModel("W-Y");

        [ObservableProperty] private PositionTableViewModel wtAxisTable = new PositionTableViewModel("W-T Axis");
        [ObservableProperty] private MotorStatusTableViewModel wtMotorStatusTable = new MotorStatusTableViewModel("W-T");


        [ObservableProperty] private bool isDieLoading;
        [ObservableProperty] private bool isWaferLoading;

        [ObservableProperty] private bool isDieStandby;
        [ObservableProperty] private bool isWaferStandby;
        public ManualTabViewModel(ILogger logger, SequenceHelper sequenceHelper)
        {
            _logger = logger.ForContext<ManualTabViewModel>();
            _sequenceHelper = sequenceHelper;
            Initialize();
        }

        private void Initialize()
        {
            DyAxisTable.AddRow(new PositionTableRowModel("READY POSITION", 10.0, 100));
            DyAxisTable.AddRow(new PositionTableRowModel("WORKING POSITION", 10.0, 100));
            DyAxisTable.AddRow(new PositionTableRowModel("TEST POSITION", 10.0, 100));


            PyAxisTable.AddRow(new PositionTableRowModel("READY POSITION", 10.0, 100));
            PyAxisTable.AddRow(new PositionTableRowModel("WORKING POSITION", 10.0, 100));


            BxAxisTable.AddRow(new PositionTableRowModel("READY POSITION", 10.0, 100));
            BxAxisTable.AddRow(new PositionTableRowModel("WORKING POSITION", 10.0, 100));


            Bz1AxisTable.AddRow(new PositionTableRowModel("READY POSITION", 10.0, 100));
            Bz1AxisTable.AddRow(new PositionTableRowModel("WORKING POSITION", 10.0, 100));


            Bz2AxisTable.AddRow(new PositionTableRowModel("READY POSITION", 10.0, 100));
            Bz2AxisTable.AddRow(new PositionTableRowModel("WORKING POSITION", 10.0, 100));


            WyAxisTable.AddRow(new PositionTableRowModel("READY POSITION", 10.0, 100));
            WyAxisTable.AddRow(new PositionTableRowModel("WORKING POSITION", 10.0, 100));


            WtAxisTable.AddRow(new PositionTableRowModel("READY POSITION", 10.0, 100));
            WtAxisTable.AddRow(new PositionTableRowModel("WORKING POSITION", 10.0, 100));
        }

        [RelayCommand]
        public void DTableLoading()
        {
            Task.Run(async () =>
            {
                IsDieLoading = true;
                await this._sequenceService.DTableLoading(_cancellationTokenSource.Token);
                IsDieLoading = false;
            });
        }

        [RelayCommand]
        public void WTableLoading()
        {
            Task.Run(async () =>
            {
                IsWaferLoading = true;
                await this._sequenceService.WTableLoading(_cancellationTokenSource.Token);
                IsWaferLoading = false;
            });
        }
    }
}
