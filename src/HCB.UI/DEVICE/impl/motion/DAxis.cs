using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.UI.SERVICE.ViewModels;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Documents.Fixed.Model.Data;

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
        [ObservableProperty] private double setSpeed = 0;
        [ObservableProperty] private double commandPosition;
        [ObservableProperty] private double currentPosition;


        public DAxis(ILogger logger)
        {
            this.logger = logger.ForContext<DAxis>();
            HomeTimeout = 10000;

            /// InpositionRange 기본값 설정
            if (unit == UnitType.mm)
            {
                InpositionRange = 0.001;
            }
            else if (unit == UnitType.um)
            {
                InpositionRange = 1;
            }

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
            catch (Exception e)
            {
                MessageBox.Show($"에러: \n {e.Message}");
            }

        }


        [RelayCommand]
        public async Task<bool> ServoOn()
        {

            if (Device?.IsConnected != true)
            {
                return false;
            }
            string command = $"#{MotorNo}J/";

            if (IsEnabled) return true;

            try
            {
                await Device.SendCommand(command);
                await Task.Delay(1000);

                if (IsEnabled)
                {
                    logger.Information($"{Name}: Servo On Success");
                }
                else
                {
                    throw new Exception("Servo On Failed");
                }
                return IsEnabled;
            }
            catch (Exception ex)
            {
                logger.Error($"{Name}: Servo On Failed");
                return false;
            }
            //// 2. 현재 상태에 따른 명령 생성 및 사전 처리
            //string command = IsEnabled
            //    ? $"#{MotorNo}K"  // Servo On -> Off 시퀀스
            //    : $"#{MotorNo}J/";   // Servo Off -> On 시퀀스

            //if (IsEnabled)
            //{
            //    IsHomeDone = false; 
            //}

            //try
            //{
            //    await Device.SendCommand(command);
            //    await Task.Delay(1000);
            //    logger.Information($"{Name}: {(IsEnabled ? "Servo ON" : "Servo Off")}");
            //    return IsEnabled;
            //}
            //catch (Exception ex)
            //{
            //    // 로그 기록 및 사용자 알림 (예시)
            //    logger.Error(ex, "Servo Command 전송 실패");
            //    // dialogService.ShowMessage("통신 에러", ex.Message);
            //}

            //return IsEnabled;
        }

        [RelayCommand]
        public async Task<bool> ServoOff()
        {

            if (Device?.IsConnected != true)
            {
                return false;
            }
            string command = $"#{MotorNo}K/";

            if (!IsEnabled) return false;

            try
            {
                await Device.SendCommand(command);
                await Task.Delay(1000);
                if (!IsEnabled)
                {
                    logger.Information($"{Name}: Servo Off Success");
                }
                else
                {
                    throw new Exception("Servo Off Fail");
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Information($"{Name}: Servo Off Failed");
                return false;
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

        public async Task Move(MoveType moveType, double velocity, double position)
        {
            this.logger.Information($"{Name}, {moveType.ToString()}, Velocity: {velocity}, Position: {position}");

            var setPos = position * EncoderCountPerUnit;

            if (Device?.IsConnected != true && Device?.IsEnabled != true)
            {
                this.logger.Information($"{Name} Axis is not available.");
                return;
            }

            // 속도 설정 (공통)
            string speedCmd = $"Motor[{MotorNo}].JogSpeed={velocity}";
            await Device.SendCommand(speedCmd);

            string moveCmd;

            if (moveType == MoveType.Absolute)
            {
                // 절대 위치 이동 명령 (J=절대위치)
                moveCmd = $"#{MotorNo}J={setPos}";
            }
            else if (moveType == MoveType.Relative)
            {
                // 상대 위치 이동 명령 (J^이동거리)
                // PMAC에서 ^ 기호는 현재 위치 기준 증분(Incremental) 이동을 의미합니다.
                moveCmd = $"#{MotorNo}J^{setPos}";
            }
            else
            {
                throw new NotImplementedException($"{moveType} Move is not implemented.");
            }

            await Device.SendCommand(moveCmd);
        }

        public async Task Move(MoveType moveType, double jerk, double velocity, double position)
        {
            this.logger.Information($"{Name}, {moveType.ToString()}, Velocity: {velocity}, Position: {position}");

            var setPos = position * EncoderCountPerUnit;

            if (Device?.IsConnected != true && Device?.IsEnabled != true)
            {
                this.logger.Information($"{Name} Axis is not available.");
                return;
            }

            string jerkCmd = $"Motor[{MotorNo}].Jerk={jerk}";
            await Device.SendCommand(jerkCmd);

            // 속도 설정 (공통)
            string speedCmd = $"Motor[{MotorNo}].JogSpeed={velocity}";
            await Device.SendCommand(speedCmd);

            string moveCmd;

            if (moveType == MoveType.Absolute)
            {
                // 절대 위치 이동 명령 (J=절대위치)
                moveCmd = $"#{MotorNo}J={setPos}";
            }
            else if (moveType == MoveType.Relative)
            {
                // 상대 위치 이동 명령 (J^이동거리)
                // PMAC에서 ^ 기호는 현재 위치 기준 증분(Incremental) 이동을 의미합니다.
                moveCmd = $"#{MotorNo}J^{setPos}";
            }
            else
            {
                throw new NotImplementedException($"{moveType} Move is not implemented.");
            }

            await Device.SendCommand(moveCmd);
        }

        public Task JogMove(JogMoveType moveType, double jogSpeed)
        {
            this.logger.Information($"{Name}, {moveType.ToString()}, JogSpeed: {jogSpeed}");

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
            catch (Exception e)
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
