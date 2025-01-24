using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;

#pragma warning disable CA1416
#pragma warning disable CsWinRT1028

namespace FangJia.Converters;

public class BoolToNavigateBackVisible : IValueConverter

{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? NavigationViewBackButtonVisible.Visible : NavigationViewBackButtonVisible.Collapsed;
        }

        return null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is NavigationViewBackButtonVisible v)
        {
            return v == NavigationViewBackButtonVisible.Visible;
        }
        return null!;
    }
}