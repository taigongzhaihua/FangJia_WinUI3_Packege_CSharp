//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;

#pragma warning disable CA1416
#pragma warning disable CsWinRT1028

namespace FangJia.Converters;

public partial class BoolToNavigateBackVisible : IValueConverter

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