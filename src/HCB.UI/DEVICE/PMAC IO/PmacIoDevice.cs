using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace HCB.UI
{
    public partial class PmacIoDevice : ObservableObject, IIoDevice
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
        [ObservableProperty] private IoDeviceType ioDeviceType;
        [ObservableProperty] public ObservableCollection<IIoData> ioDataList = new ObservableCollection<IIoData>();

        private ILogger logger;
        private uint uDeviceId;
        private bool bInitialized = false;

        public PmacIoDevice()
        {
            this.logger = Serilog.Log.ForContext<PmacIoDevice>();
        }

        public PmacIoDevice(ILogger logger)
        {
            this.logger = logger.ForContext<PmacIoDevice>();
        }

        public async Task Connect()
        {
            Byte[] byCommand;
            UInt32 uRet;

            try
            {
                uRet = await Task.Run(() => DTKPowerPmac.Instance.Connect(uDeviceId));

                if ((DTK_STATUS)uRet == DTK_STATUS.DS_Ok)
                {
                    byCommand = new Byte[255];
                    byCommand = System.Text.Encoding.ASCII.GetBytes("echo 3");
                    uRet = DTKPowerPmac.Instance.SendCommandA(uDeviceId, byCommand);
                    IsConnected = true;
                    this.logger.Information("Power PMAC IO Device Connected. IP: {Ip}", Ip);

                    
                }
                else
                {
                    DTKPowerPmac.Instance.Close(uDeviceId);
                    uDeviceId = int.MaxValue;
                    IsConnected = false;
                }
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Power PMAC IO Device Connection Failed. IP: {Ip}", Ip);
                IsConnected = false;
            }
        }

        public async Task Disconnect()
        {
            if (IsConnected)
            {
                DTKPowerPmac.Instance.IsConnected(uDeviceId, out int connected);

                if (connected == 1)
                    await Task.Run(() => DTKPowerPmac.Instance.Disconnect(uDeviceId));
                DTKPowerPmac.Instance.Close(uDeviceId);
                uDeviceId = int.MaxValue;
                IsConnected = false;
            }
        }

        public Task Initialize()
        {
            UInt32 uIPAddress;
            String[] strIP = new String[4];
            strIP = Ip.Split('.');
            uIPAddress = (Convert.ToUInt32(strIP[0]) << 24) | (Convert.ToUInt32(strIP[1]) << 16) | (Convert.ToUInt32(strIP[2]) << 8) | Convert.ToUInt32(strIP[3]);

            try
            {
                uDeviceId = DTKPowerPmac.Instance.Open(uIPAddress, (uint)DTK_MODE_TYPE.DM_GPASCII);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, "Power PMAC IO Device Initialization Failed. IP: {Ip}", Ip);
            }


            return Task.CompletedTask;
        }

        public Task RefreshStatus()
        {
            var tasks = IoDataList
                .Cast<AbstractIoBase>()
                .Where(io => !(bInitialized && (io.IoType == IoType.DigitalOutput || io.IoType == IoType.AnalogOutput)))
                .Select(async io =>
                {
                    try
                    {
                        string strCommand = io.Address;
                        string strResponse = await SendCommand<string>(strCommand);

                        switch (io.IoType)
                        {
                            case IoType.AnalogOutput:
                                (io as AnalogOutput).Value = double.Parse(strResponse);
                                break;
                            case IoType.AnalogInput:
                                (io as AnalogInput).Value = double.Parse(strResponse);
                                break;
                            case IoType.DigitalOutput:
                                (io as DigitalOutput).Value = uint.Parse(strResponse) > 0;
                                break;
                            case IoType.DigitalInput:
                                (io as DigitalInput).Value = uint.Parse(strResponse) > 0;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 예외 처리 로직
                    }
                });

            return Task.WhenAll(tasks)
                       .ContinueWith(_ =>
                       {
                           if (!bInitialized) bInitialized = true;
                       });
        }

        public Task<bool> TestConnection()
        {
            throw new NotImplementedException();
        }

        public IIoData FindIoDataByName(string name)
        {
            return IoDataList.FirstOrDefault<IIoData>(io => io.Name == name);
        }


        public void SetDigital(string name, bool bOnOff, bool simulation = false)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("IO name is null or empty", nameof(name));

            try
            {
                if (!simulation)
                {
                    var io = FindIoDataByName(name);

                    // 1) 존재 여부 체크
                    if (io == null)
                    {
                        logger.Error("SetDigital failed: IO '{Name}' not found.", name);
                        throw new InvalidOperationException($"Digital IO '{name}' not found.");
                    }

                    // 2) 타입 체크
                    if (io is not DigitalOutput ioData)
                    {
                        logger.Error(
                            "SetDigital failed: IO '{Name}' is not DigitalOutput (ActualType={Type})",
                            name, io.GetType().Name
                        );
                        throw new InvalidOperationException(
                            $"IO '{name}' is not DigitalOutput (Actual: {io.GetType().Name})"
                        );
                    }

                    // 3) 값 변경 필요할 때만 명령 전송
                    if (ioData.Value != bOnOff)
                    {
                        string strCommand = $"{ioData.Address}={(bOnOff ? 1 : 0)}";
                        SendCommand(strCommand);
                    }

                    // 4) 내부 상태 업데이트
                    ioData.Value = bOnOff;
                }
                else
                {
                    // Simulation 모드
                    var simIo = FindIoDataByName(name);

                    if (simIo == null)
                    {
                        logger.Warning("[Simulation] IO '{Name}' not found.", name);
                        return; // 시뮬레이션에서는 그냥 무시하거나 정책적으로 throw 가능
                    }

                    if (simIo is not AbstractDigital simIoData)
                    {
                        logger.Warning(
                            "[Simulation] IO '{Name}' is not AbstractDigital (ActualType={Type})",
                            name, simIo.GetType().Name
                        );
                        return;
                    }

                    simIoData.Value = bOnOff;
                    logger.Information("[Simulation] Set Digital IO: {Name} = {Value}", name, bOnOff);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "SetDigital exception: IO='{Name}', Value={Value}, Simulation={Simulation}",
                             name, bOnOff, simulation);
                throw; // 상위 시퀀스에서 StepState Failed 처리
            }
        }



        public async Task<bool> SetDigitalAsync(string name, bool bOnOff, int retry = 5, int delayMs = 300)
        {
            var ioData = (DigitalOutput)FindIoDataByName(name);

            try
            {
                // 이미 해당 상태라면 바로 true 반환
                if (ioData.Value == bOnOff) return true;

                // 명령 전송
                string strCommand = bOnOff ? $"{ioData.Address}=1" : $"{ioData.Address}=0";
                await SendCommand(strCommand); // 비동기 명령 전송 가정

                // 결과 확인 루프
                for (int i = 0; i < retry; i++)
                {
                    await Task.Delay(delayMs);

                    if (GetDigital(name) == bOnOff)
                    {
                        ioData.Value = bOnOff; // 메모리 값 업데이트
                        return true;
                    }
                }

                throw new TimeoutException($"[TimeOut] SET DIGITAL: {name}");
            }
            catch (Exception e)
            {
                this.logger.Error(e, "SetDigitalAsync Failed: {Name}", name);
                return false;
            }
        }

        public bool GetDigital(string name)
        {
            var ioData = (AbstractDigital)FindIoDataByName(name);
            if (ioData != null)
            {
                return ioData.Value;
            }
            return false;
        }  
        
        public double GetAnalog(string name)
        {
            var ioData = (AbstractAnalog)FindIoDataByName(name);
            if (ioData != null)
            {
                return ioData.Value;
            }
            return 0.0;
        }

        public void SetAnalog(string name, double value, bool simulation = false)
        {
            if (simulation == false)
            {
                var ioData = (AnalogOutput)FindIoDataByName(name);
                if (ioData != null)
                {
                    ioData.Value = value;
                }
            }
            else // simulation == true
            {
                var simIoData = (AbstractAnalog)FindIoDataByName(name);
                simIoData.Value = value;
                this.logger.Information("[Simulation] Set Analog IO: {Name} to {Value}", name, value);
            }
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
            String strResponse = "";
            Byte[] byCommand;
            Byte[] byResponse;
            byCommand = new Byte[255];
            byResponse = new Byte[255];

            String stringcmd = command;

            byCommand = System.Text.Encoding.ASCII.GetBytes(stringcmd);
            DTKPowerPmac.Instance.GetResponseA(uDeviceId, byCommand, byResponse, Convert.ToInt32(byResponse.Length - 1));
            strResponse = System.Text.Encoding.ASCII.GetString(byResponse);


            return Task.FromResult((TResult)Convert.ChangeType(strResponse, typeof(TResult)));
        }
    }
}
