using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace HCB.UI
{
    public enum JogMode { Continue, Pitch }

    [ViewModel(Lifetime.Scoped)]
    public partial class JogWindowVM : ObservableObject
    {
        private readonly DeviceManager _deviceManager;
        private readonly DispatcherTimer _positionTimer;

        private IAxis HX { get; set; }
        private IAxis HZ { get; set; }
        private IAxis hz { get; set; }
        private IAxis PY { get; set; }
        private IAxis DY { get; set; }
        private IAxis WY { get; set; }

        // ── 모드 ──────────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsContinueMode))]
        [NotifyPropertyChangedFor(nameof(IsPitchMode))]
        private JogMode currentMode = JogMode.Continue;

        public bool IsContinueMode => CurrentMode == JogMode.Continue;
        public bool IsPitchMode => CurrentMode == JogMode.Pitch;

        [RelayCommand] private void SetContinueMode() => CurrentMode = JogMode.Continue;
        [RelayCommand] private void SetPitchMode() => CurrentMode = JogMode.Pitch;

        // ── 포지션 ────────────────────────────────────────────
        [ObservableProperty] private double hxPosition;
        [ObservableProperty] private double hzPosition;
        [ObservableProperty] private double hzSmallPosition;
        [ObservableProperty] private double pyPosition;
        [ObservableProperty] private double dyPosition;
        [ObservableProperty] private double wyPosition;

        // ── 속도 ──────────────────────────────────────────────
        [ObservableProperty] private double hXSpeed = 10.0;
        [ObservableProperty] private double hZSpeed = 5.0;
        [ObservableProperty] private double hzSpeed = 5.0;
        [ObservableProperty] private double pYSpeed = 5.0;
        [ObservableProperty] private double dYSpeed = 5.0;
        [ObservableProperty] private double wYSpeed = 5.0;

        // ── 피치 ──────────────────────────────────────────────
        [ObservableProperty] private double hXPitch = 1.0;
        [ObservableProperty] private double hZPitch = 1.0;
        [ObservableProperty] private double hzPitch = 1.0;
        [ObservableProperty] private double pYPitch = 1.0;
        [ObservableProperty] private double dYPitch = 1.0;
        [ObservableProperty] private double wYPitch = 1.0;

        public JogWindowVM(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager;
            var motion = _deviceManager.GetDevice<PowerPmacDevice>(MotionExtensions.PowerPmacDeviceName);
            HX = motion.FindMotionByName(MotionExtensions.H_X);
            PY = motion.FindMotionByName(MotionExtensions.P_Y);
            DY = motion.FindMotionByName(MotionExtensions.D_Y);
            WY = motion.FindMotionByName(MotionExtensions.W_Y);
            HZ = motion.FindMotionByName(MotionExtensions.H_Z);
            hz = motion.FindMotionByName(MotionExtensions.h_z);

            _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _positionTimer.Tick += UpdatePositions;
            _positionTimer.Start();
        }

        // ── 포지션 갱신 ───────────────────────────────────────
        private void UpdatePositions(object? sender, EventArgs e)
        {
            try
            {
                HxPosition = HX.CurrentPosition;
                HzPosition = HZ.CurrentPosition;
                HzSmallPosition = hz.CurrentPosition;
                PyPosition = PY.CurrentPosition;
                DyPosition = DY.CurrentPosition;
                WyPosition = WY.CurrentPosition;
            }
            catch { /* 통신 일시 끊김 무시 */ }
        }

        // ── 공통 헬퍼 ─────────────────────────────────────────
        private async Task JogAxis(IAxis axis, JogMoveType jogMove, double speed)
            => await axis.JogMove(jogMove, speed);

        private async Task PitchAxis(IAxis axis, double speed, double pitch)
            => await axis.Move(MoveType.Relative, speed, pitch);

        // ═════════════════════════════════════════════════════
        //  Continue Jog — 코드비하인드에서 직접 호출
        // ═════════════════════════════════════════════════════
        public async Task JogStart(string axis, string dir)
        {
            var jogType = dir is "Up" or "Front" ? JogMoveType.Plus : JogMoveType.Minus;
            switch (axis)
            {
                case "HX": await JogAxis(HX, jogType, HXSpeed); break;
                case "DY": await JogAxis(DY, jogType, DYSpeed); break;
                case "PY": await JogAxis(PY, jogType, PYSpeed); break;
                case "WY": await JogAxis(WY, jogType, WYSpeed); break;
                case "HZ": await JogAxis(HZ, jogType, HZSpeed); break;
                case "hz": await JogAxis(hz, jogType, HzSpeed); break;
            }
        }

        public async Task JogStop(string axis)
        {
            switch (axis)
            {
                case "HX": await JogAxis(HX, JogMoveType.Stop, HXSpeed); break;
                case "DY": await JogAxis(DY, JogMoveType.Stop, DYSpeed); break;
                case "PY": await JogAxis(PY, JogMoveType.Stop, PYSpeed); break;
                case "WY": await JogAxis(WY, JogMoveType.Stop, WYSpeed); break;
                case "HZ": await JogAxis(HZ, JogMoveType.Stop, HZSpeed); break;
                case "hz": await JogAxis(hz, JogMoveType.Stop, HzSpeed); break;
            }
        }

        // ═════════════════════════════════════════════════════
        //  Pitch 이동 — CommandParameter: "DY+", "DY-" 등
        // ═════════════════════════════════════════════════════
        [RelayCommand]
        public async Task PitchMove(string param)
        {
            bool isMinus = param.EndsWith("-");
            string axis = param.TrimEnd('+', '-');

            switch (axis)
            {
                case "HX": await PitchAxis(HX, HXSpeed, isMinus ? -HXPitch : HXPitch); break;
                case "DY": await PitchAxis(DY, DYSpeed, isMinus ? -DYPitch : DYPitch); break;
                case "PY": await PitchAxis(PY, PYSpeed, isMinus ? -PYPitch : PYPitch); break;
                case "WY": await PitchAxis(WY, WYSpeed, isMinus ? -WYPitch : WYPitch); break;
                case "HZ": await PitchAxis(HZ, HZSpeed, isMinus ? -HZPitch : HZPitch); break;
                case "hz": await PitchAxis(hz, HzSpeed, isMinus ? -HzPitch : HzPitch); break;
            }
        }

        // ── 타이머 정리 ───────────────────────────────────────
        public void Dispose()
        {
            _positionTimer.Stop();
        }
    }
}
