using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;


namespace TGZH.Control.Converter;

public partial class BooleanToVisibilityConverter : IValueConverter

{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            return v == Visibility.Visible;
        }
        return null!;
    }
}