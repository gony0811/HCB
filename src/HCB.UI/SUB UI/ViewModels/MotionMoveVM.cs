using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    public partial class MotionMoveVM : ObservableObject
    {
        [ObservableProperty] public int speed;
        [ObservableProperty] public IAxis axis;

        public MotionMoveVM()
        {      
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
                await Axis.JogMove(JogMoveType.Plus, Speed);
            }catch(Exception e)
            {

            }
        }

        [RelayCommand]
        public async Task JogMinus()
        {
            if (Axis == null) return;
            try
            {
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
