using FangJia.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace FangJia.Converters;

public partial class LogLevelToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string logLevel)
        {
            return logLevel.ToUpperInvariant() switch
            {
                "DEBUG" => new SolidColorBrush { Color = Colors.DodgerBlue }, //
                "INFO" => new SolidColorBrush { Color = ThemeHelper.IsDarkTheme() ? Colors.Cyan : Colors.DarkCyan },// 青色
                "WARN" => new SolidColorBrush { Color = ThemeHelper.IsDarkTheme() ? Colors.Orange : Colors.DarkOrange }, // 橙色
                "ERROR" => new SolidColorBrush { Color = Colors.IndianRed }, // 红色
                "FATAL" => new SolidColorBrush { Color = Colors.Red }, // 红色
                _ => new SolidColorBrush { Color = ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black }
            };
        }
        return new SolidColorBrush { Color = ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}