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

        [RelayCommand]
        public async Task MovePosition(Tuple<IAxis, DMotionPosition>? parameters)
        {
            if (parameters == null) return;

            var axis = parameters.Item1;
            var position = parameters.Item2;

            if (axis == null || position == null) return;

            try
            {
                if (!axis.IsEnabled)
                {
                    _logger.Warning("{AxisName} is not enabled.", axis.Name);
                    return;
                }

                if (axis.IsBusy)
                {
                    _logger.Warning("{AxisName} is busy.", axis.Name);
                    return;
                }

                await axis.Move(MoveType.Absolute, velocity: position.Speed, position: position.Position);

                _logger.Information("Move {AxisName} to {Position} at Speed {Speed}", axis.Name, position.Position, position.Speed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Move Position Error for {AxisName}", axis.Name);
            }
        }

        [RelayCommand]
        public async Task MoveStop(IAxis? axis)
        {
            if (axis == null || axis.Device == null) return;
            try
            {
                if (!axis.IsEnabled)
                {
                    _logger.Warning("{AxisName} is not enabled.", axis.Name);
                    return;
                }
                else
                {
                    _logger.Information("Move Stop {AxisName}", axis.Name);
                    await axis.MoveStop();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Move Stop Error for {AxisName}", axis.Name);
            }
        }

        [RelayCommand]
        public async Task DYMovePosition(DMotionPosition? p)
        {
            if (p == null || DyAxis == null || DyAxis.Device == null) return;

            try
            {
                if (!DyAxis.IsEnabled)
                {
                    _logger.Warning("{AxisName} is not enabled.", DyAxis.Name);
                    return;
                }

                if (DyAxis.IsBusy)
                {
                    _logger.Warning("{AxisName} is busy.", DyAxis.Name);
                    return;
                }

                await DyAxis.Move(MoveType.Absolute, p.Position, p.Speed);

                _logger.Information("Move {AxisName} to {Position} at Speed {Speed}", DyAxis.Name, p.Position, p.Speed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "DY Move Position Error");
            }
        }

        [RelayCommand]
        public async Task Bz1MovePosition(DMotionPosition? p)
        {
            if (p == null || Bz1Axis == null || Bz1Axis.Device == null) return;

            try
            {
                if (!Bz1Axis.IsEnabled)
                {
                    _logger.Warning("{AxisName} is not enabled.", Bz1Axis.Name);
                    return;
                }

                if (Bz1Axis.IsBusy)
                {
                    _logger.Warning("{AxisName} is busy.", Bz1Axis.Name);
                    return;
                }

                await Bz1Axis.Move(MoveType.Absolute, p.Speed, p.Position);

                _logger.Information("Move {AxisName} to {Position} at Speed {Speed}", Bz1Axis.Name, p.Position, p.Speed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "H-Z Move Position Error");
            }
        }

        [RelayCommand]
        public async Task PYMovePosition(DMotionPosition? p)
        {
            if (p == null || PyAxis == null || PyAxis.Device == null) return;
            try
            {
                if (!PyAxis.IsEnabled)
                {
                    _logger.Warning("{AxisName} is not enabled.", PyAxis.Name);
                    return;
                }
                if (PyAxis.IsBusy)
                {
                    _logger.Warning("{AxisName} is busy.", PyAxis.Name);
                    return;
                }
                await PyAxis.Move(MoveType.Absolute, p.Position, p.Speed);
                _logger.Information("Move {AxisName} to {Position} at Speed {Speed}", PyAxis.Name, p.Position, p.Speed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PY Move Position Error");
            }
        }
    }
}
