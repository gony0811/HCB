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
        private PositionTableViewModel dyAxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel dyMotorStatusTable;


        // P-Table
        [ObservableProperty]
        private PositionTableViewModel pyAxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel pyMotorStatusTable;



        // B-Head
        [ObservableProperty]
        private PositionTableViewModel bxAxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel bxMotorStatusTable;


        [ObservableProperty]
        private PositionTableViewModel bz1AxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel bz1MotorStatusTable;

        [ObservableProperty]
        private PositionTableViewModel bz2AxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel bz2MotorStatusTable;

        [ObservableProperty]
        private PositionTableViewModel btAxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel btMotorStatusTable;
        // W-Table
        [ObservableProperty]
        private PositionTableViewModel wyAxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel wyMotorStatusTable;

        [ObservableProperty]
        private PositionTableViewModel wtAxisTable;

        [ObservableProperty]
        private MotorStatusTableViewModel wtMotorStatusTable;


        [ObservableProperty] private bool isDieLoading;
        [ObservableProperty] private bool isWaferLoading;

        [ObservableProperty] private bool isDieStandby;
        [ObservableProperty] private bool isWaferStandby;

        // 생성자에서 IoC로부터 팩토리 함수 주입
        public ManualTabViewModel(
            ILogger logger,
            DeviceManager deviceManager,
            SequenceService sequenceService,
            Func<string, PositionTableViewModel> positionFactory,
            Func<string, MotorStatusTableViewModel> motorStatusFactory)
        {
            _logger = logger.ForContext<ManualTabViewModel>();
            _deviceManager = deviceManager;
            _sequenceService = sequenceService;

            // PositionTableViewModel 및 MotorStatusTableViewModel 인스턴스를 팩토리로 생성
            dyAxisTable = positionFactory("D-Y Axis");
            dyMotorStatusTable = motorStatusFactory("Die DY");

            pyAxisTable = positionFactory("P-Y Axis");
            pyMotorStatusTable = motorStatusFactory("P-Y");

            bxAxisTable = positionFactory("B-X Axis");
            bxMotorStatusTable = motorStatusFactory("B-X");

            bz1AxisTable = positionFactory("B-Z1 Axis");
            bz1MotorStatusTable = motorStatusFactory("B-Z1");

            bz2AxisTable = positionFactory("B-Z2 Axis");
            bz2MotorStatusTable = motorStatusFactory("B-Z2");

            btAxisTable = positionFactory("B-T Axis");
            btMotorStatusTable = motorStatusFactory("B-T");

            wyAxisTable = positionFactory("W-Y Axis");
            wyMotorStatusTable = motorStatusFactory("W-Y");

            wtAxisTable = positionFactory("W-T Axis");
            wtMotorStatusTable = motorStatusFactory("W-T");

            Initialize();
        }

        private void Initialize()
        {
            var DYMotion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.D_Y);
            var PYMotion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.P_Y);
            var BXMotion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_X);
            var BZ1Motion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_Z);
            var BZ2Motion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.h_z);
            var WYMotion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.W_Y);
            var WTMotion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.W_T);
            var BTMotion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName).FindMotionByName(MotionExtensions.H_T);

            foreach (var DY in DYMotion.PositionList)
            {
                DyAxisTable.AddRow(new PositionTableRowModel(DY.Name, DY.Position, DY.Speed));
            }

            foreach (var PY in PYMotion.PositionList)
            {
                PyAxisTable.AddRow(new PositionTableRowModel(PY.Name, PY.Position, PY.Speed));
            }

            foreach (var BX in BXMotion.PositionList)
            {
                BxAxisTable.AddRow(new PositionTableRowModel(BX.Name, BX.Position, BX.Speed));
            }

            foreach (var BT in BTMotion.PositionList)
            {
                BtAxisTable.AddRow(new PositionTableRowModel(BT.Name, BT.Position, BT.Speed));
            }

            foreach (var BZ1 in BZ1Motion.PositionList)
            {
                Bz1AxisTable.AddRow(new PositionTableRowModel(BZ1.Name, BZ1.Position, BZ1.Speed));
            }

            foreach (var BZ2 in BZ2Motion.PositionList)
            {
                Bz2AxisTable.AddRow(new PositionTableRowModel(BZ2.Name, BZ2.Position, BZ2.Speed));
            }

            foreach (var WY in WYMotion.PositionList)
            {
                WyAxisTable.AddRow(new PositionTableRowModel(WY.Name, WY.Position, WY.Speed));
            }

            foreach (var WT in WTMotion.PositionList)
            {
                WtAxisTable.AddRow(new PositionTableRowModel(WT.Name, WT.Position, WT.Speed));
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
