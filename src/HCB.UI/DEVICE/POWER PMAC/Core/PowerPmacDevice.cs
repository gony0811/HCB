using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        // ══════════════════════════════════════════════════════════════
        // RefreshStatus — 위치 수신 후 인터락 자동 체크
        // ══════════════════════════════════════════════════════════════
        public async Task RefreshStatus()
        {
            foreach (var motion in MotionList)
            {
                try
                {
                    // 1. 쿼리 생성
                    var sb = new StringBuilder();
                    sb.Append($"Motor[{motion.MotorNo}].Status[0] ");
                    sb.Append($"Motor[{motion.MotorNo}].HomePos ");
                    sb.Append($"Motor[{motion.MotorNo}].ActPos ");
                    sb.Append($"Motor[{motion.MotorNo}].DesPos ");

                    // 2. 비동기 전송
                    string strResponse = await SendCommand<string>(sb.ToString());

                    // 3. 파싱
                    var values = strResponse.Split(
                        new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length < 4) continue;

                    uint status0 = Convert.ToUInt32(values[0].Replace("$", ""), 16);
                    double homePosV = Convert.ToDouble(values[1]);
                    double actPosV = Convert.ToDouble(values[2]);
                    double desPosV = Convert.ToDouble(values[3]);

                    double homePos = homePosV / motion.EncoderCountPerUnit;
                    double actPos = actPosV / motion.EncoderCountPerUnit;
                    double desPos = desPosV / motion.EncoderCountPerUnit;

                    // CurrentPosition setter 에서 DAxis.CheckInterlock() 가 자동 호출됩니다.
                    motion.CurrentPosition = actPos - homePos;
                    motion.CommandPosition = desPos - homePos;

                    // ── 상태 비트 파싱 ────────────────────────────────
                    motion.IsEnabled = (status0 & 0x00002000) != 0;  // Bit13 ClosedLoop
                    motion.IsHomeDone = (status0 & 0x00008000) != 0;  // Bit15 HomeComplete
                    motion.IsError = (status0 & 0x01000000) != 0;  // Bit24 AmpFault
                    motion.IsPlusLimit = (status0 & 0x10000000) != 0;  // Bit28
                    motion.IsMinusLimit = (status0 & 0x20000000) != 0;  // Bit29

                    bool isInPosBit = (status0 & 0x00000800) != 0;   // Bit11 InPos
                    motion.IsBusy = !isInPosBit;

                    bool isPosDiffOk = Math.Abs(motion.CommandPosition - motion.CurrentPosition)
                                        <= motion.InpositionRange;
                    motion.InPosition = isInPosBit && isPosDiffOk;

                    // ── 인터락 체크 (DeviceLevel 2중 안전) ───────────
                    // DAxis.CurrentPosition setter 에서 이미 체크하지만,
                    // Device 수준에서 한 번 더 검사하여 이중으로 보장합니다.
                    CheckInterlockForAxis(motion);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Motion[{MotorNo}] Update Error", motion.MotorNo);
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
            // 하드웨어 리미트 스위치 동작 시 인터락 강제 설정
            if (motion.IsPlusLimit || motion.IsMinusLimit)
            {
                string limitType = motion.IsPlusLimit ? "Plus" : "Minus";
                _logger.Error(
                    "[INTERLOCK HW LIMIT] {Name}(Motor#{No}) {Type} 리미트 스위치 동작",
                    motion.Name, motion.MotorNo, limitType);

                // IAxis 의 인터락 서비스에 직접 접근하는 대신 EStop 호출
                _ = motion.EStop();
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
            foreach (var motion in MotionList)
            {
                try
                {
                    await motion.MoveStop();
                }
                catch (Exception e)
                {
                    throw new Exception("Stop 명령중 에러 발생", e);
                }
            }
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

            DTKPowerPmac.Instance.GetResponseA(_uDeviceId, byCommand, byResponse, byResponse.Length - 1);

            string trimmed = Encoding.ASCII.GetString(byResponse).Trim();

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