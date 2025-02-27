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

public partial class BoolToTextConverter : IValueConverter
{
    public string? TrueText { get; set; }
    public string? FalseText { get; set; }
    public object? Convert(object? value, Type targetType, object parameter, string language)
    {
        return value is true ? TrueText : FalseText;
    }

    public object ConvertBack(object? value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}