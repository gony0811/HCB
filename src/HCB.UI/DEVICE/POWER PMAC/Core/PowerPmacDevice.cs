using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Fixed.Model.Data;
using Telerik.Windows.Documents.Flow.FormatProviders.Html;
using static System.Net.WebRequestMethods;

namespace HCB.UI
{
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
        [ObservableProperty] public ObservableCollection<IAxis> motionList = new ObservableCollection<IAxis>();
        
        private ILogger logger;
        private uint uDeviceId;

        public PowerPmacDevice()
        {
        }

        public PowerPmacDevice(ILogger logger)
        {
            this.logger = logger.ForContext<PowerPmacDevice>();
        }

        public Task Connect()
        {
            Byte[] byCommand;
            UInt32 uRet;
            uDeviceId = 0;
            uRet = DTKPowerPmac.Instance.Connect(uDeviceId);

            if ((DTK_STATUS)uRet == DTK_STATUS.DS_Ok)
            {
                byCommand = new Byte[255];
                byCommand = System.Text.Encoding.ASCII.GetBytes("echo 3");
                uRet = DTKPowerPmac.Instance.SendCommandA(uDeviceId, byCommand);
                IsConnected = true;
            }
            else
            {
                DTKPowerPmac.Instance.Close(uDeviceId);
                uDeviceId = int.MaxValue;
                IsConnected = false;
            }

            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            if (IsConnected)
            {
                DTKPowerPmac.Instance.IsConnected(uDeviceId, out int connected);

                if(connected == 1)
                    DTKPowerPmac.Instance.Disconnect(uDeviceId);
                DTKPowerPmac.Instance.Close(uDeviceId);
                uDeviceId = int.MaxValue;
                IsConnected = false;
            }

            return Task.CompletedTask;
        }

        public Task Initialize()
        {
            try
            {
                UInt32 uIPAddress;
                String[] strIP = new String[4];
                strIP = Ip.Split('.');
                uIPAddress = (Convert.ToUInt32(strIP[0]) << 24) | (Convert.ToUInt32(strIP[1]) << 16) | (Convert.ToUInt32(strIP[2]) << 8) | Convert.ToUInt32(strIP[3]);


                uDeviceId = DTKPowerPmac.Instance.Open(uIPAddress, (uint)DTK_MODE_TYPE.DM_GPASCII);
            }
            catch (Exception ex)
            {
                logger.Error("PowerPmacDevice Initialize Open Error: {0}", ex.Message);
                throw;
            }


            return Task.CompletedTask;
        }

        public async Task RefreshStatus()
        {
            foreach (var motion in MotionList)
            {
                try
                {
                    // 1. 명령어 생성
                    // 띄어쓰기로 구분하여 여러 값을 한 번에 요청합니다.
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"Motor[{motion.MotorNo}].Status[0] ");       // 상태 비트 (매뉴얼 기준)
                    sb.Append($"Motor[{motion.MotorNo}].HomePos ");         // 홈 오프셋
                    sb.Append($"Motor[{motion.MotorNo}].ActPos ");          // 현재 위치 (Encoder)
                    sb.Append($"Motor[{motion.MotorNo}].DesPos ");          // 명령 위치 (Desired)
                                                                            // (HomeComplete는 Status[0]의 Bit 15에 있으므로 별도 요청 불필요)

                    // 2. 비동기 전송 및 응답 수신
                    string strResponse = await SendCommand<string>(sb.ToString());

                    // 3. 응답 파싱 (줄바꿈 문자로 분리)
                    var values = strResponse.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (values.Length < 4) continue; // 데이터가 부족하면 스킵

                    // 4. 데이터 변환
                    // Status[0]은 Hex($) 문자열이므로 처리 필요
                    uint status0 = Convert.ToUInt32(values[0].Replace("$", ""), 16);

                    double homePosVal = Convert.ToDouble(values[1]);
                    double actPosVal = Convert.ToDouble(values[2]);
                    double desPosVal = Convert.ToDouble(values[3]);

                    // 단위 변환
                    double homePos = homePosVal / motion.EncoderCountPerUnit;
                    double actPos = actPosVal / motion.EncoderCountPerUnit;
                    double desPos = desPosVal / motion.EncoderCountPerUnit;

                    motion.CurrentPosition = actPos - homePos;
                    motion.CommandPosition = desPos - homePos;

                    // =========================================================
                    // [중요] 매뉴얼 기반 상태 비트 분석
                    // =========================================================

                    // 1. Servo On 상태 (Bit 13: ClosedLoop) - $2000
                    motion.IsEnabled = (status0 & 0x00002000) != 0;

                    // 2. 원점 복귀 완료 (Bit 15: HomeComplete) - $8000
                    motion.IsHomeDone = (status0 & 0x00008000) != 0;

                    // 3. 에러 상태 (Bit 24: AmpFault) - $1000000
                    // 필요시 Bit 26(FeFatal, $4000000)이나 Bit 21(I2tFault)도 OR 조건으로 추가 가능
                    motion.IsError = (status0 & 0x01000000) != 0;

                    // 4. 하드웨어 리미트
                    // Plus Limit (Bit 28) - $10000000
                    motion.IsPlusLimit = (status0 & 0x10000000) != 0;

                    // Minus Limit (Bit 29) - $20000000
                    motion.IsMinusLimit = (status0 & 0x20000000) != 0;

                    // 5. 이동 중 여부 (Busy)
                    // Bit 11 (InPos)이 0이면 "이동 중"으로 판단
                    // InPos ($800): 목표 위치 도달 시 1, 이동 중이거나 오차 크면 0
                    bool isInPosBit = (status0 & 0x00000800) != 0;
                    motion.IsBusy = !isInPosBit;

                    // 6. InPosition 최종 판단
                    // 하드웨어 신호(Bit 11)와 소프트웨어 오차 범위 체크를 동시에 만족해야 True
                    bool isPosDiffOk = (Math.Abs(motion.CommandPosition - motion.CurrentPosition) <= motion.InpositionRange);

                    motion.InPosition = isInPosBit && isPosDiffOk;
                }
                catch (Exception ex)
                {
                    // 디버깅을 위해 콘솔에 출력 (실제 운영 시엔 로그 기록)
                    Console.WriteLine($"Motion[{motion.MotorNo}] Update Error: {ex.Message}");
                }
            }
        }

        public Task RefreshStatus_()
        {          
            foreach (var motion in MotionList)
            {
                // 여기서 각 모션의 상태를 갱신하는 로직을 구현해야 합니다.
                // 예: motion.CurrentPosition = GetCurrentPositionFromDevice(motion.Id);
                try
                {
                    String strCommand = "";
                    String strResponse = "";
                    string[] strResponseArry = new string[10];
                    uint intResponse = 0;

                    strCommand = "Motor[" + (motion.MotorNo).ToString() + "].Status[0]";         //모터 상태
                    strCommand += "Motor[" + (motion.MotorNo).ToString() + "].HomePos";          //home 위치  
                    strCommand += "Motor[" + (motion.MotorNo).ToString() + "].ActPos";           //encode 위치 
                    strCommand += "Motor[" + (motion.MotorNo).ToString() + "].DesPos";           //
                    strCommand += "Motor[" + (motion.MotorNo).ToString() + "].HomeComplete";     // Home Completed 
                    strResponse = SendCommand<string>(strCommand).Result;
                    strResponseArry[0] = strResponse.Substring(0, strResponse.IndexOf("\r\n"));
                    strResponse = strResponse.Remove(0, strResponse.IndexOf("\r\n") + 2);
                    strResponseArry[1] = strResponse.Substring(0, strResponse.IndexOf("\r\n"));
                    strResponse = strResponse.Remove(0, strResponse.IndexOf("\r\n") + 2);
                    strResponseArry[2] = strResponse.Substring(0, strResponse.IndexOf("\r\n"));
                    strResponse = strResponse.Remove(0, strResponse.IndexOf("\r\n") + 2);
                    strResponseArry[3] = strResponse.Substring(0, strResponse.IndexOf("\r\n"));
                    strResponse = strResponse.Remove(0, strResponse.IndexOf("\r\n") + 2);

                    intResponse = Convert.ToUInt32(strResponseArry[0].Substring(1, strResponseArry[0].Length - 1), 16);  //Using ToUInt32 not ToUInt64, as per OP comment
                    double homepos = Convert.ToDouble(strResponseArry[1]) / motion.EncoderCountPerUnit;
                    double feedpos = Convert.ToDouble(strResponseArry[2]) / motion.EncoderCountPerUnit;
                    double commandpos = Convert.ToDouble(strResponseArry[3]) / motion.EncoderCountPerUnit;

                    motion.CurrentPosition = feedpos - homepos;
                    motion.CommandPosition = commandpos - homepos;

                    // 상태 비트 설정
                    motion.IsEnabled = ((intResponse & 0x00001000) == 0x00001000);
                    motion.IsBusy = !((intResponse & 0x00002000) == 0x00002000);
                    motion.IsError = ((intResponse & 0x01000000) == 0x00100000);
                    motion.IsPlusLimit = ((intResponse & 0x10000000) == 0x10000000);
                    motion.IsMinusLimit = ((intResponse & 0x20000000) == 0x20000000);
                    motion.IsHomeDone = ((intResponse & 0x00004000) == 0x00004000);

                    if (motion.CommandPosition + motion.InpositionRange >= motion.CurrentPosition &&
                        motion.CommandPosition - motion.InpositionRange <= motion.CurrentPosition &&
                            (intResponse & 0x00000800) == 0x00000800)
                    {
                        motion.InPosition = true;
                    }
                    else
                    {
                        motion.InPosition = false;
                    }
                }
                catch (Exception ex)
                {
                    // 예외 처리 로직
                }
            }

            return Task.Delay(10);
        }

        public Task<bool> TestConnection()
        {
            throw new NotImplementedException();
        }

        public IAxis FindMotionByName(string name)
        {
            return MotionList.FirstOrDefault<IAxis>(motion => motion.Name == name);
        }

        public IAxis FindMotionByMotorIndex(int motorIndex)
        {
            return MotionList.FirstOrDefault<IAxis>(motion => motion.MotorNo == motorIndex);
        }


        public Task SendCommand(string command)
        {
            String strResponse = "";
            Byte[] byCommand;
            Byte[] byResponse;
            byCommand = new Byte[255];
            byResponse = new Byte[255];

            String stringcmd = command;

            byCommand = System.Text.Encoding.ASCII.GetBytes(stringcmd);
            DTKPowerPmac.Instance.GetResponseA(uDeviceId, byCommand, byResponse, Convert.ToInt32(byResponse.Length - 1));
            strResponse = System.Text.Encoding.ASCII.GetString(byResponse);

            return Task.CompletedTask;
        }

        public Task<TResult> SendCommand<TResult>(string command)
        {
            const int MAX_RESPONSE_LENGTH = 1024;
            String strResponse = "";
            Byte[] byCommand;
            Byte[] byResponse;
            byCommand = new Byte[MAX_RESPONSE_LENGTH];
            byResponse = new Byte[MAX_RESPONSE_LENGTH];

            String stringcmd = command;

            byCommand = Encoding.ASCII.GetBytes(command);
            uint errorCode = DTKPowerPmac.Instance.GetResponseA(uDeviceId, byCommand, byResponse, Convert.ToInt32(byResponse.Length - 1));
            strResponse = Encoding.ASCII.GetString(byResponse);
            string trimmedResponse = strResponse.Trim();
            if (trimmedResponse.StartsWith("?"))
            {
                // 오류 코드를 포함하는 예외를 발생시킵니다.
                throw new Exception($"PowerPMAC Command Error for '{command}': {trimmedResponse}");
            }

            try
            {
                // double 또는 int 같은 숫자로 변환할 때, 문화권에 독립적인 
                // CultureInfo.InvariantCulture를 사용하여 소수점(.) 포맷을 강제합니다.
                object convertedValue = Convert.ChangeType(trimmedResponse, typeof(TResult), CultureInfo.InvariantCulture);

                // Task로 래핑하여 반환
                return Task.FromResult((TResult)convertedValue);
            }
            catch (FormatException ex)
            {
                // 변환 실패(예: 숫자가 아닌 문자열을 double로 변환 시도)
                throw new FormatException($"Cannot convert response '{trimmedResponse}' to type {typeof(TResult).Name}. Command: {command}", ex);
            }
            catch (InvalidCastException ex)
            {
                // 타입 불일치 오류
                throw new InvalidCastException($"Invalid type conversion for response '{trimmedResponse}' to {typeof(TResult).Name}.", ex);
            }
        }
    }
}
