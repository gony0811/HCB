using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System.Threading.Tasks;
using Serilog;
using System.Threading;
using System.Drawing.Printing;
using System;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class ManualTabViewModel : ObservableObject
    {
        private ILogger _logger;
        private SequenceService _sequenceService;
        private DeviceManager _deviceManager;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        // D-Table
        [ObservableProperty]
        private IAxis? dyAxis;

        [ObservableProperty]
        private IAxis? pyAxis;


        [ObservableProperty]
        private IAxis? bxAxis;

        [ObservableProperty]
        private IAxis? btAxis;

        [ObservableProperty]
        private IAxis? bz1Axis;

        [ObservableProperty]
        private IAxis? bz2Axis;

        [ObservableProperty]
        private IAxis? wyAxis;

        [ObservableProperty]
        private IAxis? wtAxis;

        [ObservableProperty] private bool isDieLoading;
        [ObservableProperty] private bool isWaferLoading;

        [ObservableProperty] private bool isDieStandby;
        [ObservableProperty] private bool isWaferStandby;

        // 생성자에서 IoC로부터 팩토리 함수 주입
        public ManualTabViewModel(
            ILogger logger,
            DeviceManager deviceManager,
            SequenceService sequenceService)
        {
            _logger = logger.ForContext<ManualTabViewModel>();
            _deviceManager = deviceManager;
            _sequenceService = sequenceService;
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                DyAxis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.D_Y);
                PyAxis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.P_Y);
                BxAxis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_X);
                Bz1Axis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_Z);
                Bz2Axis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.h_z);
                WyAxis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.W_Y);
                WtAxis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.W_T);
                BtAxis = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_T);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "ManualTabViewModel 초기화 중 오류 발생");
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
