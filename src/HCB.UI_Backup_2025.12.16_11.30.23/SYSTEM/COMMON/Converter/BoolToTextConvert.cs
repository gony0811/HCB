
using System;
using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace HCB.UI
{
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            var s = (p as string ?? "ON|OFF").Split('|');
            return (v is bool b && b) ? s[0] : s[1];
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
