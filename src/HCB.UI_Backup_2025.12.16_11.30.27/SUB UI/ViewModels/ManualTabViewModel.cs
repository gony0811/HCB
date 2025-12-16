using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class ManualTabViewModel : ObservableObject
    {
        // D-Table
        [ObservableProperty]
        private DieAxisTableViewModel dyAxisTable = new DieAxisTableViewModel("D-Y Axis");

        [ObservableProperty]
        private MotorStatusTableViewModel dyMotorStatusTable= new MotorStatusTableViewModel("Die DY");

        
        // P-Table
        [ObservableProperty]
        private DieAxisTableViewModel pyAxisTable = new DieAxisTableViewModel("P-Y Axis");

        [ObservableProperty]
        private MotorStatusTableViewModel pyMotorStatusTable = new MotorStatusTableViewModel("P-Y");


        
        // B-Head
        [ObservableProperty] private DieAxisTableViewModel bxAxisTable = new DieAxisTableViewModel("B-X Axis");
        [ObservableProperty] private MotorStatusTableViewModel bxMotorStatusTable = new MotorStatusTableViewModel("B-X");


        [ObservableProperty] private DieAxisTableViewModel bz1AxisTable = new DieAxisTableViewModel("B-Z1 Axis");
        [ObservableProperty] private MotorStatusTableViewModel bz1MotorStatusTable = new MotorStatusTableViewModel("B-Z1");

        [ObservableProperty] private DieAxisTableViewModel bz2AxisTable = new DieAxisTableViewModel("B-Z2 Axis");

        [ObservableProperty] private MotorStatusTableViewModel bz2MotorStatusTable = new MotorStatusTableViewModel("B-Z2");

        // W-Table
        [ObservableProperty] private DieAxisTableViewModel wyAxisTable = new DieAxisTableViewModel("W-Y Axis");
        [ObservableProperty] private MotorStatusTableViewModel wyMotorStatusTable = new MotorStatusTableViewModel("W-Y");

        [ObservableProperty] private DieAxisTableViewModel wtAxisTable = new DieAxisTableViewModel("W-T Axis");
        [ObservableProperty] private MotorStatusTableViewModel wtMotorStatusTable = new MotorStatusTableViewModel("W-T");



        public ManualTabViewModel()
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
    }
}
