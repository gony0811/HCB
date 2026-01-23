namespace HCB.UI
{
    #region 타입 정의
    public enum DTK_MODE_TYPE
    {
        DM_GPASCII = 0,
        DM_GETSENDS_0 = 1,
        DM_GETSENDS_1 = 2,
        DM_GETSENDS_2 = 3,
        DM_GETSENDS_3 = 4,
        DM_GETSENDS_4 = 5
    }

    public enum DTK_STATUS
    {
        DS_Ok = 0,
        DS_Exception = 1,
        DS_TimeOut = 2,
        DS_Connected = 3,
        DS_NotConnected = 4,
        DS_Failed = 5,
        DS_InvalidDevice = 11,
        DS_LengthExceeds = 21,
        DS_RunningDownload = 22,
        DS_RunningRead = 23
    }

    public enum DTK_RESET_TYPE
    {
        DR_Reset = 0,
        DR_FullReset = 1
    }
    #endregion

    #region Delegate / 인터페이스
    public abstract class DTKPowerPmacBase
    {
        public delegate void PDOWNLOAD_MESSAGE_A(string lpMessage);
        public delegate void PDOWNLOAD_PROGRESS(int nPercent);
        public delegate void PRECEIVE_PROC_A(string lpReceive);
    }

    public interface IDTKPowerPmac
    {
        uint Open(uint dwIPAddress, uint uMode);
        uint Close(uint uDeviceID);
        uint GetDeviceCount(out int pnDeviceCount);
        uint GetIPAddress(uint uDeviceID, out uint pdwIPAddress);
        uint Connect(uint uDeviceID);
        uint Disconnect(uint uDeviceID);
        uint IsConnected(uint uDeviceID, out int pbConnected);
        uint GetResponseA(uint uDeviceID, byte[] command, byte[] lpResponse, int nLength);
        uint SendCommandA(uint uDeviceID, byte[] command);
        uint Abort(uint uDeviceID);
        uint DownloadA(uint uDeviceID, byte[] lpwDownload, int bDownload,
                       DTKPowerPmacBase.PDOWNLOAD_PROGRESS lpProgress,
                       DTKPowerPmacBase.PDOWNLOAD_MESSAGE_A lpMessage);
        uint SetReceiveA(uint uDeviceID, DTKPowerPmacBase.PRECEIVE_PROC_A lpReceiveProc);
    }
    #endregion

    // ──────────────────────────────────────────────────────────────
    #region 인터페이스 구현 클래스

    public class DTKPowerPmac64 : DTKPowerPmacBase, IDTKPowerPmac
    {
        public uint Open(uint dwIPAddress, uint uMode) => DTKPowerPmac64Interop.Open(dwIPAddress, uMode);
        public uint Close(uint uDeviceID) => DTKPowerPmac64Interop.Close(uDeviceID);
        public uint GetDeviceCount(out int pnDeviceCount) => DTKPowerPmac64Interop.GetDeviceCount(out pnDeviceCount);
        public uint GetIPAddress(uint uDeviceID, out uint pdwIPAddress) => DTKPowerPmac64Interop.GetIPAddress(uDeviceID, out pdwIPAddress);
        public uint Connect(uint uDeviceID) => DTKPowerPmac64Interop.Connect(uDeviceID);
        public uint Disconnect(uint uDeviceID) => DTKPowerPmac64Interop.Disconnect(uDeviceID);
        public uint IsConnected(uint uDeviceID, out int pbConnected) => DTKPowerPmac64Interop.IsConnected(uDeviceID, out pbConnected);
        public uint GetResponseA(uint uDeviceID, byte[] lpCommand, byte[] lpResponse, int nLength)
            => DTKPowerPmac64Interop.GetResponseA(uDeviceID, lpCommand, lpResponse, nLength);
        public uint SendCommandA(uint uDeviceID, byte[] lpCommand) => DTKPowerPmac64Interop.SendCommandA(uDeviceID, lpCommand);
        public uint Abort(uint uDeviceID) => DTKPowerPmac64Interop.Abort(uDeviceID);
        public uint DownloadA(uint uDeviceID, byte[] lpwDownload, int bDownload,
            PDOWNLOAD_PROGRESS lpProgress, PDOWNLOAD_MESSAGE_A lpMessage)
            => DTKPowerPmac64Interop.DownloadA(uDeviceID, lpwDownload, bDownload, lpProgress, lpMessage);
        public uint SetReceiveA(uint uDeviceID, PRECEIVE_PROC_A lpReceiveProc)
            => DTKPowerPmac64Interop.SetReceiveA(uDeviceID, lpReceiveProc);
    }

    public class DTKPowerPmac32 : DTKPowerPmacBase, IDTKPowerPmac
    {
        public uint Open(uint dwIPAddress, uint uMode) => DTKPowerPmac32Interop.Open(dwIPAddress, uMode);
        public uint Close(uint uDeviceID) => DTKPowerPmac32Interop.Close(uDeviceID);
        public uint GetDeviceCount(out int pnDeviceCount) => DTKPowerPmac32Interop.GetDeviceCount(out pnDeviceCount);
        public uint GetIPAddress(uint uDeviceID, out uint pdwIPAddress) => DTKPowerPmac32Interop.GetIPAddress(uDeviceID, out pdwIPAddress);
        public uint Connect(uint uDeviceID) => DTKPowerPmac32Interop.Connect(uDeviceID);
        public uint Disconnect(uint uDeviceID) => DTKPowerPmac32Interop.Disconnect(uDeviceID);
        public uint IsConnected(uint uDeviceID, out int pbConnected) => DTKPowerPmac32Interop.IsConnected(uDeviceID, out pbConnected);
        public uint GetResponseA(uint uDeviceID, byte[] lpCommand, byte[] lpResponse, int nLength)
            => DTKPowerPmac32Interop.GetResponseA(uDeviceID, lpCommand, lpResponse, nLength);
        public uint SendCommandA(uint uDeviceID, byte[] lpCommand) => DTKPowerPmac32Interop.SendCommandA(uDeviceID, lpCommand);
        public uint Abort(uint uDeviceID) => DTKPowerPmac32Interop.Abort(uDeviceID);
        public uint DownloadA(uint uDeviceID, byte[] lpwDownload, int bDownload,
            PDOWNLOAD_PROGRESS lpProgress, PDOWNLOAD_MESSAGE_A lpMessage)
            => DTKPowerPmac32Interop.DownloadA(uDeviceID, lpwDownload, bDownload, lpProgress, lpMessage);
        public uint SetReceiveA(uint uDeviceID, PRECEIVE_PROC_A lpReceiveProc)
            => DTKPowerPmac32Interop.SetReceiveA(uDeviceID, lpReceiveProc);
    }
    #endregion
}
