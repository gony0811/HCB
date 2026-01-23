using System;
using System.Globalization;

using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace HCB.UI
{
    public class NotConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v is bool b ? !b : Binding.DoNothing;
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => Binding.DoNothing;
    }
}
