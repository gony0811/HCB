using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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

        [ObservableProperty] private bool isEnabled;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool isError;
        [ObservableProperty] private bool inPosition;
        [ObservableProperty] private bool isHome;
        [ObservableProperty] private bool isPlusLimit;
        [ObservableProperty] private bool isMinusLimit;
        [ObservableProperty] private bool isMotionDone;
        [ObservableProperty] private bool isHomeDone;
        [ObservableProperty] private double currentSpeed;
        [ObservableProperty] private double commandPosition;
        [ObservableProperty] private double currentPosition;


        [RelayCommand]
        public async Task Home()
        {
            try
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
            catch(Exception e)
            {
                MessageBox.Show($"에러: \n {e.Message}");
            }
            
        }


        [RelayCommand]
        public async Task ServoOn()
        {
            if (Device?.IsConnected != true || Device?.IsEnabled != true)
            {
                return;
            }

            // 2. 현재 상태에 따른 명령 생성 및 사전 처리
            string command = IsEnabled
                ? $"#{MotorNo}J/"  // Servo On -> Off 시퀀스
                : $"#{MotorNo}K";   // Servo Off -> On 시퀀스

            if (IsEnabled)
            {
                IsHomeDone = false; 
            }

            try
            {
                await Device.SendCommand(command);
            }
            catch (Exception ex)
            {
                // 로그 기록 및 사용자 알림 (예시)
                // logger.LogError(ex, "Servo Command 전송 실패");
                // dialogService.ShowMessage("통신 에러", ex.Message);
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

            if (Device?.IsConnected == true && Device?.IsEnabled == true)
            {
                return Device.SendCommand(cmd);              
            }
            else
            {
                return Task.CompletedTask;
            }     
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

            if (Device?.IsConnected == true && Device?.IsEnabled == true)
            {
                return Device.SendCommand(cmd);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        public Task EStop()
        {
            string cmd = string.Format("#{0:D}J/", MotorNo);

            if (Device?.IsConnected == true && Device?.IsEnabled == true)
            {
                return Device.SendCommand(cmd);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

    }
}
