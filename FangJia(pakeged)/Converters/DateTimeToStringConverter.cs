using Microsoft.UI.Xaml.Data;
using System;

namespace FangJia.Converters
{
    public partial class DateTimeToStringConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dt)
                return $"{dt:yyyy-MM-dd HH:mm:ss.fff}";
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
