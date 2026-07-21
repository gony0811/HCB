using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
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

        // Z축 피듀셜 트래킹 파라미터
        [ObservableProperty] private double zPositionA;
        [ObservableProperty] private double zPositionB;
        [ObservableProperty] private int zTrackRepeat = 10;
        [ObservableProperty] private string zTrackStatus = "-";

        // Z축 피듀셜 트래킹 결과
        public ObservableCollection<FiducialZTrackPoint> ZTrackResults { get; } = new();

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

        // ══════════════════════════════════════════════
        //  Z축 피듀셜 트래킹
        //  A/B 두 Z 위치를 오가며 HC1/HC2 Fiducial을 반복 측정하여
        //  A 첫 측정 기준 변화량을 기록한다.
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task SetPositionA()
        {
            var ct = GetToken();
            ZPositionA = await _sequenceService.GetCurrentPosition(MotionExtensions.H_Z, ct);
            ZTrackStatus = $"A 지점 설정: {ZPositionA:F4} mm";
            _logger.Information("Z트래킹 A 지점 설정: {Z:F4}", ZPositionA);
        }

        [RelayCommand]
        public async Task SetPositionB()
        {
            var ct = GetToken();
            ZPositionB = await _sequenceService.GetCurrentPosition(MotionExtensions.H_Z, ct);
            ZTrackStatus = $"B 지점 설정: {ZPositionB:F4} mm";
            _logger.Information("Z트래킹 B 지점 설정: {Z:F4}", ZPositionB);
        }

        [RelayCommand]
        public async Task FiducialZTrack()
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            ZTrackResults.Clear();
            var ct = GetToken();

            try
            {
                if (ZTrackRepeat <= 0)
                    throw new ArgumentException("반복 횟수는 1 이상이어야 합니다.");

                double baseZ = await _sequenceService.GetCurrentPosition(MotionExtensions.H_Z, ct);
                _logger.Information(
                    "Z 피듀셜 트래킹 시작 | A={A:F4}, B={B:F4}, repeat={Repeat}",
                    ZPositionA, ZPositionB, ZTrackRepeat);

                int totalSteps = ZTrackRepeat * 2;
                ZTrackStatus = $"준비 완료 — A/B 반복 {ZTrackRepeat}회 (총 {totalSteps}회 측정)";

                Point2D? refHc1 = null;
                Point2D? refHc2 = null;
                int current = 0;

                for (int rep = 0; rep < ZTrackRepeat; rep++)
                {
                    double[] positions = { ZPositionA, ZPositionB };
                    string[] labels = { "A", "B" };

                    for (int p = 0; p < 2; p++)
                    {
                        ct.ThrowIfCancellationRequested();
                        current++;

                        string label = labels[p];
                        double targetZ = positions[p];
                        ZTrackStatus = $"[{current}/{totalSteps}] {label} 지점(Z={targetZ:F4}) 이동 중...";

                        await _sequenceService.MotionsMove(MotionExtensions.H_Z, targetZ, ct);
                        await Task.Delay(200, ct);

                        ZTrackStatus = $"[{current}/{totalSteps}] {label} 지점 HC1 측정 중...";
                        await _communication.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
                        var hc1Result = await _communication.RequestVisionMarkPosition(
                            MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
                        if (hc1Result?.Result == Result.NG)
                            throw new Exception($"HC1 Fiducial 측정 실패 ({label} 지점)");

                        ZTrackStatus = $"[{current}/{totalSteps}] {label} 지점 HC2 측정 중...";
                        await _communication.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
                        var hc2Result = await _communication.RequestVisionMarkPosition(
                            MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
                        if (hc2Result?.Result == Result.NG)
                            throw new Exception($"HC2 Fiducial 측정 실패 ({label} 지점)");

                        var hc1Pos = Point2D.of(hc1Result.X, hc1Result.Y);
                        var hc2Pos = Point2D.of(hc2Result.X, hc2Result.Y);

                        if (refHc1 == null)
                        {
                            refHc1 = hc1Pos;
                            refHc2 = hc2Pos;
                        }

                        var point = new FiducialZTrackPoint
                        {
                            Repeat = rep + 1,
                            Position = label,
                            ZAbsolute = targetZ,
                            Hc1X = hc1Pos.X,
                            Hc1Y = hc1Pos.Y,
                            Hc2X = hc2Pos.X,
                            Hc2Y = hc2Pos.Y,
                            Hc1DeltaX = hc1Pos.X - refHc1.X,
                            Hc1DeltaY = hc1Pos.Y - refHc1.Y,
                            Hc2DeltaX = hc2Pos.X - refHc2.X,
                            Hc2DeltaY = hc2Pos.Y - refHc2.Y,
                        };
                        ZTrackResults.Add(point);

                        _logger.Information(
                            "Z트래킹 [{Current}/{Total}] {Label}(Z={Z:F4}) | " +
                            "HC1({H1X:F6},{H1Y:F6}) Δ({D1X:F6},{D1Y:F6}) | " +
                            "HC2({H2X:F6},{H2Y:F6}) Δ({D2X:F6},{D2Y:F6})",
                            current, totalSteps, label, targetZ,
                            hc1Pos.X, hc1Pos.Y, point.Hc1DeltaX, point.Hc1DeltaY,
                            hc2Pos.X, hc2Pos.Y, point.Hc2DeltaX, point.Hc2DeltaY);
                    }
                }

                await _sequenceService.MotionsMove(MotionExtensions.H_Z, baseZ, ct);

                await SaveZTrackCsv(ct);
                ZTrackStatus = $"완료 — {ZTrackRepeat}회 반복, {ZTrackResults.Count}개 측정, CSV 저장됨";
                _logger.Information("Z 피듀셜 트래킹 완료 | 총 {Count}개 포인트", ZTrackResults.Count);
            }
            catch (OperationCanceledException)
            {
                ZTrackStatus = "취소됨";
                _logger.Warning("Z 피듀셜 트래킹 취소");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Z 피듀셜 트래킹 실패");
                ZTrackStatus = $"오류: {e.Message}";
            }
            finally
            {
                IsNotBusy = true;
            }
        }

        private async Task SaveZTrackCsv(CancellationToken ct)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "정밀도 데이터");
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, $"FiducialZTrack_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("Repeat,Position,ZAbsolute,HC1_X,HC1_Y,HC2_X,HC2_Y,HC1_DeltaX,HC1_DeltaY,HC2_DeltaX,HC2_DeltaY");
                foreach (var p in ZTrackResults)
                {
                    sb.AppendLine($"{p.Repeat},{p.Position},{p.ZAbsolute:F6}," +
                                  $"{p.Hc1X:F6},{p.Hc1Y:F6},{p.Hc2X:F6},{p.Hc2Y:F6}," +
                                  $"{p.Hc1DeltaX:F6},{p.Hc1DeltaY:F6}," +
                                  $"{p.Hc2DeltaX:F6},{p.Hc2DeltaY:F6}");
                }

                await File.WriteAllTextAsync(path, sb.ToString(), ct);
                _logger.Information("Z트래킹 CSV 저장: {Path}", path);
            }
            catch (Exception e)
            {
                _logger.Warning(e, "Z트래킹 CSV 저장 실패");
            }
        }
    }

    public class FiducialZTrackPoint
    {
        public int Repeat { get; set; }
        public string Position { get; set; } = "";
        public double ZAbsolute { get; set; }

        public double Hc1X { get; set; }
        public double Hc1Y { get; set; }
        public double Hc2X { get; set; }
        public double Hc2Y { get; set; }

        public double Hc1DeltaX { get; set; }
        public double Hc1DeltaY { get; set; }
        public double Hc2DeltaX { get; set; }
        public double Hc2DeltaY { get; set; }

        public double Hc1DeltaXUm => Hc1DeltaX * 1000.0;
        public double Hc1DeltaYUm => Hc1DeltaY * 1000.0;
        public double Hc2DeltaXUm => Hc2DeltaX * 1000.0;
        public double Hc2DeltaYUm => Hc2DeltaY * 1000.0;
    }
}
