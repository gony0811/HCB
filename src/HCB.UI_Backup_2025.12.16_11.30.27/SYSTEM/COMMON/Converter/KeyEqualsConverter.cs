using System;
using System.Globalization;
using System.Windows.Data;

namespace HCB.UI
{
    public class KeyEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type t, object parameter, CultureInfo c)
            => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

        // ConvertBack은 안 씀(명령으로 SelectedPageKey를 갱신하기 때문)
        public object ConvertBack(object value, Type t, object parameter, CultureInfo c)
            => Binding.DoNothing;
    }
}
