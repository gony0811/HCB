using System;
using System.Runtime.InteropServices;

namespace HCB.UI
{
    // ──────────────────────────────────────────────────────────────
    #region 팩토리 (자동 선택)
    public static class DTKPowerPmac
    {
        private static IDTKPowerPmac _instance;
        public static IDTKPowerPmac Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Environment.Is64BitProcess ?
                        new DTKPowerPmac64() as IDTKPowerPmac :
                        new DTKPowerPmac32() as IDTKPowerPmac;
                return _instance;
            }
        }
    }
    #endregion
}
