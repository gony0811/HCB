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

        public PmacIoDevice()
        {
            this.logger = Serilog.Log.ForContext<PmacIoDevice>();
        }

        public PmacIoDevice(ILogger logger)
        {
            this.logger = logger.ForContext<PmacIoDevice>();
        }

        public Task Connect()
        {
            Byte[] byCommand;
            UInt32 uRet;

            try
            {
                uRet = DTKPowerPmac.Instance.Connect(uDeviceId);

                if ((DTK_STATUS)uRet == DTK_STATUS.DS_Ok)
                {
                    byCommand = new Byte[255];
                    byCommand = System.Text.Encoding.GetEncoding("euc-kr").GetBytes("echo 3");
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

            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            if (IsConnected)
            {
                DTKPowerPmac.Instance.IsConnected(uDeviceId, out int connected);

                if (connected == 1)
                    DTKPowerPmac.Instance.Disconnect(uDeviceId);
                DTKPowerPmac.Instance.Close(uDeviceId);
                uDeviceId = int.MaxValue;
                IsConnected = false;
            }

            return Task.CompletedTask;
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

                    strCommand = string.Format("{0}{1:D4}", io.Address, io.Index);

                    strResponse = SendCommand<string>(strCommand).Result;

                    switch(io.IoType)
                    {
                        case IoType.AnalogInput:
                            (io as AnalogInput).Value = Convert.ToDouble(strResponse);
                            break;
                        case IoType.DigitalInput:
                            (io as DigitalInput).Value = (Convert.ToUInt32(strResponse) != 0);
                            break;
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

        public IIoData FindIoDataByName(string name)
        {
            return IoDataList.FirstOrDefault<IIoData>(io => io.Name == name);
        }

        

        public Task SendCommand(string command)
        {
            String strResponse = "";
            Byte[] byCommand;
            Byte[] byResponse;
            byCommand = new Byte[255];
            byResponse = new Byte[255];

            String stringcmd = command;

            byCommand = System.Text.Encoding.GetEncoding("euc-kr").GetBytes(stringcmd);
            DTKPowerPmac.Instance.GetResponseA((uint)Id, byCommand, byResponse, Convert.ToInt32(byResponse.Length - 1));
            strResponse = System.Text.Encoding.GetEncoding("euc-kr").GetString(byResponse);

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

            byCommand = System.Text.Encoding.GetEncoding("euc-kr").GetBytes(stringcmd);
            DTKPowerPmac.Instance.GetResponseA((uint)Id, byCommand, byResponse, Convert.ToInt32(byResponse.Length - 1));
            strResponse = System.Text.Encoding.GetEncoding("euc-kr").GetString(byResponse);


            return Task.FromResult((TResult)Convert.ChangeType(strResponse, typeof(TResult)));
        }
    }
}
