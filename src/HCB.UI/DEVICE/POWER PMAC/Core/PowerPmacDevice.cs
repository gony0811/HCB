using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
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
        //public async Task RefreshStatus()
        //{
        //    foreach (var motion in MotionList)
        //    {
        //        try
        //        {
        //            // 1. 명령어 생성
        //            // 띄어쓰기로 구분하여 여러 값을 한 번에 요청합니다.
        //            var sb = new System.Text.StringBuilder();
        //            sb.Append($"Motor[{motion.MotorNo}].Status[0] ");       // 상태 비트 (매뉴얼 기준)
        //            sb.Append($"Motor[{motion.MotorNo}].HomePos ");         // 홈 오프셋
        //            sb.Append($"Motor[{motion.MotorNo}].ActPos ");          // 현재 위치 (Encoder)
        //            sb.Append($"Motor[{motion.MotorNo}].DesPos ");          // 명령 위치 (Desired)
        //                                                                    // (HomeComplete는 Status[0]의 Bit 15에 있으므로 별도 요청 불필요)

        //            // 2. 비동기 전송 및 응답 수신
        //            string strResponse = await SendCommand<string>(sb.ToString());

        //            // 3. 응답 파싱 (줄바꿈 문자로 분리)
        //            var values = strResponse.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        //            if (values.Length < 4) continue; // 데이터가 부족하면 스킵

        //            // 4. 데이터 변환
        //            // Status[0]은 Hex($) 문자열이므로 처리 필요
        //            uint status0 = Convert.ToUInt32(values[0].Replace("$", ""), 16);

        //            double homePosVal = Convert.ToDouble(values[1]);
        //            double actPosVal = Convert.ToDouble(values[2]);
        //            double desPosVal = Convert.ToDouble(values[3]);

        //            // 단위 변환
        //            double homePos = homePosVal / motion.EncoderCountPerUnit;
        //            double actPos = actPosVal / motion.EncoderCountPerUnit;
        //            double desPos = desPosVal / motion.EncoderCountPerUnit;

        //            motion.CurrentPosition = actPos - homePos;
        //            motion.CommandPosition = desPos - homePos;

        //            // =========================================================
        //            // [중요] 매뉴얼 기반 상태 비트 분석
        //            // =========================================================

        //            // 1. Servo On 상태 (Bit 13: ClosedLoop) - $2000
        //            motion.IsEnabled = (status0 & 0x00002000) != 0;

        //            // 2. 원점 복귀 완료 (Bit 15: HomeComplete) - $8000
        //            motion.IsHomeDone = (status0 & 0x00008000) != 0;

        //            // 3. 에러 상태 (Bit 24: AmpFault) - $1000000
        //            // 필요시 Bit 26(FeFatal, $4000000)이나 Bit 21(I2tFault)도 OR 조건으로 추가 가능
        //            motion.IsError = (status0 & 0x01000000) != 0;

        //            // 4. 하드웨어 리미트
        //            // Plus Limit (Bit 28) - $10000000
        //            motion.IsPlusLimit = (status0 & 0x10000000) != 0;

        //            // Minus Limit (Bit 29) - $20000000
        //            motion.IsMinusLimit = (status0 & 0x20000000) != 0;

        //            // 5. 이동 중 여부 (Busy)
        //            // Bit 11 (InPos)이 0이면 "이동 중"으로 판단
        //            // InPos ($800): 목표 위치 도달 시 1, 이동 중이거나 오차 크면 0
        //            bool isInPosBit = (status0 & 0x00000800) != 0;
        //            motion.IsBusy = !isInPosBit;

        //            // 6. InPosition 최종 판단
        //            // 하드웨어 신호(Bit 11)와 소프트웨어 오차 범위 체크를 동시에 만족해야 True
        //            bool isPosDiffOk = (Math.Abs(motion.CommandPosition - motion.CurrentPosition) <= motion.InpositionRange);

        //            motion.InPosition = isInPosBit && isPosDiffOk;
        //        }
        //        catch (Exception ex)
        //        {
        //            // 디버깅을 위해 콘솔에 출력 (실제 운영 시엔 로그 기록)
        //            Console.WriteLine($"Motion[{motion.MotorNo}] Update Error: {ex.Message}");
        //        }
        //    }
        //}

        // ══════════════════════════════════════════════════════════════
        // RefreshStatus — 위치 수신 후 인터락 자동 체크
        // ══════════════════════════════════════════════════════════════
        //}

        public async Task RefreshStatus()
        {
            foreach (var motion in MotionList)
            {
                try
                {
                    // 1. 해당 축에 필요한 데이터만 묶어서 요청
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"Motor[{motion.MotorNo}].Status[0] ");
                    sb.Append($"Motor[{motion.MotorNo}].HomePos ");
                    sb.Append($"Motor[{motion.MotorNo}].ActPos ");
                    sb.Append($"Motor[{motion.MotorNo}].DesPos ");
                    sb.Append($"Motor[{motion.MotorNo}].InPos"); // PMAC 내부 InPos 상태 직접 요청

                    // 2. 비동기 전송 및 응답 수신
                    string strResponse = await SendCommand<string>(sb.ToString());
                    var values = strResponse.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    // 데이터 개수 검증 (5개의 값이 들어와야 함)
                    if (values.Length < 5)
                    {
                        Console.WriteLine($"Motion[{motion.MotorNo}] 데이터 응답 부족: {values.Length}개");
                        continue;
                    }

                    // 3. 데이터 파싱 및 변환
                    uint status0 = Convert.ToUInt32(values[0].Replace("$", ""), 16);
                    double homePosVal = Convert.ToDouble(values[1]);
                    double actPosVal = Convert.ToDouble(values[2]);
                    double desPosVal = Convert.ToDouble(values[3]);
                    int inPosRaw = Convert.ToInt32(values[4]); // 1: 정지(InPos), 0: 이동중

                    // 단위 변환 및 위치 계산
                    double scale = motion.EncoderCountPerUnit;
                    motion.CurrentPosition = (actPosVal - homePosVal) / scale;
                    motion.CommandPosition = (desPosVal - homePosVal) / scale;

                    // 4. 상태 비트 분석 (Status[0] 기반)
                    motion.IsEnabled = (status0 & 0x00002000) != 0;   // Bit 13: ClosedLoop
                    motion.IsHomeDone = (status0 & 0x00008000) != 0;  // Bit 15: HomeComplete
                    motion.IsError = (status0 & 0x01000000) != 0;     // Bit 24: AmpFault
                    motion.IsPlusLimit = (status0 & 0x10000000) != 0; // Bit 28: PlusLimit
                    motion.IsMinusLimit = (status0 & 0x20000000) != 0;// Bit 29: MinusLimit

                    // 5. [핵심] Motor[i].InPos 기반 최종 상태 결정
                    // PMAC이 판단한 InPos 값을 그대로 적용 (매우 정확함)
                    motion.InPosition = (inPosRaw == 1);

                    // InPosition이면 Busy가 아님
                    motion.IsBusy = !motion.InPosition;
                }
                catch (Exception ex)
                {
                    // 한 축이 실패해도 로그만 남기고 다음 축으로 넘어감
                    Console.WriteLine($"Motion[{motion.MotorNo}] Update Error: {ex.Message}");
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