using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
namespace HCB.UI
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b)
                ? new SolidColorBrush(Colors.Green)
                : new SolidColorBrush(Colors.Red);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
    

