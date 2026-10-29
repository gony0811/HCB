using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        [ObservableProperty] private double jitter;
        [ObservableProperty] private double jitterTolerance =50;
        [ObservableProperty] private bool isJitterWarning;

        public DAxis(ILogger logger)
        {
            this.logger = logger.ForContext<DAxis>();
            HomeTimeout = 1000 * 60 * 5;

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

        partial void OnNameChanged(string value)
        {
            // 단위 nm
            JitterTolerance = value switch
            {
                MotionExtensions.h_z or MotionExtensions.D_Y => 100,
                _ => 50,
            };
        }


        [RelayCommand]
        public async Task Home()
        {
            try
            {
                // H_T, W_T는 홈 전에 위상(Encpha)이 잡혀 있어야 한다.
                if (Name == MotionExtensions.H_T || Name == MotionExtensions.W_T)
                    await EnsureTPhasedAsync(Device, logger);

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

        // H_T·W_T가 동시에 홈을 잡아도 PLC 9가 한 번만 실행되도록 직렬화한다.
        private static readonly SemaphoreSlim _tPhaseLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// W_T, H_T 위상(Encpha) 확인 — 이미 위상이 잡혀 있으면(≠0) 재실행하지 않는다.
        /// Encpha=0 (위상 미완료) → enable plc 9 실행 후 위상 완료까지 대기 (최대 300초)
        /// </summary>
        public static async Task EnsureTPhasedAsync(IMotionDevice device, ILogger logger, CancellationToken ct = default)
        {
            await _tPhaseLock.WaitAsync(ct);
            try
            {
                string phased = await device.SendCommand<string>("Encpha");
                if (phased != "0") return;

                logger.Information("[TInit] 위상 시작");
                await device.SendCommand("enable plc 9");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(90));
                var pollInterval = TimeSpan.FromMilliseconds(100);

                while (true)
                {
                    if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                        throw new TimeoutException("[TInit] H_T/W_T 위상(Encpha) 완료 시간 초과");
                    ct.ThrowIfCancellationRequested();

                    string result = await device.SendCommand<string>("Encpha");
                    string plcStatus = await device.SendCommand<string>("plc[9].active");
                    if (result == "1" && plcStatus == "0")
                        break;

                    try { await Task.Delay(pollInterval, timeoutCts.Token); }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
                }

                logger.Information("[TInit] 위상 종료");
            }
            finally
            {
                _tPhaseLock.Release();
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
                await Task.Delay(100);

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

        public async Task EStop()
        {
            // 로컬 변수로 캡처하여 경쟁 조건 방지
            var device = Device;

            if (device?.IsConnected == true && device?.IsEnabled == true)
            {
                string cmd = $"#{MotorNo:D}J/";
                await device.SendCommand(cmd);
            }
        }

    }
}
