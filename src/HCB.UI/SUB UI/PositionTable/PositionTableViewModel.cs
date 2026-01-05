using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using Serilog;

namespace HCB.UI
{
    public partial class PositionTableViewModel : ObservableObject
    {
        private readonly ILogger logger;

        [ObservableProperty]
        private string tableName;

        [ObservableProperty]
        public ObservableCollection<PositionTableRowModel> rows = new ObservableCollection<PositionTableRowModel>();

        public PositionTableViewModel(ILogger logger, string tableName)
        {
            this.logger = logger;
            this.TableName = tableName;
        }

        public void AddRow(PositionTableRowModel row)
        {
            Rows.Add(row);
        }

        [RelayCommand]
        public void Save(PositionTableRowModel row)
        {
            
            MessageBox.Show($"[{row.Name}] 저장 완료\nPos={row.Position}, Spd={row.Speed}");
        }

        [RelayCommand]
        private void Move(PositionTableRowModel row)
        {
            MessageBox.Show($"[{row.Name}] 위치로 이동 실행!");
        }
    }

    public partial class PositionTableRowModel : ObservableObject
    {
        
        [ObservableProperty] public string name;
        [ObservableProperty] public double position;
        [ObservableProperty] public double speed;

        public PositionTableRowModel(string name, double position, double speed)
        {
            Name = name;
            Position = position;
            Speed = speed;
        }
    }  
}
