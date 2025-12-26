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
        private DieAxisTableViewModel dyAxisTable = new DieAxisTableViewModel("D-Y Axis");


        // P-Table
        [ObservableProperty]
        private DieAxisTableViewModel pyAxisTable = new DieAxisTableViewModel("P-Y Axis");



        // B-Head
        [ObservableProperty] private DieAxisTableViewModel bxAxisTable = new DieAxisTableViewModel("B-X Axis");


        [ObservableProperty] private DieAxisTableViewModel bz1AxisTable = new DieAxisTableViewModel("B-Z1 Axis");

        [ObservableProperty] private DieAxisTableViewModel bz2AxisTable = new DieAxisTableViewModel("B-Z2 Axis");


        // W-Table
        [ObservableProperty] private DieAxisTableViewModel wyAxisTable = new DieAxisTableViewModel("W-Y Axis");

        [ObservableProperty] private DieAxisTableViewModel wtAxisTable = new DieAxisTableViewModel("W-T Axis");


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
            DyAxisTable.AddRow(new DieAxisRowModel("READY POSITION", 10.0, 100));
            DyAxisTable.AddRow(new DieAxisRowModel("WORKING POSITION", 10.0, 100));


            PyAxisTable.AddRow(new DieAxisRowModel("READY POSITION", 10.0, 100));
            PyAxisTable.AddRow(new DieAxisRowModel("WORKING POSITION", 10.0, 100));


            BxAxisTable.AddRow(new DieAxisRowModel("READY POSITION", 10.0, 100));
            BxAxisTable.AddRow(new DieAxisRowModel("WORKING POSITION", 10.0, 100));


            Bz1AxisTable.AddRow(new DieAxisRowModel("READY POSITION", 10.0, 100));
            Bz1AxisTable.AddRow(new DieAxisRowModel("WORKING POSITION", 10.0, 100));


            Bz2AxisTable.AddRow(new DieAxisRowModel("READY POSITION", 10.0, 100));
            Bz2AxisTable.AddRow(new DieAxisRowModel("WORKING POSITION", 10.0, 100));


            WyAxisTable.AddRow(new DieAxisRowModel("READY POSITION", 10.0, 100));
            WyAxisTable.AddRow(new DieAxisRowModel("WORKING POSITION", 10.0, 100));


            WtAxisTable.AddRow(new DieAxisRowModel("READY POSITION", 10.0, 100));
            WtAxisTable.AddRow(new DieAxisRowModel("WORKING POSITION", 10.0, 100));
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
