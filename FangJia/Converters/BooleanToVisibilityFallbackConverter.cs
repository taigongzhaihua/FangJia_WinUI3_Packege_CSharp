using Microsoft.UI.Xaml.Data;
using System;

namespace FangJia.Converters;

public partial class BooleanToVisibilityFallbackConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        }
        return Microsoft.UI.Xaml.Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Microsoft.UI.Xaml.Visibility v)
        {
            return v == Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        return true;
    }

}