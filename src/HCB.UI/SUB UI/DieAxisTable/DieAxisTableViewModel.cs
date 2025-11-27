using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace HCB.UI
{
    public partial class DieAxisTableViewModel : ObservableObject
    {
        [ObservableProperty]
        private string tableName;

        [ObservableProperty]
        public ObservableCollection<DieAxisRowModel> rows = new ObservableCollection<DieAxisRowModel>();

        public DieAxisTableViewModel(string tableName)
        {
            this.TableName = tableName;
        }

        public void AddRow(DieAxisRowModel row)
        {
            Rows.Add(row);
        }

        [RelayCommand]
        public void Save(DieAxisRowModel row)
        {
            MessageBox.Show($"[{row.Name}] 저장 완료\nPos={row.Position}, Spd={row.Speed}");
        }

        [RelayCommand]
        private void Move(DieAxisRowModel row)
        {
            MessageBox.Show($"[{row.Name}] 위치로 이동 실행!");
        }
    }

    public partial class DieAxisRowModel : ObservableObject
    {
        
        [ObservableProperty] public string name;
        [ObservableProperty] public double position;
        [ObservableProperty] public double speed;

        public DieAxisRowModel(string name, double position, double speed)
        {
            Name = name;
            Position = position;
            Speed = speed;
        }
    }
}
