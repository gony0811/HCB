using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using Serilog;
using HCB.IoC;
using HCB.Data.Repository;
using System.Threading.Tasks;
using HCB.Data.Entity;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace HCB.UI
{
    /// 2026.01.07 deprecated
    /// 

    [ViewModel(Lifetime.Transient)]
    public partial class PositionTableViewModel : ObservableObject
    {
        private readonly ILogger logger;
        private readonly MotionRepository motionRepository;
        private readonly MotionPositionRepository motionPositionRepository;

        [ObservableProperty]
        private string tableName;

        [ObservableProperty]
        public ObservableCollection<PositionTableRowModel> rows = new ObservableCollection<PositionTableRowModel>();

        public PositionTableViewModel(ILogger logger, string tableName, MotionRepository motionRepository, MotionPositionRepository motionPositionRepository)
        {
            this.logger = logger.ForContext<PositionTableViewModel>();
            this.TableName = tableName;
            this.motionRepository = motionRepository;
            this.motionPositionRepository = motionPositionRepository;
        }

        public void AddRow(PositionTableRowModel row)
        {
            Rows.Add(row);
        }

        [RelayCommand]
        public async Task Save(PositionTableRowModel row)
        {
            if (row == null)
            {
                MessageBox.Show("저장할 항목이 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var motionName = GetMotionNameFromTableName(TableName);
            if (string.IsNullOrEmpty(motionName))
            {
                MessageBox.Show($"알 수 없는 테이블 이름입니다: {TableName}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Include PositionList to get related positions
                var motion = (await motionRepository.ListAsync(
                    m => m.Name == motionName,
                    include: q => q.Include(m => m.PositionList)
                )).FirstOrDefault();

                if (motion == null)
                {
                    MessageBox.Show($"모션 '{motionName}'을(를) 데이터베이스에서 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var positionEntity = motion.PositionList.FirstOrDefault(p => p.Name == row.Name);

                if (positionEntity != null) // Update existing position
                {
                    positionEntity.Position = row.Position;
                    positionEntity.Speed = row.Speed;
                    await motionPositionRepository.Update(positionEntity);
                    logger.Information("Position '{PositionName}' for motion '{MotionName}' updated.", row.Name, motionName);
                }
                else // Create new position
                {
                    var newPosition = new MotionPosition
                    {
                        Name = row.Name,
                        Position = row.Position,
                        Speed = row.Speed,
                        MotionId = motion.Id
                    };
                    await motionPositionRepository.AddAsync(newPosition);
                    logger.Information("New position '{PositionName}' for motion '{MotionName}' created.", row.Name, motionName);
                }

                MessageBox.Show($"[{row.Name}] 저장 완료\nPos={row.Position}, Spd={row.Speed}", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex, "Failed to save position for {TableName}", TableName);
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetMotionNameFromTableName(string tableName)
        {
            return tableName switch
            {
                "D-Y Axis" => MotionExtensions.D_Y,
                "P-Y Axis" => MotionExtensions.P_Y,
                "B-X Axis" => MotionExtensions.H_X,
                "B-Z1 Axis" => MotionExtensions.H_Z,
                "B-Z2 Axis" => MotionExtensions.h_z,
                "B-T Axis" => MotionExtensions.H_T,
                "W-Y Axis" => MotionExtensions.W_Y,
                "W-T Axis" => MotionExtensions.W_T,
                _ => string.Empty,
            };
        }

        [RelayCommand]
        private void Move(PositionTableRowModel row)
        {
            MessageBox.Show($"[{row.Name}] 위치로 이동 실행!");
        }
    }

    public partial class PositionTableRowModel : ObservableObject
    {
        
        [ObservableProperty] private string name;
        [ObservableProperty] private double position;
        [ObservableProperty] private double speed;

        public PositionTableRowModel(string name, double position, double speed)
        {
            Name = name;
            Position = position;
            Speed = speed;
        }
    }  
}
