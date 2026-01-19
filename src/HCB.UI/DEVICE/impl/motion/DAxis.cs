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
using Serilog;

namespace HCB.UI
{
    public partial class DAxis : ObservableObject, IAxis
    {
        private readonly ILogger logger;

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

        public DAxis(ILogger logger)
        {
            this.logger = logger.ForContext<DAxis>();
            HomeTimeout = 10000;
        }

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

            logger.Information("ServoOn/Off Command Sent: {Name}, IsEnabled: {IsEnabled}", Name, IsEnabled);

            if (Device?.IsConnected != true || Device?.IsEnabled != true)
            {
                return;
            }

            // 2. 현재 상태에 따른 명령 생성 및 사전 처리
            string command = IsEnabled
                ? $"#{MotorNo}K"  // Servo On -> Off 시퀀스
                : $"#{MotorNo}J/";   // Servo Off -> On 시퀀스

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
                
                //cmd = string.Format("#{0}J/ #{0}$", MotorNo);

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
             this.logger.Information($"{Name}, {moveType.ToString()}, JogSpeed: {jogSpeed}");
            // Todo: 현재 테스트 용도로 MessageBox 사용중 실사용시 삭제

            try
            {
                if (Device?.IsConnected == true && Device?.IsEnabled == true)
                {
                    if (moveType == JogMoveType.Stop)
                    {
                        string stopCmd = string.Format("#{0:D}J/", MotorNo);
                        return Device.SendCommand(stopCmd);
                    }
                    else
                    {
                        string direction = (moveType == JogMoveType.Plus ? "+" : "-");
                        // Motor[x].JogSpeed={속도} 와 #{x}J/ 를 공백으로 구분하여 한 번에 전송
                        string cmd = string.Format("Motor[{0}].JogSpeed={1} #{0}J{2}", MotorNo, jogSpeed, direction);
                        //string cmd = string.Format("Motor[{0}].JogSpeed={1} #{0}J/", MotorNo, jogSpeed);
                        //string cmd = string.Format("#{0:D}J/", MotorNo);

                        return Device.SendCommand(cmd);
                    }    
                }
            }
            catch(Exception e)
            {
                
            }
            return Task.CompletedTask;
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
