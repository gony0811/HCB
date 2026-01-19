using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace HCB.UI
{
    public partial class MotionMoveVM : ObservableObject
    {
        private readonly ILogger _logger;

        [ObservableProperty] public int speed;
        [ObservableProperty] public IAxis axis;

        public MotionMoveVM(ILogger logger)
        {      
            this._logger = logger.ForContext<MotionMoveVM>();
        }

        [RelayCommand]
        public async Task PitchMove()
        {
            MessageBox.Show(Axis.Name);
        }

        [RelayCommand]
        public async Task JogPlus()
        {

            if (Axis == null) return;
            try
            {
                if (!Axis.IsEnabled)
                {
                    this._logger.Warning("{axis} is not enabled.", Axis.Name);
                    return;
                }
                else if (!Axis.IsMinusLimit && Axis.IsBusy)
                {
                    this._logger.Warning("{axis} is busy.", Axis.Name);
                    return;
                }
                else if (Axis.LimitMinSpeed > Speed || Axis.LimitMaxSpeed < Speed)
                {
                    this._logger.Warning("{axis} speed {speed} is out of range [{min},{max}].", Axis.Name, Speed, Axis.LimitMinSpeed, Axis.LimitMaxSpeed);
                    return;
                }

                await Axis.JogMove(JogMoveType.Plus, Speed);
            }
            catch (Exception e)
            {

            }
        }

        [RelayCommand]
        public async Task JogMinus()
        {
            if (Axis == null) return;
            try
            {
                if (!Axis.IsEnabled)
                {
                    this._logger.Warning("{axis} is not enabled.", Axis.Name);
                    return;
                }
                else if (!Axis.IsPlusLimit && Axis.IsBusy)
                {
                    this._logger.Warning("{axis} is busy.", Axis.Name);
                    return;
                }
                else if (Axis.LimitMinSpeed > Speed || Axis.LimitMaxSpeed < Speed)
                {
                    this._logger.Warning("{axis} speed {speed} is out of range [{min},{max}].", Axis.Name, Speed, Axis.LimitMinSpeed, Axis.LimitMaxSpeed);
                    return;
                }

                await Axis.JogMove(JogMoveType.Minus, Speed);
            }
            catch (Exception e)
            {

            }
        }

        [RelayCommand]
        public async Task JogStop()
        { 
            if (Axis == null) return;
            try
            {
                 await Axis.JogMove(JogMoveType.Stop, 0);
            }
            catch (Exception e)
            {

            }
        }


    }
}
