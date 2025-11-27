
using System.Runtime.InteropServices;

namespace HCB.UI
{
    #region 64bit Interop 선언부
    internal static class DTKPowerPmac64Interop
    {
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKPowerPmacOpen")] public static extern uint Open(uint ip, uint mode);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKPowerPmacClose")] public static extern uint Close(uint id);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKGetDeviceCount")] public static extern uint GetDeviceCount(out int count);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKGetIPAddress")] public static extern uint GetIPAddress(uint id, out uint ip);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKConnect")] public static extern uint Connect(uint id);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKDisconnect")] public static extern uint Disconnect(uint id);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKIsConnected")] public static extern uint IsConnected(uint id, out int connected);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKGetResponseA")] public static extern uint GetResponseA(uint id, byte[] cmd, byte[] resp, int len);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKSendCommandA")] public static extern uint SendCommandA(uint id, byte[] cmd);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKAbort")] public static extern uint Abort(uint id);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKDownloadA")]
        public static extern uint DownloadA(uint id, byte[] data, int bDownload,
            DTKPowerPmacBase.PDOWNLOAD_PROGRESS prog, DTKPowerPmacBase.PDOWNLOAD_MESSAGE_A msg);
        [DllImport("PowerPmac64.dll", EntryPoint = "DTKSetReceiveA")] public static extern uint SetReceiveA(uint id, DTKPowerPmacBase.PRECEIVE_PROC_A recv);
    }
    #endregion

    #region 32bit Interop 선언부
    internal static class DTKPowerPmac32Interop
    {
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKPowerPmacOpen")] public static extern uint Open(uint ip, uint mode);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKPowerPmacClose")] public static extern uint Close(uint id);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKGetDeviceCount")] public static extern uint GetDeviceCount(out int count);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKGetIPAddress")] public static extern uint GetIPAddress(uint id, out uint ip);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKConnect")] public static extern uint Connect(uint id);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKDisconnect")] public static extern uint Disconnect(uint id);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKIsConnected")] public static extern uint IsConnected(uint id, out int connected);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKGetResponseA")] public static extern uint GetResponseA(uint id, byte[] cmd, byte[] resp, int len);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKSendCommandA")] public static extern uint SendCommandA(uint id, byte[] cmd);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKAbort")] public static extern uint Abort(uint id);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKDownloadA")]
        public static extern uint DownloadA(uint id, byte[] data, int bDownload,
            DTKPowerPmacBase.PDOWNLOAD_PROGRESS prog, DTKPowerPmacBase.PDOWNLOAD_MESSAGE_A msg);
        [DllImport("PowerPmac32.dll", EntryPoint = "DTKSetReceiveA")] public static extern uint SetReceiveA(uint id, DTKPowerPmacBase.PRECEIVE_PROC_A recv);
    }
    #endregion
}
