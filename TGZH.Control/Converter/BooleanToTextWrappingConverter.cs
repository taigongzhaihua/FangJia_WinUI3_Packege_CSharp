using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace TGZH.Control.Converter;

internal partial class BooleanToTextWrappingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? TextWrapping.Wrap : TextWrapping.NoWrap;
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is TextWrapping v)
        {
            return v == TextWrapping.Wrap;
        }
        return null;
    }
}