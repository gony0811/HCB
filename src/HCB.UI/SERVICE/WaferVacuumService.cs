using CommunityToolkit.Mvvm.ComponentModel;
using HCB.IoC;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    /// <summary>
    /// W-Table Vacuum On/Off 공용 서비스.
    /// - ON: 웨이퍼 사이즈(inch)에 따라 1~N 채널만 켠다 (1"=1, 4"=1~2, 6"=1~3, 8"=1~4, 12"=1~5).
    /// - OFF: 5채널 전체를 끈다 (Release 포함).
    /// - 상태는 실제 DO(DO_WTABLE_VAC_n_ON) 값에서 계산한다.
    /// - 시퀀스 CancellationToken과 분리된 전용 토큰을 사용해 Vacuum 조작이 진행 중인 시퀀스를 취소하지 않는다.
    /// </summary>
    [Service(Lifetime.Singleton)]
    public partial class WaferVacuumService : ObservableObject
    {
        public const int ReleaseDelayMs = 5000;

        // 웨이퍼 사이즈(inch) → 사용할 Vacuum 채널 수
        public static readonly IReadOnlyDictionary<int, int> WaferSizeChannelCount = new Dictionary<int, int>
        {
            [1] = 1,
            [4] = 2,
            [6] = 3,
            [8] = 4,
            [12] = 5,
        };

        private readonly ILogger _logger;
        private readonly SequenceService _sequenceService;
        private readonly DeviceManager _deviceManager;
        private Dictionary<int, DigitalOutput> _vacOutputs = new();

        private CancellationTokenSource _cts;
        private Task _current = Task.CompletedTask;

        /// <summary>한 채널이라도 ON이면 true</summary>
        [ObservableProperty] private bool isOn;

        /// <summary>현재 ON 상태 표시 문자열 (예: "Vac 1,2,3 ON")</summary>
        [ObservableProperty] private string statusText = "Vac OFF";

        /// <summary>마지막으로 선택한 웨이퍼 사이즈(inch)</summary>
        [ObservableProperty] private int selectedWaferInch = 12;

        public WaferVacuumService(ILogger logger, DeviceManager deviceManager, SequenceService sequenceService)
        {
            _logger = logger.ForContext<WaferVacuumService>();
            _sequenceService = sequenceService;
            _deviceManager = deviceManager;

            EnsureOutputs();
            RefreshState();

            _sequenceService.InterlockActivated += Cancel;
        }

        public static IEnumerable<int> ChannelsForSize(int inch)
            => Enumerable.Range(1, WaferSizeChannelCount.TryGetValue(inch, out var n) ? n : IoExtensions.WTableVacuumChannelCount);

        // IO 디바이스가 아직 로드되지 않았을 수 있으므로 찾을 때까지 재시도
        private void EnsureOutputs()
        {
            if (_vacOutputs.Count > 0) return;

            var device = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            if (device == null) return;

            _vacOutputs = Enumerable.Range(1, IoExtensions.WTableVacuumChannelCount)
                .Select(ch => (ch, io: device.FindIoDataByName($"DO_WTABLE_VAC_{ch}_ON") as DigitalOutput))
                .Where(x => x.io != null)
                .ToDictionary(x => x.ch, x => x.io);

            if (_vacOutputs.Count != IoExtensions.WTableVacuumChannelCount)
                _logger.Warning("W-Table Vacuum DO {Found}/{Expected}개만 찾음 — 상태 표시가 부정확할 수 있음",
                    _vacOutputs.Count, IoExtensions.WTableVacuumChannelCount);

            foreach (var io in _vacOutputs.Values)
                io.ValueChanged += (_, _) => RefreshState();
        }

        private List<int> OnChannels()
            => _vacOutputs.Where(kv => kv.Value.Value).Select(kv => kv.Key).OrderBy(ch => ch).ToList();

        private void RefreshState()
        {
            EnsureOutputs();
            var on = OnChannels();
            IsOn = on.Count > 0;
            StatusText = on.Count > 0 ? $"Vac {string.Join(",", on)} ON" : "Vac OFF";
        }

        public void Cancel()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// 웨이퍼 사이즈에 해당하는 채널(1~N)을 켜고, 그 밖에 켜져 있던 채널은 끈다.
        /// </summary>
        public Task TurnOnAsync(int inch)
        {
            SelectedWaferInch = inch;
            var target = ChannelsForSize(inch).ToList();

            return RunAsync($"Wafer Vacuum {inch}inch ON (Vac {string.Join(",", target)})", async ct =>
            {
                await _sequenceService.WVacChannelsOnOff(target, true, ReleaseDelayMs, ct);

                var extra = OnChannels().Except(target).ToList();
                if (extra.Count > 0)
                    await _sequenceService.WVacChannelsOnOff(extra, false, ReleaseDelayMs, ct);
            });
        }

        /// <summary>5채널 전체 OFF</summary>
        public Task TurnOffAsync()
            => RunAsync("Wafer Vacuum ALL OFF", ct =>
                _sequenceService.WVacChannelsOnOff(
                    Enumerable.Range(1, IoExtensions.WTableVacuumChannelCount), false, ReleaseDelayMs, ct));

        /// <summary>실제 IO 기준 반전 — ON이면 전체 OFF, OFF면 마지막 선택 사이즈로 ON</summary>
        public Task ToggleAsync() => IsOn ? TurnOffAsync() : TurnOnAsync(SelectedWaferInch);

        /// <summary>
        /// 진행 중인 이전 조작(예: OFF Release 대기)은 취소하고 끝난 뒤 실행한다.
        /// </summary>
        private async Task RunAsync(string name, Func<CancellationToken, Task> operation)
        {
            Cancel();
            var cts = new CancellationTokenSource();
            _cts = cts;

            try { await _current; }
            catch { /* 이전 조작의 결과는 해당 호출에서 이미 처리됨 */ }

            if (cts.IsCancellationRequested) return; // 더 최신 요청이 들어옴

            _current = operation(cts.Token);
            try
            {
                await _current;
                _logger.Information("{Name} 완료", name);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("{Name} 취소됨", name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "{Name} 실패", name);
            }
            finally
            {
                RefreshState();
            }
        }
    }
}
