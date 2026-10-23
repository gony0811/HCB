using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;
using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    /// <summary>
    /// W-Table Vacuum 전체(5채널) On/Off 공용 서비스.
    /// - 상태(IsOn)는 실제 DO(DO_WTABLE_VAC_n_ON) 값에서 계산한다 (5채널 모두 ON일 때만 ON).
    /// - 시퀀스 CancellationToken과 분리된 전용 토큰을 사용해 Vacuum 조작이 진행 중인 시퀀스를 취소하지 않는다.
    /// - 사이드바/ManualTab 등 여러 버튼이 이 서비스를 공유하므로 서로 상태가 어긋나지 않는다.
    /// </summary>
    [Service(Lifetime.Singleton)]
    public partial class WaferVacuumService : ObservableObject
    {
        public const int ReleaseDelayMs = 5000;

        private readonly ILogger _logger;
        private readonly SequenceService _sequenceService;
        private readonly DeviceManager _deviceManager;
        private DigitalOutput[] _vacOutputs = Array.Empty<DigitalOutput>();

        private CancellationTokenSource _cts;
        private Task _current = Task.CompletedTask;

        [ObservableProperty] private bool isOn;

        public WaferVacuumService(ILogger logger, DeviceManager deviceManager, SequenceService sequenceService)
        {
            _logger = logger.ForContext<WaferVacuumService>();
            _sequenceService = sequenceService;
            _deviceManager = deviceManager;

            EnsureOutputs();
            RefreshState();

            _sequenceService.InterlockActivated += Cancel;
        }

        // IO 디바이스가 아직 로드되지 않았을 수 있으므로 찾을 때까지 재시도
        private void EnsureOutputs()
        {
            if (_vacOutputs.Length > 0) return;

            var device = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            if (device == null) return;

            _vacOutputs = Enumerable.Range(1, IoExtensions.WTableVacuumChannelCount)
                .Select(ch => device.FindIoDataByName($"DO_WTABLE_VAC_{ch}_ON") as DigitalOutput)
                .Where(io => io != null)
                .ToArray();

            if (_vacOutputs.Length != IoExtensions.WTableVacuumChannelCount)
                _logger.Warning("W-Table Vacuum DO {Found}/{Expected}개만 찾음 — 상태 표시가 부정확할 수 있음",
                    _vacOutputs.Length, IoExtensions.WTableVacuumChannelCount);

            foreach (var io in _vacOutputs)
                io.ValueChanged += (_, _) => RefreshState();
        }

        private void RefreshState()
        {
            EnsureOutputs();
            IsOn = _vacOutputs.Length > 0 && _vacOutputs.All(io => io.Value);
        }

        public void Cancel()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        /// <summary>현재 실제 IO 상태 기준으로 반전 (ON → OFF, 그 외 → 전체 ON)</summary>
        public Task ToggleAsync() => SetAsync(!IsOn);

        /// <summary>
        /// 5채널 전체 On/Off. 진행 중인 이전 조작(예: OFF Release 대기)은 취소하고 끝난 뒤 실행한다.
        /// </summary>
        public async Task SetAsync(bool onOff)
        {
            Cancel();
            var cts = new CancellationTokenSource();
            _cts = cts;

            try { await _current; }
            catch { /* 이전 조작의 결과는 해당 호출에서 이미 처리됨 */ }

            if (cts.IsCancellationRequested) return; // 더 최신 요청이 들어옴

            _current = _sequenceService.WVacAllOnOff(onOff, ReleaseDelayMs, cts.Token);
            try
            {
                await _current;
                _logger.Information("Wafer Vacuum ALL {State}", onOff ? "ON" : "OFF");
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Wafer Vacuum ALL {State} 취소됨", onOff ? "ON" : "OFF");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Wafer Vacuum ALL {State} 실패", onOff ? "ON" : "OFF");
            }
            finally
            {
                RefreshState();
            }
        }
    }
}
