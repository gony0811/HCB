
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Binding = System.Windows.Data.Binding;

namespace HCB.UI
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
        {
            var s = (p as string ?? "#4CAF50|#BDBDBD").Split('|');
            var hex = (v is bool b && b) ? s[0] : s[1];
            return (SolidColorBrush)(new BrushConverter().ConvertFrom(hex)!);
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

}
