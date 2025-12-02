using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class DAxis : ObservableObject, IAxis
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private int motorNo;

        [ObservableProperty] private UnitType unit;
        [ObservableProperty] private double limitMinSpeed;
        [ObservableProperty] private double limitMaxSpeed;
        [ObservableProperty] private double limitMinPosition;
        [ObservableProperty] private double limitMaxPosition;
        [ObservableProperty] private double encoderCountPerUnit;
        [ObservableProperty] private double inpositionRange;
        [ObservableProperty] private int hommingProgramNumber;
        [ObservableProperty] private int homeTimeout;

        [ObservableProperty] private IMotionDevice device;

        [ObservableProperty] public ObservableCollection<DMotionParameter> parameterList = new ObservableCollection<DMotionParameter>();
        [ObservableProperty] public ObservableCollection<DMotionPosition> positionList = new ObservableCollection<DMotionPosition>();

        public bool IsEnabled { get; set; }
        public bool IsBusy { get; set; }
        public bool IsError { get; set; }
        public bool InPosition { get; set; }
        public bool IsHome { get; set; }
        public bool IsPlusLimit { get; set; }
        public bool IsMinusLimit { get; set; }
        public bool IsMotionDone { get; set; }
        public bool IsHomeDone { get; set; }
        public double CurrentSpeed { get; set; }
        public double CommandPosition { get; set; }
        public double CurrentPosition { get; set; }

        public async Task Home()
        {
            string cmd = string.Format("ENABLE PLC {0:D}", HommingProgramNumber);
            await Device.SendCommand(cmd);

            Stopwatch timeout = new Stopwatch();

            timeout.Start();

            while (true)
            {
                await Task.Delay(100);

                if (IsHomeDone)
                {
                    return;
                }
                else if (timeout.ElapsedMilliseconds > HomeTimeout)
                {
                    cmd = string.Format("DISABLE PLC {0:D}", HommingProgramNumber);
                    await Device.SendCommand(cmd);
                    await MoveStop();

                    return;
                }
            }
        }


        public Task ServoReady(bool ready)
        {
            if (!ready)
            {
                this.IsHomeDone = false;
            }

            string cmd = string.Empty;

            if (ready)
            {
                cmd = string.Format("#{0}J/", MotorNo);
            }
            else
            {
                cmd = string.Format("#{0}K", MotorNo);
            }

            return Device.SendCommand(cmd);
        }

        public Task Move(MoveType moveType, double jerk, double velocity, double position)
        {
            throw new NotImplementedException();
        }

        public Task JogMove(JogMoveType moveType, double jogSpeed)
        {
            throw new NotImplementedException();
        }

        public Task MoveStop()
        {
            string cmd = string.Format("#{0:D}J/", MotorNo);
            return Device.SendCommand(cmd);
        }

        public Task EStop()
        {
            string cmd = string.Format("#{0:D}J/", MotorNo);
            return Device.SendCommand(cmd);
        }
    }
}
