    using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity.Type;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly RecipeService _recipeService;
        private readonly ECParamService _paramService;
        private readonly ILogger _logger;

        private IAxis? _hxAxis;
        private IAxis? _wyAxis;
        private IAxis? _pyAxis;
        private IAxis? _hzAxis;

        // 모션 정보 표시용 (XAML에서 CurrentPosition을 실시간 바인딩)
        public IAxis? HxAxis => _hxAxis;
        public IAxis? WyAxis => _wyAxis;
        public IAxis? PyAxis => _pyAxis;
        public IAxis? HzAxis => _hzAxis;
        // 카메라에 따른 Y축: PC → P_Y, HC → W_Y
        public IAxis? MotionYAxis => IsPc ? _pyAxis : _wyAxis;

        private CancellationTokenSource? _cts;

        // 선택 항목
        [ObservableProperty] private CameraType selectedCamera = CameraType.HC1_HIGH;
        [ObservableProperty] private MarkType selectedMark = MarkType.ALIGN_MARK;
        [ObservableProperty] private DirectType selectedDirect = DirectType.LEFT;

        // 측정 시점 모션 스냅샷
        [ObservableProperty] private double motionHx;
        [ObservableProperty] private double motionY;
        [ObservableProperty] private string motionYName = "W_Y";
        [ObservableProperty] private bool hasMotionSnapshot;

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

        // ── 전체 시퀀스 파라미터 (사용자 설정) ──
        [ObservableProperty] private int seqTopDie = 1;            // Pickup 할 Top Die 번호
        [ObservableProperty] private double seqHtRotation;        // 3) H_T 회전 값 (mm/deg, 축 단위)
        [ObservableProperty] private double seqPcLeftHx;          // 4) PC Left 측정 H_X
        [ObservableProperty] private double seqPcLeftPy;          // 4) PC Left 측정 P_Y
        [ObservableProperty] private double seqPcLeftHz;          // 4) PC Left 측정 H_Z (초점)
        [ObservableProperty] private double seqPcRightHx;         // 4) PC Right 측정 H_X
        [ObservableProperty] private double seqPcRightPy;         // 4) PC Right 측정 P_Y
        [ObservableProperty] private double seqPcRightHz;         // 4) PC Right 측정 H_Z (초점)
        [ObservableProperty] private double seqHc1Hz;            // 7) HC1 측정 base H_Z (초점)
        [ObservableProperty] private double seqHc1DeltaHx;        // 7) HC1 Left→Right H_X 이동량
        [ObservableProperty] private double seqHc1DeltaWy;        // 7) HC1 Left→Right W_Y 이동량
        [ObservableProperty] private bool seqApplyHcAlignZGap = true;  // 7) HC1 Align: FID_ALIGN_GAP 초점 이동 적용
        [ObservableProperty] private double offsetX;             // 5) W-Table PLACE_CENTER Offset X
        [ObservableProperty] private double offsetY;             // 5) W-Table PLACE_CENTER Offset Y
        [ObservableProperty] private string seqStatus = "-";

        // 전체 시퀀스 측정 결과
        public ObservableCollection<SeqMeasurePoint> SeqResults { get; } = new();

        public VisionTabViewModel(
            DeviceManager deviceManager,
            EqpCommunicationService communication,
            SequenceService sequenceService,
            RecipeService recipeService,
            ECParamService eCParamService,
            ILogger logger)
        {
            _communication = communication;
            _sequenceService = sequenceService;
            _recipeService = recipeService;
            _paramService = eCParamService;
            _logger = logger.ForContext<VisionTabViewModel>();
            var device = deviceManager.GetDevice<PowerPmacDevice>("PMAC");
            _hxAxis = device.FindMotionByName(MotionExtensions.H_X);
            _wyAxis = device.FindMotionByName(MotionExtensions.W_Y);
            _pyAxis = device.FindMotionByName(MotionExtensions.P_Y);
            _hzAxis = device.FindMotionByName(MotionExtensions.H_Z);

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

        // PC 카메라: H_X / P_Y,  HC 카메라: H_X / W_Y
        public bool IsPc => SelectedCamera is CameraType.PC_HIGH or CameraType.PC_LOW;

        // HC1/HC2 Align Mark 측정 시 H_Z, h_z 축을 FID_ALIGN_GAP 만큼 이동해야 초점이 맞는다.
        private bool NeedAlignZMove => !IsPc && SelectedMark == MarkType.ALIGN_MARK;

        partial void OnSelectedCameraChanged(CameraType value)
        {
            OnPropertyChanged(nameof(IsPc));
            OnPropertyChanged(nameof(MotionYAxis));
            MotionYName = IsPc ? MotionExtensions.P_Y : MotionExtensions.W_Y;
        }

        [RelayCommand]
        public void Stop()
        {
            _cts?.Cancel();
            StatusText = "중지 요청됨...";
        }

        // ══════════════════════════════════════════════
        //  공용 측정 루틴
        //  · HC1/HC2 Align Mark: H_Z/h_z를 FID_ALIGN_GAP만큼 이동 후 촬상, 측정 후 복귀
        //    (StepSeqTabViewModel.BtmHighAlign → DieSequence.BtmHighAlign 참고)
        //  · 측정 시점의 H_X / (P_Y|W_Y) 모션 위치를 스냅샷으로 기록
        // ══════════════════════════════════════════════
        private async Task<VisionMarkPositionResponse?> MeasureMark(CancellationToken ct)
        {
            bool zMove = NeedAlignZMove;
            double gap = 0;

            if (zMove)
            {
                gap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
                StatusText = $"Align 초점 이동 중... (H_Z/h_z ±{gap:F4}mm)";
                await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, -gap, ct);
                await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, gap, ct);
            }

            try
            {
                await _communication.RequestAFStart(SelectedCamera, SelectedMark, ct);
                var result = await _communication.RequestVisionMarkPosition(
                    SelectedMark, SelectedCamera, SelectedDirect.ToString());

                CaptureMotionSnapshot();
                return result;
            }
            finally
            {
                if (zMove)
                {
                    // 측정 성공/실패와 무관하게 Fid 초점 높이로 복귀
                    try
                    {
                        await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, -gap, ct);
                        await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, gap, ct);
                    }
                    catch (Exception e) { _logger.Warning(e, "Align Z 복귀 실패"); }
                }
            }
        }

        private void CaptureMotionSnapshot()
        {
            MotionHx = _hxAxis?.CurrentPosition ?? 0;
            MotionY = (IsPc ? _pyAxis : _wyAxis)?.CurrentPosition ?? 0;
            MotionYName = IsPc ? MotionExtensions.P_Y : MotionExtensions.W_Y;
            HasMotionSnapshot = true;
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

                var result = await MeasureMark(ct);

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

                var result = await MeasureMark(ct);

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
        //  Bonding (현재 위치에서 가압 시퀀스 실행)
        // ══════════════════════════════════════════════

        [RelayCommand]
        public async Task Bonding()
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            var ct = GetToken();
            try
            {
                StatusText = "본딩(가압) 진행 중...";
                var history = new ObservableCollection<BondingDataPoint>();
                await _sequenceService.BondingPress(history, ct);
                StatusText = $"본딩 완료 — {history.Count}개 포인트 수집";
                _logger.Information("VisionTab 본딩 완료 — {Count}개 포인트", history.Count);
            }
            catch (OperationCanceledException) { StatusText = "취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "Bonding failed");
                StatusText = $"오류: {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        // ══════════════════════════════════════════════
        //  전체 시퀀스
        //  1) Top Die Pickup
        //  2) P-Table 이동      3) H_T 회전(사용자 설정)
        //  4) PC로 Top Align Mark 측정 Left/Right (H_X·P_Y 사용자 설정)
        //  5) W-Table(PLACE_CENTER) 이동
        //  6) 본딩(가압)
        //  7) HC1으로 Top Align Mark 측정 Left→Right (H_X·W_Y 이동량 사용자 설정)
        //  8) 모션·비전 측정값 CSV 저장
        // ══════════════════════════════════════════════

        // 개별 스텝을 IsNotBusy 가드 + 취소/예외 처리로 감싸 실행
        private async Task RunSeqStep(string name, Func<CancellationToken, Task> body)
        {
            if (!IsNotBusy) return;
            IsNotBusy = false;
            var ct = GetToken();
            try { await body(ct); }
            catch (OperationCanceledException) { SeqStatus = $"{name} — 취소됨"; }
            catch (Exception e)
            {
                _logger.Error(e, "{Step} 실패", name);
                SeqStatus = $"오류({name}): {e.Message}";
            }
            finally { IsNotBusy = true; }
        }

        // ── 개별 스텝 커맨드 (단계별 실행) ──
        [RelayCommand] public Task SeqStep1Pickup() => RunSeqStep("① Pickup", Step1PickupCore);
        [RelayCommand] public Task SeqStep2PTable() => RunSeqStep("② P-Table 이동", Step2PTableCore);
        [RelayCommand] public Task SeqStep3Rotate() => RunSeqStep("③ H_T 회전", Step3RotateCore);
        [RelayCommand] public Task SeqStep4PcMeasure() => RunSeqStep("④ PC 측정", Step4PcMeasureCore);
        [RelayCommand] public Task SeqStep5WTable() => RunSeqStep("⑤ W-Table 이동", Step5WTableCore);
        [RelayCommand] public Task SeqStep6Bonding() => RunSeqStep("⑥ 본딩", Step6BondingCore);
        [RelayCommand] public Task SeqStep7Hc1Measure() => RunSeqStep("⑦ HC1 측정", Step7Hc1MeasureCore);
        [RelayCommand] public Task SeqStep8SaveCsv() => RunSeqStep("⑧ CSV 저장", Step8SaveCsvCore);

        [RelayCommand]
        public void SeqClearResults()
        {
            SeqResults.Clear();
            SeqStatus = "측정 결과 초기화됨";
        }

        // ── 전체 시퀀스 (①~⑧ 연속 실행) ──
        [RelayCommand]
        public Task RunFullSequence() => RunSeqStep("전체 시퀀스", async ct =>
        {
            SeqResults.Clear();
            await Step1PickupCore(ct);
            await Step2PTableCore(ct);
            await Step3RotateCore(ct);
            await Step4PcMeasureCore(ct);
            await Step5WTableCore(ct);
            await Step6BondingCore(ct);
            await Step7Hc1MeasureCore(ct);
            await Step8SaveCsvCore(ct);
            SeqStatus = $"전체 완료 — {SeqResults.Count}개 측정, CSV 저장됨";
            _logger.Information("전체 시퀀스 완료 — {Count}개 측정", SeqResults.Count);
        });

        // ══════════════════════════════════════════════
        //  스텝 구현 (전체·개별 공용)
        // ══════════════════════════════════════════════

        // ① Top Die Pickup (저배 측정 → 픽업). 개별 실행 시 측정 결과 초기화.
        private async Task Step1PickupCore(CancellationToken ct)
        {
            SeqResults.Clear();
            SeqStatus = $"① Top Die #{SeqTopDie} 저배 측정...";
            var lowAlign = await _sequenceService.TopLowMeasure(SeqTopDie, MarkType.DIE_CENTER_TOP, ct);
            SeqStatus = "① Top Die Pickup...";
            await _sequenceService.MotionsMove(MotionExtensions.H_T, 0, ct);
            await _sequenceService.DTablePickup(DieType.TOP, SeqTopDie, lowAlign, ct);
            SeqStatus = "① Pickup 완료";
        }

        // ② P-Table 이동 (Head 안전 위치 상승)
        private async Task Step2PTableCore(CancellationToken ct)
        {
            SeqStatus = "② P-Table 이동(Head 안전 위치)...";
            await _sequenceService.Init_Head(ct);
            SeqStatus = "② P-Table 이동 완료";
        }

        // ③ H_T 회전 (사용자 설정)
        private async Task Step3RotateCore(CancellationToken ct)
        {
            SeqStatus = $"③ H_T 회전 {SeqHtRotation:F4} ...";
            await _sequenceService.MotionsMove(MotionExtensions.H_T, SeqHtRotation, ct);
            SeqStatus = "③ H_T 회전 완료";
        }

        // ④ PC로 Top Align Mark 측정 (Left / Right). XY 이동 후 H_Z 초점 하강.
        private async Task Step4PcMeasureCore(CancellationToken ct)
        {
            SeqStatus = "④ PC Left 이동·측정...";
            await Task.WhenAll(
                _sequenceService.MotionsMove(MotionExtensions.H_X, SeqPcLeftHx, ct),
                _sequenceService.MotionsMove(MotionExtensions.P_Y, SeqPcLeftPy, ct));
            await _sequenceService.MotionsMove(MotionExtensions.H_Z, SeqPcLeftHz, ct);
            var pcLeft = await MeasureAlign(CameraType.PC_HIGH, DirectType.LEFT, false, ct);
            AddSeqResult("4.PC", CameraType.PC_HIGH, DirectType.LEFT, MotionExtensions.P_Y, _pyAxis, pcLeft);

            SeqStatus = "④ PC Right 이동·측정...";
            await Task.WhenAll(
                _sequenceService.MotionsMove(MotionExtensions.H_X, SeqPcRightHx, ct),
                _sequenceService.MotionsMove(MotionExtensions.P_Y, SeqPcRightPy, ct));
            await _sequenceService.MotionsMove(MotionExtensions.H_Z, SeqPcRightHz, ct);
            var pcRight = await MeasureAlign(CameraType.PC_HIGH, DirectType.RIGHT, false, ct);
            AddSeqResult("4.PC", CameraType.PC_HIGH, DirectType.RIGHT, MotionExtensions.P_Y, _pyAxis, pcRight);
            SeqStatus = "④ PC 측정 완료";
        }

        // ⑤ W-Table(PLACE_CENTER) 이동 — OffsetX/Y 적용
        private async Task Step5WTableCore(CancellationToken ct)
        {
            SeqStatus = "⑤ W-Table(PLACE_CENTER) 이동...";
            await _sequenceService.Init_Head(ct);
            await Task.WhenAll(
                _sequenceService.MotionsMove(MotionExtensions.H_X, "PLACE_CENTER", ct),
                _sequenceService.MotionsMove(MotionExtensions.W_Y, "PLACE_CENTER", ct));
            SeqStatus = $"⑤ W-Table 이동 완료 (Offset X:{OffsetX:F4}, Y:{OffsetY:F4})";
        }

        // ⑥ 본딩 (Z 하강 → 가압)
        private async Task Step6BondingCore(CancellationToken ct)
        {
            SeqStatus = "⑥ 본딩 Z 하강...";
            double topDieThickness = await _sequenceService.GetRecipe("TopDieThickness");
            double btmDieThickness = await _sequenceService.GetRecipe("BtmDieThickness");
            double shankToWaferOffset = _paramService.GetDouble("ShankToWaferOffset");
            double readyPosition = await _sequenceService.GetRecipe("READY_POSITION");
            await _sequenceService.MotionsMove(MotionExtensions.H_Z,
                shankToWaferOffset - topDieThickness - btmDieThickness - readyPosition, ct);

            SeqStatus = "⑥ 본딩(가압)...";
            var history = new ObservableCollection<BondingDataPoint>();
            await _sequenceService.BondingPress(history, ct);
            SeqStatus = $"⑥ 본딩 완료 — {history.Count}개 포인트";
        }

        // ⑦ HC1으로 Top Align Mark 측정 (Left → Right)
        private async Task Step7Hc1MeasureCore(CancellationToken ct)
        {
            SeqStatus = "⑦ HC1 초점(H_Z) 이동...";
            await Task.WhenAll(
                _sequenceService.MotionsMove(MotionExtensions.H_X, "PLACE_CENTER", OffsetX, ct),
                _sequenceService.MotionsMove(MotionExtensions.W_Y, "PLACE_CENTER", OffsetY, ct));
            await _sequenceService.MotionsMove(MotionExtensions.h_z, 1.8, ct);
            await _sequenceService.MotionsMove(MotionExtensions.H_Z, SeqHc1Hz, ct);

            SeqStatus = "⑦ HC1 Left 측정...";
            var hc1Left = await MeasureAlign(CameraType.HC1_HIGH, DirectType.LEFT, SeqApplyHcAlignZGap, ct);
            AddSeqResult("7.HC1", CameraType.HC1_HIGH, DirectType.LEFT, MotionExtensions.W_Y, _wyAxis, hc1Left);

            SeqStatus = "⑦ HC1 Left→Right 이동·측정...";
            await Task.WhenAll(
                _sequenceService.RelativeMotionsMove(MotionExtensions.H_X, SeqHc1DeltaHx, ct),
                _sequenceService.RelativeMotionsMove(MotionExtensions.W_Y, SeqHc1DeltaWy, ct));
            var hc1Right = await MeasureAlign(CameraType.HC1_HIGH, DirectType.RIGHT, SeqApplyHcAlignZGap, ct);
            AddSeqResult("7.HC1", CameraType.HC1_HIGH, DirectType.RIGHT, MotionExtensions.W_Y, _wyAxis, hc1Right);
            SeqStatus = "⑦ HC1 측정 완료";
        }

        // ⑧ 슬립 테스트 측정값을 한 행으로 직렬화하여 단일 CSV 파일에 누적 저장
        private async Task Step8SaveCsvCore(CancellationToken ct)
        {
            await SaveSlipTestCsv(ct);
            SeqStatus = $"⑧ CSV 저장 완료 — {SeqResults.Count}개 측정";
        }

        // 지정 카메라·방향으로 Align Mark 측정 (AF → Vision)
        //  · applyHcAlignZGap=true 이고 HC1/HC2 카메라이면 촬상 전 h_z/H_Z를
        //    FID_ALIGN_GAP 만큼 이동해 Align 초점을 맞추고, 측정 후 복귀한다.
        private async Task<VisionMarkPositionResponse?> MeasureAlign(
            CameraType cam, DirectType dir, bool applyHcAlignZGap, CancellationToken ct)
        {
            bool zMove = applyHcAlignZGap &&
                         cam is CameraType.HC1_HIGH or CameraType.HC2_HIGH;
            double gap = 0;

            //if (zMove)
            //{
            //    gap = _recipeService.FindByParamDouble(MotionExtensions.FID_ALIGN_GAP);
            //    await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, -gap, ct);
            //    await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, gap, ct);
            //}

            try
            {
                await _communication.RequestAFStart(cam, MarkType.ALIGN_MARK, ct);
                var r = await _communication.RequestVisionMarkPosition(
                    MarkType.ALIGN_MARK, cam, dir.ToString());
                if (r == null) throw new Exception($"{cam} {dir} 비전 응답 null");
                if (r.Result == Result.NG) throw new Exception($"{cam} {dir} 비전 측정 실패");
                return r;
            }
            finally
            {
                //if (zMove)
                //{
                //    try
                //    {
                //        await _sequenceService.RelativeMotionsMove(MotionExtensions.H_Z, -gap, ct);
                //        await _sequenceService.RelativeMotionsMove(MotionExtensions.h_z, gap, ct);
                //    }
                //    catch (Exception e) { _logger.Warning(e, "HC Align Z 복귀 실패"); }
                //}
            }
        }

        // 측정 시점의 모션(H_X, Y축, H_Z) + 비전 결과를 한 행으로 기록
        private void AddSeqResult(string step, CameraType cam, DirectType dir,
            string yAxisName, IAxis? yAxis, VisionMarkPositionResponse? r)
        {
            SeqResults.Add(new SeqMeasurePoint
            {
                Step = step,
                Camera = cam.ToString(),
                Direction = dir.ToString(),
                HtRotation = SeqHtRotation,
                Hx = _hxAxis?.CurrentPosition ?? 0,
                YAxisName = yAxisName,
                Y = yAxis?.CurrentPosition ?? 0,
                Hz = _hzAxis?.CurrentPosition ?? 0,
                VisionX = r?.X ?? double.NaN,
                VisionY = r?.Y ?? double.NaN,
                Result = r?.Result.ToString() ?? "NULL",
            });
        }

        // 슬립 테스트 1회 실행 결과(PC/HC1 Left·Right 4점)를 한 행으로 직렬화하여
        // 단일 파일(SlipTest.csv)에 누적 저장한다.
        //  · 각도            : H_T 회전 지령값(SeqHtRotation)
        //  · 비전 측정치     : PC/HC Align Mark Left·Right 의 VisionX/Y
        //  · 모션 측정치     : 각 측정 시점의 H_X / Y(P_Y|W_Y) / H_Z
        //  · 얼라인 상대거리 : Left↔Right 마크 절대위치(모션+비전) 간 거리 (PC/HC 각각)
        //  · THETA           : Left→Right 마크 라인의 각도(deg) (PC/HC 각각)
        private async Task SaveSlipTestCsv(CancellationToken ct)
        {
            try
            {
                SeqMeasurePoint? Find(string step, DirectType dir) =>
                    SeqResults.FirstOrDefault(p => p.Step == step && p.Direction == dir.ToString());

                var pcL = Find("4.PC", DirectType.LEFT);
                var pcR = Find("4.PC", DirectType.RIGHT);
                var hcL = Find("7.HC1", DirectType.LEFT);
                var hcR = Find("7.HC1", DirectType.RIGHT);

                // 얼라인 마크 절대 위치 = 모션 위치 + 비전 측정 오프셋(mm)
                Point2D Mark(SeqMeasurePoint? p) => p == null
                    ? Point2D.Zero
                    : Point2D.of(p.Hx + p.VisionX, p.Y + p.VisionY);

                // Left↔Right 상대거리(mm)와 Left→Right 각도(THETA, deg)
                (double dist, double theta) LR(SeqMeasurePoint? l, SeqMeasurePoint? r)
                {
                    var lp = Mark(l);
                    var rp = Mark(r);
                    double dist = CalibrationMath.Distance(rp, lp);
                    double theta = CalibrationMath.ToDegree(Math.Atan2(rp.Y - lp.Y, rp.X - lp.X));
                    return (dist, theta);
                }

                var (pcDist, pcTheta) = LR(pcL, pcR);
                var (hcDist, hcTheta) = LR(hcL, hcR);

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "HCB", "시퀀스 데이터");
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, "SlipTest.csv");
                bool exists = File.Exists(path);

                const string header =
                    "Timestamp,Angle," +
                    "PC_ALIGN_L_X,PC_ALIGN_L_Y,PC_ALIGN_R_X,PC_ALIGN_R_Y," +
                    "HC_ALIGN_L_X,HC_ALIGN_L_Y,HC_ALIGN_R_X,HC_ALIGN_R_Y," +
                    "PC_L_Hx,PC_L_Y,PC_L_Hz,PC_R_Hx,PC_R_Y,PC_R_Hz," +
                    "HC_L_Hx,HC_L_Y,HC_L_Hz,HC_R_Hx,HC_R_Y,HC_R_Hz," +
                    "PC_RelDist,HC_RelDist,PC_Theta,HC_Theta";

                string V(SeqMeasurePoint? p, bool x) =>
                    p == null ? "" : (x ? p.VisionX : p.VisionY).ToString("F6");
                string M(SeqMeasurePoint? p, char c) =>
                    p == null ? "" : (c == 'x' ? p.Hx : c == 'y' ? p.Y : p.Hz).ToString("F6");

                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var line = string.Join(",", new[]
                {
                    ts,
                    SeqHtRotation.ToString("F6"),
                    V(pcL, true), V(pcL, false), V(pcR, true), V(pcR, false),
                    V(hcL, true), V(hcL, false), V(hcR, true), V(hcR, false),
                    M(pcL, 'x'), M(pcL, 'y'), M(pcL, 'z'), M(pcR, 'x'), M(pcR, 'y'), M(pcR, 'z'),
                    M(hcL, 'x'), M(hcL, 'y'), M(hcL, 'z'), M(hcR, 'x'), M(hcR, 'y'), M(hcR, 'z'),
                    pcDist.ToString("F6"), hcDist.ToString("F6"),
                    pcTheta.ToString("F6"), hcTheta.ToString("F6"),
                });

                if (!exists)
                    await File.AppendAllTextAsync(path,
                        header + Environment.NewLine + line + Environment.NewLine, ct);
                else
                    await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);

                _logger.Information("슬립 테스트 CSV 누적 저장: {Path}", path);
            }
            catch (Exception e)
            {
                _logger.Warning(e, "슬립 테스트 CSV 저장 실패");
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

    // 전체 시퀀스 측정 1행 (모션 + 비전)
    public class SeqMeasurePoint
    {
        public string Step { get; set; } = "";
        public string Camera { get; set; } = "";
        public string Direction { get; set; } = "";
        public double HtRotation { get; set; }
        public double Hx { get; set; }
        public string YAxisName { get; set; } = "";
        public double Y { get; set; }
        public double Hz { get; set; }
        public double VisionX { get; set; }
        public double VisionY { get; set; }
        public string Result { get; set; } = "";

        public double VisionXUm => VisionX * 1000.0;
        public double VisionYUm => VisionY * 1000.0;
    }
}
