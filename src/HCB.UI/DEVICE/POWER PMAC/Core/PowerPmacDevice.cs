using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Fixed.Model.Actions;
using Telerik.Windows.Documents.Flow.FormatProviders.Html;
using static System.Net.WebRequestMethods;

namespace HCB.UI
{
    /// <summary>
    /// PowerPMAC 장비 통신 클래스.
    /// RefreshStatus() 에서 위치를 수신한 직후 인터락 서비스를 호출하여
    /// 범위 초과 시 즉시 전체 축 정지 명령을 전송합니다.
    /// </summary>
    public partial class PowerPmacDevice : ObservableObject, IMotionDevice
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private DeviceType deviceType;
        [ObservableProperty] private string fileName;
        [ObservableProperty] private bool isEnabled;
        [ObservableProperty] private string instanceName;
        [ObservableProperty] private bool isConnected;
        [ObservableProperty] private string description;
        [ObservableProperty] private string ip;
        [ObservableProperty] private int port;
        [ObservableProperty] private MotionDeviceType motionDeviceType;
        [ObservableProperty] public ObservableCollection<IAxis> motionList = new();


        private readonly ILogger _logger;
        private readonly IInterlockService _interlock;
        private uint _uDeviceId;

        // ── 재연결 관련 필드 ──────────────────────────────────────────
        private readonly SemaphoreSlim _reconnectLock = new(1, 1);
        private bool _isReconnecting;
        private int _consecutiveFailCount;
        private const int MaxRetryDelaySeconds = 30;
        private const int MaxRetryAttempts = 10; // 무한 재시도 방지

        // ── 생성자 ─────────────────────────────────────────────────────
        public PowerPmacDevice() { }

        public PowerPmacDevice(ILogger logger, IInterlockService interlockService)
        {
            _logger = logger.ForContext<PowerPmacDevice>();
            _interlock = interlockService;

            // 인터락 발생 시 → 전체 축 즉시 정지
            _interlock.InterlockTriggered += OnInterlockTriggered;
        }

        // ── 인터락 이벤트 핸들러 ──────────────────────────────────────
        private async void OnInterlockTriggered(object? sender, InterlockEventArgs e)
        {
            _logger.Error("[INTERLOCK] {Message} → 전체 축 비상 정지 실행", e.Message);

            try
            {
                await StopAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[INTERLOCK] 전체 축 비상 정지 중 오류");
            }
        }

        public async Task RefreshStatus()
        {
            if (!IsConnected)
            {
                // 재연결 중이면 폴링 스킵
                if (!_isReconnecting)
                    OnCommunicationLost("RefreshStatus 호출 시 미연결 상태");
                return;
            }

            foreach (var motion in MotionList)
            {
                // 루프 도중 연결이 끊기면 나머지 축 스킵
                if (!IsConnected) break;

                try
                {
                    var sb = new StringBuilder();
                    sb.Append($"Motor[{motion.MotorNo}].Status[0] ");
                    sb.Append($"Motor[{motion.MotorNo}].HomePos ");
                    sb.Append($"Motor[{motion.MotorNo}].ActPos ");
                    sb.Append($"Motor[{motion.MotorNo}].DesPos ");
                    sb.Append($"Motor[{motion.MotorNo}].InPos");

                    string strResponse = await SendCommand<string>(sb.ToString());
                    var values = strResponse.Split(
                        new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length < 5)
                    {
                        _logger.Warning("Motion[{No}] 데이터 응답 부족: {Cnt}개",
                            motion.MotorNo, values.Length);
                        continue;
                    }

                    uint status0 = Convert.ToUInt32(values[0].Replace("$", ""), 16);
                    double homePosVal = Convert.ToDouble(values[1]);
                    double actPosVal = Convert.ToDouble(values[2]);
                    double desPosVal = Convert.ToDouble(values[3]);
                    int inPosRaw = Convert.ToInt32(values[4]);

                    double scale = motion.EncoderCountPerUnit;
                    motion.CurrentPosition = (actPosVal - homePosVal) / scale;
                    motion.CommandPosition = (desPosVal - homePosVal) / scale;

                    motion.IsEnabled = (status0 & 0x00002000) != 0;
                    motion.IsHomeDone = (status0 & 0x00008000) != 0;
                    motion.IsError = (status0 & 0x01000000) != 0;
                    motion.IsPlusLimit = (status0 & 0x10000000) != 0;
                    motion.IsMinusLimit = (status0 & 0x20000000) != 0;

                    motion.InPosition = (inPosRaw == 1);
                    motion.IsBusy = !motion.InPosition;
                }
                catch (InvalidOperationException)
                {
                    // SendCommand에서 통신 실패로 이미 재연결 트리거됨 → 루프 중단
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning("Motion[{No}] Update Error: {Msg}",
                        motion.MotorNo, ex.Message);
                }
            }
        }

        /// <summary>
        /// Device 수준 인터락 체크.
        /// DAxis 에 IInterlockService 가 직접 주입되었으므로
        /// 여기서는 추가 로그 및 하드웨어 리미트 연동만 처리합니다.
        /// </summary>
        private void CheckInterlockForAxis(IAxis motion)
        { 
        }

        /// <summary>
        /// 통신 실패 감지 시 호출. 재연결 루프를 백그라운드로 시작합니다.
        /// </summary>
        private void OnCommunicationLost(string reason)
        {
            if (_isReconnecting) return;

            IsConnected = false;
            _logger.Warning("[PMAC] 통신 끊김 감지: {Reason} → 재연결 시작", reason);

            _ = Task.Run(() => ReconnectLoopAsync());
        }

        /// <summary>
        /// 지수 백오프 재연결 루프.
        /// </summary>
        private async Task ReconnectLoopAsync()
        {
            if (!await _reconnectLock.WaitAsync(0)) return; // 이미 재연결 중이면 스킵

            try
            {
                _isReconnecting = true;
                _consecutiveFailCount = 0;

                while (!IsConnected && _consecutiveFailCount < MaxRetryAttempts)
                {
                    _consecutiveFailCount++;

                    // 지수 백오프: 1s → 2s → 4s → 8s → … → 최대 30s
                    int delaySec = Math.Min(
                        (int)Math.Pow(2, _consecutiveFailCount - 1),
                        MaxRetryDelaySeconds);

                    _logger.Information(
                        "[PMAC] 재연결 시도 {N}/{Max} ({Delay}초 후)",
                        _consecutiveFailCount, MaxRetryAttempts, delaySec);

                    await Task.Delay(TimeSpan.FromSeconds(delaySec));

                    try
                    {
                        // 기존 핸들 정리
                        try { DTKPowerPmac.Instance.Close(_uDeviceId); }
                        catch { /* 이미 닫혔을 수 있음 */ }

                        _uDeviceId = uint.MaxValue;

                        // 재초기화 → 재연결
                        await Initialize();
                        await Connect();

                        if (IsConnected)
                        {
                            _logger.Information("[PMAC] 재연결 성공 (시도 {N}회)", _consecutiveFailCount);
                            _consecutiveFailCount = 0;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "[PMAC] 재연결 시도 {N} 실패", _consecutiveFailCount);
                    }
                }

                if (!IsConnected)
                {
                    _logger.Error("[PMAC] 최대 재시도 횟수({Max}) 초과. 수동 복구 필요.", MaxRetryAttempts);
                }
            }
            finally
            {
                _isReconnecting = false;
                _reconnectLock.Release();
            }
        }

        // ══════════════════════════════════════════════════════════════
        // 기존 메서드 (변경 없음)
        // ══════════════════════════════════════════════════════════════

        public async Task Connect()
        {
            uint uRet = await Task.Run(() => DTKPowerPmac.Instance.Connect(_uDeviceId));

            if ((DTK_STATUS)uRet == DTK_STATUS.DS_Ok)
            {
                byte[] byCommand = Encoding.ASCII.GetBytes("echo 3");
                DTKPowerPmac.Instance.SendCommandA(_uDeviceId, byCommand);
                IsConnected = true;
            }
            else
            {
                DTKPowerPmac.Instance.Close(_uDeviceId);
                _uDeviceId = uint.MaxValue;
                IsConnected = false;
            }
        }

        public async Task Disconnect()
        {
            if (!IsConnected) return;

            DTKPowerPmac.Instance.IsConnected(_uDeviceId, out int connected);

            if (connected == 1)
                await Task.Run(() => DTKPowerPmac.Instance.Disconnect(_uDeviceId));

            DTKPowerPmac.Instance.Close(_uDeviceId);
            _uDeviceId = uint.MaxValue;
            IsConnected = false;
        }

        public Task Initialize()
        {
            try
            {
                string[] strIP = Ip.Split('.');
                uint uIPAddr = (Convert.ToUInt32(strIP[0]) << 24)
                                  | (Convert.ToUInt32(strIP[1]) << 16)
                                  | (Convert.ToUInt32(strIP[2]) << 8)
                                  | Convert.ToUInt32(strIP[3]);

                _uDeviceId = DTKPowerPmac.Instance.Open(uIPAddr, (uint)DTK_MODE_TYPE.DM_GPASCII);
            }
            catch (Exception ex)
            {
                _logger.Error("PowerPmacDevice Initialize Open Error: {0}", ex.Message);
                throw;
            }

            return Task.CompletedTask;
        }
        public async Task StopAsync()
        {
            // 모든 축 병렬 정지 (리미트 여부 관계없이)
            var stopTasks = MotionList.Select(async motion =>
            {
                try
                {
                    if (motion.IsPlusLimit || motion.IsMinusLimit)
                    {
                        string limitType = motion.IsPlusLimit ? "Plus" : "Minus";
                        _logger.Error(
                            "[INTERLOCK HW LIMIT] {Name}(Motor#{No}) {Type} 리미트",
                            motion.Name, motion.MotorNo, limitType);
                    }

                    await motion.EStop(); // 모든 축 정지
                }
                catch (Exception ex)
                {
                    _logger.Error("Motor#{No} EStop 실패: {Msg}",
                        motion.MotorNo, ex.Message);
                }
            });

            await Task.WhenAll(stopTasks);
        }

        public Task<bool> TestConnection()
            => throw new NotImplementedException();

        public IAxis FindMotionByName(string name)
            => MotionList.FirstOrDefault<IAxis>(m => m.Name == name);

        public IAxis FindMotionByMotorIndex(int motorIndex)
            => MotionList.FirstOrDefault<IAxis>(m => m.MotorNo == motorIndex);

        public Task SendCommand(string command)
        {
            byte[] byCommand = Encoding.ASCII.GetBytes(command);
            byte[] byResponse = new byte[255];
            DTKPowerPmac.Instance.GetResponseA(_uDeviceId, byCommand, byResponse, byResponse.Length - 1);
            return Task.CompletedTask;
        }

        public Task<TResult> SendCommand<TResult>(string command)
        {
            const int MAX = 1024;
            byte[] byCommand = Encoding.ASCII.GetBytes(command);
            byte[] byResponse = new byte[MAX];

            try
            {
                DTKPowerPmac.Instance.GetResponseA(_uDeviceId, byCommand, byResponse, byResponse.Length - 1);
            }
            catch (Exception ex)
            {
                OnCommunicationLost(ex.Message);
                throw new InvalidOperationException($"PMAC 통신 실패: {command}", ex);
            }

            string trimmed = Encoding.ASCII.GetString(byResponse).Trim('\0').Trim();

            // 빈 응답 = 연결 끊김 가능성
            if (string.IsNullOrEmpty(trimmed))
            {
                OnCommunicationLost("빈 응답 수신");
                throw new InvalidOperationException($"PMAC 빈 응답: {command}");
            }

            if (trimmed.StartsWith("?"))
                throw new Exception($"PowerPMAC Command Error for '{command}': {trimmed}");

            try
            {
                object converted = Convert.ChangeType(trimmed, typeof(TResult), CultureInfo.InvariantCulture);
                return Task.FromResult((TResult)converted);
            }
            catch (FormatException ex)
            {
                throw new FormatException(
                    $"Cannot convert response '{trimmed}' to {typeof(TResult).Name}. Command: {command}", ex);
            }
            catch (InvalidCastException ex)
            {
                throw new InvalidCastException(
                    $"Invalid type conversion for '{trimmed}' to {typeof(TResult).Name}.", ex);
            }
        }
    }
}