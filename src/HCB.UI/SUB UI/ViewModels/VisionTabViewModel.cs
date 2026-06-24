using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    [ViewModel(Lifetime.Scoped)]
    public partial class VisionTabViewModel : ObservableObject
    {
        private readonly EqpCommunicationService _communication;
        private readonly SequenceService _sequenceService;
        private readonly ILogger _logger;

        private IAxis? _hxAxis;
        private IAxis? _wyAxis;
        private IAxis? _pyAxis;

        private CancellationTokenSource? _cts;

        // 선택 항목
        [ObservableProperty] private CameraType selectedCamera = CameraType.HC1_HIGH;
        [ObservableProperty] private MarkType selectedMark = MarkType.ALIGN_MARK;
        [ObservableProperty] private DirectType selectedDirect = DirectType.LEFT;

        // UI 상태
        [ObservableProperty] private bool isNotBusy = true;
        [ObservableProperty] private string statusText = "-";

        // 측정 결과 (mm)
        [ObservableProperty] private double measuredX;
        [ObservableProperty] private double measuredY;
        [ObservableProperty] private double measuredDist;
        [ObservableProperty] private bool hasMeasurement;

        // 측정 결과 (um 표시용)
        [ObservableProperty] private double measuredXUm;
        [ObservableProperty] private double measuredYUm;
        [ObservableProperty] private double measuredDistUm;

        // 이동량 (측정값 기반, 사용자가 확인 후 이동)
        [ObservableProperty] private double moveX;
        [ObservableProperty] private double moveY;

        // 재측정 잔차 (mm)
        [ObservableProperty] private double errorX;
        [ObservableProperty] private double errorY;
        [ObservableProperty] private double errorDist;
        [ObservableProperty] private bool hasVerification;

        // 재측정 잔차 (um 표시용)
        [ObservableProperty] private double errorXUm;
        [ObservableProperty] private double errorYUm;
        [ObservableProperty] private double errorDistUm;

        // 결과 요약
        [ObservableProperty] private string resultSummary = "-";

        public VisionTabViewModel(
            DeviceManager deviceManager,
            EqpCommunicationService communication,
            SequenceService sequenceService,
            ILogger logger)
        {
            _communication = communication;
            _sequenceService = sequenceService;
            _logger = logger.ForContext<VisionTabViewModel>();
            var device = deviceManager.GetDevice<PowerPmacDevice>("PMAC");
            _hxAxis = device.FindMotionByName(MotionExtensions.H_X);
            _wyAxis = device.FindMotionByName(MotionExtensions.W_Y);
            _pyAxis = device.FindMotionByName(MotionExtensions.P_Y);

            _sequenceService.InterlockActivated += OnInterlockActivated;
        }

        private void OnInterlockActivated()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        private CancellationToken GetToken()
        {
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        private string YAxisName => SelectedCamera is CameraType.PC_HIGH or CameraType.PC_LOW
            ? MotionExtensions.P_Y : MotionExtensions.W_Y;

        private bool IsPc => SelectedCamera is CameraType.PC_HIGH or CameraType.PC_LOW;

        [RelayCommand]
        public void Stop()
        {
            _cts?.Cancel();
            StatusText = "중지 요청됨...";
        }

        // ══════════════════════════════════════════════
        //  1단계: 측정만 수행
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task Measure()
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            HasMeasurement = false;
            HasVerification = false;
            var ct = GetToken();
            try
            {
                StatusText = "비전 측정 중...";

                await _communication.RequestAFStart(SelectedCamera, SelectedMark, ct);
                var result = await _communication.RequestVisionMarkPosition(
                    SelectedMark, SelectedCamera, SelectedDirect.ToString());

                if (result == null) throw new Exception("비전 응답 null");
                if (result.Result == Result.NG) throw new Exception("비전 측정 실패");

                MeasuredX = result.X;
                MeasuredY = result.Y;
                MeasuredDist = Math.Sqrt(result.X * result.X + result.Y * result.Y);
                MeasuredXUm = MeasuredX * 1000.0;
                MeasuredYUm = MeasuredY * 1000.0;
                MeasuredDistUm = MeasuredDist * 1000.0;

                if (IsPc)
                {
                    MoveX = -result.X;
                    MoveY = +result.Y;
                }
                else
                {
                    MoveX = -result.X;
                    MoveY = -result.Y;
                }

                HasMeasurement = true;
                StatusText = $"측정 완료 — X:{MeasuredX * 1000:F1}μm, Y:{MeasuredY * 1000:F1}μm";
                ResultSummary = $"측정({MeasuredX * 1000:F1}, {MeasuredY * 1000:F1})μm  dist={MeasuredDist * 1000:F1}μm";

                _logger.Information("비전 측정 | Camera={Camera}, X={X:F6}, Y={Y:F6}, Dist={D:F6}mm",
                    SelectedCamera, MeasuredX, MeasuredY, MeasuredDist);
            }
            catch (OperationCanceledException) { StatusText = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Measure failed");
                StatusText = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  2단계: 측정값 기반 이동
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task MoveToMark()
        {
            if (!IsNotBusy || !HasMeasurement) return;
            IsNotBusy = false;
            var ct = GetToken();
            try
            {
                StatusText = $"이동 중... ΔX:{MoveX * 1000:F1}μm, ΔY:{MoveY * 1000:F1}μm";

                await Task.WhenAll(
                    _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, MoveX, ct),
                    _sequenceService.RelativeMotionsMove(YAxisName, MoveY, ct));

                StatusText = "이동 완료 — 재측정으로 잔차를 확인하세요";
            }
            catch (OperationCanceledException) { StatusText = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "MoveToMark failed");
                StatusText = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  3단계: 재측정 (잔차 확인) + CSV 저장
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task Verify()
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            HasVerification = false;
            var ct = GetToken();
            try
            {
                StatusText = "재측정 중...";

                await _communication.RequestAFStart(SelectedCamera, SelectedMark, ct);
                var result = await _communication.RequestVisionMarkPosition(
                    SelectedMark, SelectedCamera, SelectedDirect.ToString());

                if (result == null) throw new Exception("재측정 응답 null");
                if (result.Result == Result.NG) throw new Exception("재측정 실패");

                ErrorX = result.X;
                ErrorY = result.Y;
                ErrorDist = Math.Sqrt(result.X * result.X + result.Y * result.Y);
                ErrorXUm = ErrorX * 1000.0;
                ErrorYUm = ErrorY * 1000.0;
                ErrorDistUm = ErrorDist * 1000.0;
                HasVerification = true;

                ResultSummary = $"측정({MeasuredX * 1000:F1}, {MeasuredY * 1000:F1})μm → " +
                                $"잔차({ErrorX * 1000:F1}, {ErrorY * 1000:F1})μm  dist={ErrorDist * 1000:F1}μm";

                StatusText = $"검증 완료 — 잔차 dist={ErrorDist * 1000:F1}μm";

                await SaveCsv(ct);

                _logger.Information("검증 | 측정({MX:F4},{MY:F4}) → 잔차({EX:F4},{EY:F4}) dist={D:F4}mm",
                    MeasuredX, MeasuredY, ErrorX, ErrorY, ErrorDist);
            }
            catch (OperationCanceledException) { StatusText = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Verify failed");
                StatusText = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        private async Task SaveCsv(CancellationToken ct)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "정밀도 데이터");
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, $"VerifyAccuracy_{SelectedCamera}.csv");
                bool exists = File.Exists(path);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{SelectedCamera}," +
                           $"{MeasuredX:F6},{MeasuredY:F6},{ErrorX:F6},{ErrorY:F6},{ErrorDist:F6}";

                if (!exists)
                    await File.WriteAllTextAsync(path,
                        "Timestamp,Camera,MeasuredX,MeasuredY,ErrorX,ErrorY,ErrorDist\n" + line + "\n", ct);
                else
                    await File.AppendAllTextAsync(path, line + "\n", ct);
            }
            catch (Exception e)
            {
                _logger.Warning(e, "정밀도 CSV 저장 실패");
            }
        }
    }
}
