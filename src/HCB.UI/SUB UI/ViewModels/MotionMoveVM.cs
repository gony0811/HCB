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
        [ObservableProperty] private double pitch;
        private double _speed;
        public double Speed
        {
            get => _speed;
            set
            {
                // 범위를 강제로 제한 (Clamping)
                double validatedValue = Math.Max(Axis.LimitMinSpeed, Math.Min(Axis.LimitMaxSpeed, value));

                if (_speed != validatedValue)
                {
                    _speed = validatedValue;
                    OnPropertyChanged(nameof(Speed));
                }
                else
                {
                    // 범위를 벗어난 값을 입력했을 때, UI의 글자를 다시 정상 범위 숫자로 
                    // 되돌리기 위해 강제로 알림을 한 번 더 보냅니다.
                    OnPropertyChanged(nameof(Speed));
                }
            }
        }

        [ObservableProperty] public IAxis axis;

        public MotionMoveVM(ILogger logger)
        {
            this._logger = logger.ForContext<MotionMoveVM>();
        }

        [RelayCommand]
        public void SelectPitch(double pitch)
        {
            Pitch = pitch;
        }

        [RelayCommand]
        public async Task RelativeMove()
        {
            if (Axis == null || Pitch == 0) return;
            try
            {
                if (!Axis.IsEnabled)
                {
                    this._logger.Warning("{axis} is not enabled.", Axis.Name);
                    return;
                }

                if (Axis.CurrentPosition + Pitch >= Axis.LimitMinPosition
                    && Axis.CurrentPosition + Pitch <= Axis.LimitMaxPosition)
                {
                    await Axis.Move(MoveType.Relative, Axis.SetSpeed, Pitch);
                }
                else
                {
                    this._logger.Warning("{axis} position {position} is out of range [{min},{max}]", Axis.Name, Axis.SetSpeed, Axis.LimitMinPosition, Axis.LimitMaxPosition);
                }
            }
            catch (Exception e)
            {

            }
        }

        [RelayCommand]
        public async Task AbsoluteMove()
        {
            if (Axis == null) return;
            try
            {
                if (!Axis.IsEnabled)
                {
                    this._logger.Warning("{axis} is not enabled.", Axis.Name);
                    return;
                }

                if (Pitch >= Axis.LimitMinPosition
                    && Pitch <= Axis.LimitMaxPosition)
                {
                    await Axis.Move(MoveType.Absolute, Axis.SetSpeed, Pitch);
                }
                else
                {
                    this._logger.Warning("{axis} position {position} is out of range [{min},{max}]", Axis.Name, Axis.SetSpeed, Axis.LimitMinPosition, Axis.LimitMaxPosition);
                }
            }
            catch (Exception e)
            {

            }
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
                else if (Axis.LimitMinSpeed > Axis.SetSpeed || Axis.LimitMaxSpeed < Axis.SetSpeed)
                {
                    this._logger.Warning("{axis} speed {speed} is out of range [{min},{max}].", Axis.Name, Axis.SetSpeed, Axis.LimitMinSpeed, Axis.LimitMaxSpeed);
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
                else if (Axis.LimitMinSpeed > Axis.SetSpeed || Axis.LimitMaxSpeed < Axis.SetSpeed)
                {
                    this._logger.Warning("{axis} speed {speed} is out of range [{min},{max}].", Axis.Name, Axis.SetSpeed, Axis.LimitMinSpeed, Axis.LimitMaxSpeed);
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
