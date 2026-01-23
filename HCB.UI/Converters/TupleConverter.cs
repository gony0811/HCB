using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace HCB.UI.Converters
{
    public class TupleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // IAxis와 DMotionPosition을 Tuple로 만듭니다.
            // values[0]는 IAxis, values[1]은 DMotionPosition이 될 것입니다.
            if (values != null && values.Length == 2 && values[0] is IAxis axis && values[1] is DMotionPosition position)
            {
                return new Tuple<IAxis, DMotionPosition>(axis, position);
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // 이 시나리오에서는 ConvertBack이 필요하지 않습니다.
            throw new NotImplementedException();
        }
    }
}