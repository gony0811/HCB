using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    /// <summary>
    /// 장비 판넬 스위치(DI) 감시 → 시퀀스 실행
    ///  · X000 (DI_START_SWITCH) 0→1 : Top Full Sequence 시작 (레시피 Component가 DIE면 Die, WAFER면 선택 Die Wafer 본딩)
    ///  · X001 (DI_STOP_SWITCH)  0→1 : Sequence Stop
    ///  · X002 (DI_RESET_SWITCH) 1→0 : MachineInitAsync (StepSeqTab Init)
    /// </summary>
    public class IoSwitchService : BackgroundService
    {
        private const int PollingIntervalMs = 50;

        private readonly ILogger _logger;
        private readonly DeviceManager _deviceManager;
        private readonly RecipeService _recipeService;
        // ViewModel은 UI 스레드에서 생성되어야 하므로(DispatcherTimer 등) 최초 사용 시점에 UI 스레드에서 지연 생성한다.
        private readonly Lazy<StepSeqTabViewModel> _stepSeqTab;
        private readonly Lazy<WaferSeqTabViewModel> _waferSeqTab;

        private bool? _prevStart;
        private bool? _prevStop;
        private bool? _prevReset;

        // 스위치로 시작한 시퀀스(Start/Init) — 진행 중 재시작 방지
        private Task _running = Task.CompletedTask;

        public IoSwitchService(
            ILogger logger,
            DeviceManager deviceManager,
            RecipeService recipeService,
            Lazy<StepSeqTabViewModel> stepSeqTab,
            Lazy<WaferSeqTabViewModel> waferSeqTab)
        {
            _logger = logger.ForContext<IoSwitchService>();
            _deviceManager = deviceManager;
            _recipeService = recipeService;
            _stepSeqTab = stepSeqTab;
            _waferSeqTab = waferSeqTab;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Poll();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "IoSwitchService 폴링 오류");
                }

                try { await Task.Delay(PollingIntervalMs, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void Poll()
        {
            var device = _deviceManager.GetDevice<PmacIoDevice>(IoExtensions.IoDeviceName);
            if (device == null) return;

            bool start = device.GetDigital(IoExtensions.DI_START_SWITCH);
            bool stop = device.GetDigital(IoExtensions.DI_STOP_SWITCH);
            bool reset = device.GetDigital(IoExtensions.DI_RESET_SWITCH);

            // 최초 1회는 기준값만 저장 (프로그램 시작 시 오동작 방지)
            if (_prevStart.HasValue)
            {
                if (stop && _prevStop == false) OnStop();
                if (start && _prevStart == false) OnStart();
                if (!reset && _prevReset == true) OnReset();
            }

            _prevStart = start;
            _prevStop = stop;
            _prevReset = reset;
        }

        private void OnStart()
        {
            if (!_running.IsCompleted)
            {
                _logger.Warning("[X000] START 무시 — 스위치로 시작한 시퀀스가 진행 중입니다.");
                return;
            }

            var component = _recipeService.UseRecipe?.Component;
            if (component == ComponentType.WAFER)
            {
                _logger.Information("[X000] START — Wafer Top Full Sequence 시작");
                _running = RunOnUi(async () =>
                {
                    var wafer = _waferSeqTab.Value;
                    if (wafer.SelectedDie == null)
                    {
                        _logger.Warning("[X000] Wafer 본딩 Die가 선택되지 않았습니다.");
                        return;
                    }
                    await wafer.BondingCommand.ExecuteAsync(null);
                });
            }
            else if (component == ComponentType.DIE)
            {
                _logger.Information("[X000] START — Die Top Full Sequence 시작");
                _running = RunOnUi(() => _stepSeqTab.Value.TopRunFullSequence());
            }
            else
            {
                _logger.Warning("[X000] START 무시 — 사용 레시피가 선택되지 않았습니다.");
            }
        }

        private void OnStop()
        {
            _logger.Information("[X001] STOP — Sequence Stop");
            _ = RunOnUi(() => _stepSeqTab.Value.Stop());
        }

        private void OnReset()
        {
            if (!_running.IsCompleted)
            {
                _logger.Warning("[X002] INIT 무시 — 스위치로 시작한 시퀀스가 진행 중입니다.");
                return;
            }

            _logger.Information("[X002] RESET(1→0) — MachineInitAsync 시작");
            _running = RunOnUi(() => _stepSeqTab.Value.Init());
        }

        // ViewModel 상태가 UI 바인딩되어 있으므로 UI 스레드에서 실행한다.
        private Task RunOnUi(Func<Task> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return Task.CompletedTask;

            return dispatcher.InvokeAsync(async () =>
            {
                try { await action(); }
                catch (Exception ex) { _logger.Error(ex, "IoSwitchService 시퀀스 실행 오류"); }
            }).Task.Unwrap();
        }
    }
}
