using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public enum eUpDown
    {
        Down,
        Up
    }

    public enum eOnOff
    {
        Off,
        On
    }

    internal static class EnumExtensions
    {
        // 사용법: myEnum.ToBool()
        public static bool ToBool(this eUpDown value, eUpDown up)
        {
            return value == eUpDown.Up; // Up이면 true 반환
        }

        // 사용법: myBool.ToUpDown()
        public static eUpDown ToUpDown(this bool value)
        {
            return value ? eUpDown.Up : eUpDown.Down;
        }

        public static eOnOff ToOnOff(this bool value)
        {
            return value ? eOnOff.On : eOnOff.Off;
        }
    }
}
