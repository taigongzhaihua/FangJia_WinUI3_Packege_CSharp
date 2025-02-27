//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

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