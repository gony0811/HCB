using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.UI.SERVICE.ViewModels;
using Serilog;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    public partial class DMotionPosition : ObservableObject
    {
        public ILogger _logger;
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private double speed;
        [ObservableProperty] private double position;
        [ObservableProperty] private IAxis parentMotion;



        public MotionPosition ToEntity()
        {
            return new MotionPosition
            {
                Id = this.Id,
                MotionId = ParentMotion.Id,
                Name = this.Name,
                Position = this.Position,
                Speed = this.Speed,
            };
        }


        [RelayCommand]
        public async Task AbsoluteMove()
        {
            
            if (ParentMotion == null || ParentMotion.Device == null) return;

            try
            {
                if (!ParentMotion.IsEnabled)
                {
                    _logger.Warning("{AxisName} is not enabled.", ParentMotion.Name);
                    return;
                }

                if (ParentMotion.IsBusy)
                {
                    _logger.Warning("{AxisName} is busy.", ParentMotion.Name);
                    return;
                }

                await ParentMotion.Move(MoveType.Absolute, velocity: Speed, position: Position);

                _logger.Information("Move {AxisName} to {Position} at Speed {Speed}", ParentMotion.Name, Position, Speed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Move Position Error for {AxisName}", ParentMotion.Name);
            }
            
        }

        [RelayCommand]
        public async Task Stop()
        {
            if (ParentMotion == null || ParentMotion.Device == null) return;
            try
            {
                if (!ParentMotion.IsEnabled)
                {
                    _logger.Warning("{AxisName} is not enabled.", ParentMotion.Name);
                    return;
                }
                else
                {
                    _logger.Information("Move Stop {AxisName}", ParentMotion.Name);
                    await ParentMotion.MoveStop();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Move Stop Error for {AxisName}", ParentMotion.Name);
            }
        }
            
    }
}
