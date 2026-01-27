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

            foreach (var data in IoDataList)
            {
                // 여기서 각 io의 상태를 갱신하는 로직을 구현해야 합니다.
                try
                {
                    String strCommand = "";
                    String strResponse = "";
                    string[] strResponseArry = new string[10];

                    var io = data as AbstractIoBase;

                    strCommand = io.Address;

                    //strCommand = string.Format("{0}{1:D3}", io.Address, io.Index);

                    if (bInitialized && (io.IoType == IoType.DigitalOutput || io.IoType == IoType.AnalogOutput))
                    {
                        continue;
                    }

                    strResponse = SendCommand<string>(strCommand).Result;     

                    switch (io.IoType)
                    {
                        case IoType.AnalogOutput:
                            double aoVal = Double.Parse(strResponse);
                            (io as AnalogOutput).Value = aoVal;
                            break;
                        case IoType.AnalogInput:
                            double aiVal = Double.Parse(strResponse);
                            (io as AnalogInput).Value = aiVal;
                            break;
                        case IoType.DigitalOutput:
                            uint doVal = uint.Parse(strResponse);
                            (io as DigitalOutput).Value = doVal > 0 ? true : false;
                            break;
                        case IoType.DigitalInput:
                            uint diVal = uint.Parse(strResponse);
                            (io as DigitalInput).Value = diVal > 0 ? true : false;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // 예외 처리 로직
                }
                finally
                {
                    
                }
            }

            if (!bInitialized) bInitialized = true;

            return Task.Delay(10);
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
           
            if (simulation == false)
            {
                var ioData = (DigitalOutput)FindIoDataByName(name);


                if (ioData.Value != bOnOff)
                {
                    String strCommand = "";

                    strCommand = bOnOff ? string.Format("{0}=1", ioData.Address) : string.Format("{0}=0", ioData.Address);

                    SendCommand(strCommand);
                }

                ioData.Value = bOnOff;
            }
            else // simulation == true
            {
                var simIoData = (AbstractDigital)FindIoDataByName(name);
                simIoData.Value = bOnOff;
                this.logger.Information("[Simulation] Set Digital IO: {Name} to {Value}", name, bOnOff);
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
