using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System.Threading.Tasks;
using Serilog;
using System.Threading;
using System.Linq;
using System;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class ManualTabViewModel : ObservableObject
    {
        private readonly ILogger _logger;
        private readonly SequenceHelper _sequenceHelper;
        private readonly SequenceService _sequenceService;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private DeviceManager deviceManager;

        // D-Table
        [ObservableProperty]
        private IAxis? dyAxis = new DAxis { Name = "D-Y Axis" };

        [ObservableProperty]
        private IAxis? pyAxis = new DAxis { Name = "P-Y Axis" };


        [ObservableProperty]
        private IAxis? bxAxis = new DAxis { Name = "B-X Axis" };

        [ObservableProperty]
        private IAxis? bz1Axis = new DAxis { Name = "B-Z1 Axis" };

        [ObservableProperty]
        private IAxis? bz2Axis = new DAxis { Name = "B-Z2 Axis" };

        [ObservableProperty]
        private IAxis? wyAxis = new DAxis { Name = "W-Y Axis" };

        [ObservableProperty]
        private IAxis? wtAxis = new DAxis { Name = "W-T Axis" };

        [ObservableProperty] private bool isDieLoading;
        [ObservableProperty] private bool isWaferLoading;

        [ObservableProperty] private bool isDieStandby;
        [ObservableProperty] private bool isWaferStandby;
        public ManualTabViewModel(ILogger logger, SequenceHelper sequenceHelper, DeviceManager deviceManager)
        {
            _logger = logger.ForContext<ManualTabViewModel>();
            _sequenceHelper = sequenceHelper;
            this.deviceManager = deviceManager;
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                IMotionDevice pmac = deviceManager.GetDevice<IMotionDevice>("Pmac");
                DyAxis = pmac.MotionList.FirstOrDefault(x => x.Name == "D-Y Axis");
                PyAxis = pmac.MotionList.FirstOrDefault(x => x.Name == "P-Y Axis");
                BxAxis = pmac.MotionList.FirstOrDefault(x => x.Name == "B-X Axis");
                Bz1Axis = pmac.MotionList.FirstOrDefault(x => x.Name == "B-Z1 Axis");
                Bz2Axis = pmac.MotionList.FirstOrDefault(x => x.Name == "B-Z2 Axis");
                WyAxis = pmac.MotionList.FirstOrDefault(x => x.Name == "W-Y Axis");
                WtAxis = pmac.MotionList.FirstOrDefault(x => x.Name == "W-T Axis");
            }
            catch(Exception e)
            {

            }
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
