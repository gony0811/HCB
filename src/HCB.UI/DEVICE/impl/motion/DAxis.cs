using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using HCB.UI.SERVICE.ViewModels;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    public partial class DAxis : ObservableObject, IAxis
    {
        private readonly ILogger _logger;

        // ── 인터락 서비스 (외부 주입 또는 내부 생성) ──────────────────
        private readonly IInterlockService _interlock;

        // ── 인터락 경고 마진 (단위는 축 단위와 동일) ──────────────────
        /// <summary>한계값 기준으로 이 값 이내에 들어오면 Warning 이벤트 발생</summary>
        public double InterlockWarningMargin { get; set; } = 1.0;

        // ── ObservableProperty ─────────────────────────────────────────
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

        [ObservableProperty] public ObservableCollection<DMotionParameter> parameterList = new();
        [ObservableProperty] public ObservableCollection<DMotionPosition> positionList = new();

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

        // currentPosition 이 바뀔 때마다 인터락 체크
        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (SetProperty(ref _currentPosition, value))
                    CheckInterlock(value);
            }
        }

        // ── 인터락 상태 (UI 바인딩용) ──────────────────────────────────
        [ObservableProperty] private InterlockState interlockState = InterlockState.Normal;
        [ObservableProperty] private bool isInterlocked = false;

        // ── 생성자 ─────────────────────────────────────────────────────
        public DAxis(ILogger logger, IInterlockService interlockService)
        {
            _logger = logger.ForContext<DAxis>();
            _interlock = interlockService;

            HomeTimeout = 1000 * 60 * 5;

            // InpositionRange 기본값
            InpositionRange = unit == UnitType.um ? 1.0 : 0.001;

            // 인터락 이벤트 구독
            _interlock.InterlockTriggered += OnInterlockTriggered;
            _interlock.InterlockReleased += OnInterlockReleased;
        }

        // ── 인터락 체크 ────────────────────────────────────────────────
        /// <summary>
        /// CurrentPosition 이 변경될 때마다 호출됩니다.
        /// 범위를 벗어나면 인터락 서비스가 원자적으로 락을 설정하고
        /// 즉시 MoveStop 을 호출합니다.
        /// </summary>
        private void CheckInterlock(double position)
        {
            // 한계값이 설정되지 않은 경우 스킵
            if (LimitMinPosition == 0 && LimitMaxPosition == 0) return;

            var state = _interlock.Check(
                Name, MotorNo,
                position,
                LimitMinPosition, LimitMaxPosition,
                InterlockWarningMargin);

            InterlockState = state;
            IsInterlocked = state == InterlockState.Locked;

            if (state == InterlockState.Locked)
            {
                // 비동기 정지 명령 (fire-and-forget, 로그만 남김)
                _ = EmergencyStopAsync();
            }
        }

        private async Task EmergencyStopAsync()
        {
            try
            {
                await EStop();
                _logger.Warning($"[INTERLOCK] {Name} 축 비상 정지 실행 완료");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"[INTERLOCK] {Name} 축 비상 정지 중 오류");
            }
        }

        // ── 인터락 수동 해제 ───────────────────────────────────────────
        [RelayCommand]
        public void ReleaseInterlock()
        {
            // 축이 안전 범위 안에 있는지 먼저 확인
            if (CurrentPosition < LimitMinPosition || CurrentPosition > LimitMaxPosition)
            {
                _logger.Warning($"[INTERLOCK] {Name} 축이 아직 한계 범위 밖에 있습니다. 해제 불가.");
                return;
            }

            _interlock.TryRelease();
        }

        // ── 인터락 이벤트 핸들러 ──────────────────────────────────────
        private void OnInterlockTriggered(object? sender, InterlockEventArgs e)
        {
            // 이 축에서 발생한 이벤트만 처리
            if (e.MotorNo != MotorNo) return;

            _logger.Error($"[INTERLOCK] {e.Message}");

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    $"인터락 발생!\n\n축: {e.AxisName}\n현재 위치: {e.Value:F4}\n한계값: {e.Limit:F4}\n발생 시각: {e.OccurredAt:HH:mm:ss.fff}",
                    "인터락 경고",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        }

        private void OnInterlockReleased(object? sender, EventArgs e)
        {
            _logger.Information($"[INTERLOCK] {Name} 축 인터락 해제됨");
            InterlockState = InterlockState.Normal;
            IsInterlocked = false;
        }

        // ══════════════════════════════════════════════════════════════
        // 기존 명령 메서드 (변경 없음 - 인터락과 독립 동작)
        // ══════════════════════════════════════════════════════════════

        [RelayCommand]
        public async Task Home()
        {
            try
            {
                string cmd = string.Format("ENABLE PLC {0:D}", HommingProgramNumber);
                await Device.SendCommand(cmd);

                var timeout = Stopwatch.StartNew();

                while (true)
                {
                    await Task.Delay(100);

                    if (IsHomeDone) return;

                    if (timeout.ElapsedMilliseconds > HomeTimeout)
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
            if (Device?.IsConnected != true) return false;

            string command = $"#{MotorNo}J/";

            if (IsEnabled) return true;

            try
            {
                await Device.SendCommand(command);
                await Task.Delay(1000);

                if (IsEnabled)
                    _logger.Information($"{Name}: Servo On Success");
                else
                    throw new Exception("Servo On Failed");

                return IsEnabled;
            }
            catch (Exception ex)
            {
                _logger.Error($"{Name}: Servo On Failed - {ex.Message}");
                return false;
            }
        }

        [RelayCommand]
        public async Task<bool> ServoOff()
        {
            if (Device?.IsConnected != true) return false;

            string command = $"#{MotorNo}K/";

            if (!IsEnabled) return false;

            try
            {
                await Device.SendCommand(command);
                await Task.Delay(1000);

                if (!IsEnabled)
                    _logger.Information($"{Name}: Servo Off Success");
                else
                    throw new Exception("Servo Off Fail");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Information($"{Name}: Servo Off Failed - {ex.Message}");
                return false;
            }
        }

        public Task ServoReady(bool ready)
        {
            if (!ready) IsHomeDone = false;

            string cmd = ready
                ? string.Format("#{0}J/", MotorNo)
                : string.Format("#{0}K", MotorNo);

            return (Device?.IsConnected == true && Device?.IsEnabled == true)
                ? Device.SendCommand(cmd)
                : Task.CompletedTask;
        }

        public async Task Move(MoveType moveType, double velocity, double position)
        {
            // 인터락 상태에서는 이동 명령 차단
            if (_interlock.IsLocked)
            {
                _logger.Warning($"[INTERLOCK] {Name} 축 이동 차단됨 (인터락 활성)");
                return;
            }

            _logger.Information($"{Name}, {moveType}, Velocity: {velocity}, Position: {position}");

            var setPos = position * EncoderCountPerUnit;

            if (Device?.IsConnected != true && Device?.IsEnabled != true)
            {
                _logger.Information($"{Name} Axis is not available.");
                return;
            }

            await Device.SendCommand($"Motor[{MotorNo}].JogSpeed={velocity}");

            string moveCmd = moveType == MoveType.Absolute
                ? $"#{MotorNo}J={setPos}"
                : moveType == MoveType.Relative
                    ? $"#{MotorNo}J^{setPos}"
                    : throw new NotImplementedException($"{moveType} Move is not implemented.");

            await Device.SendCommand(moveCmd);
        }

        public async Task Move(MoveType moveType, double jerk, double velocity, double position)
        {
            if (_interlock.IsLocked)
            {
                _logger.Warning($"[INTERLOCK] {Name} 축 이동 차단됨 (인터락 활성)");
                return;
            }

            _logger.Information($"{Name}, {moveType}, Velocity: {velocity}, Position: {position}");

            var setPos = position * EncoderCountPerUnit;

            if (Device?.IsConnected != true && Device?.IsEnabled != true)
            {
                _logger.Information($"{Name} Axis is not available.");
                return;
            }

            await Device.SendCommand($"Motor[{MotorNo}].Jerk={jerk}");
            await Device.SendCommand($"Motor[{MotorNo}].JogSpeed={velocity}");

            string moveCmd = moveType == MoveType.Absolute
                ? $"#{MotorNo}J={setPos}"
                : moveType == MoveType.Relative
                    ? $"#{MotorNo}J^{setPos}"
                    : throw new NotImplementedException($"{moveType} Move is not implemented.");

            await Device.SendCommand(moveCmd);
        }

        public Task JogMove(JogMoveType moveType, double jogSpeed)
        {
            if (_interlock.IsLocked)
            {
                _logger.Warning($"[INTERLOCK] {Name} 축 Jog 이동 차단됨 (인터락 활성)");
                return Task.CompletedTask;
            }

            _logger.Information($"{Name}, {moveType}, JogSpeed: {jogSpeed}");

            try
            {
                if (Device?.IsConnected == true && Device?.IsEnabled == true)
                {
                    if (moveType == JogMoveType.Stop)
                    {
                        return Device.SendCommand($"#{MotorNo:D}J/");
                    }
                    else
                    {
                        string direction = moveType == JogMoveType.Plus ? "+" : "-";
                        string cmd = string.Format("Motor[{0}].JogSpeed={1} #{0}J{2}", MotorNo, jogSpeed, direction);
                        return Device.SendCommand(cmd);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, $"{Name} JogMove 오류");
            }

            return Task.CompletedTask;
        }

        public Task MoveStop()
        {
            string cmd = string.Format("#{0:D}J/", MotorNo);

            return (Device?.IsConnected == true && Device?.IsEnabled == true)
                ? Device.SendCommand(cmd)
                : Task.CompletedTask;
        }

        public Task EStop()
        {
            string cmd = string.Format("#{0:D}J/", MotorNo);

            return (Device?.IsConnected == true && Device?.IsEnabled == true)
                ? Device.SendCommand(cmd)
                : Task.CompletedTask;
        }
    }
}