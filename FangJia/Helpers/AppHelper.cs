//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using System;
using System.Reflection;

namespace FangJia.Helpers;

public static class AppHelper
{
    /// <summary>
    /// 从字符串转换为枚举。
    /// </summary>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    /// <param name="text">要转换的字符串。</param>
    /// <returns>枚举值。</returns>
    /// <exception cref="InvalidOperationException">如果泛型参数 'TEnum' 不是枚举，则抛出异常。</exception>
    public static TEnum GetEnum<TEnum>(string? text) where TEnum : struct
    {
        if (!typeof(TEnum).GetTypeInfo().IsEnum)
        {
            throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
        }

        return (TEnum)Enum.Parse(typeof(TEnum), text!);
    }

    /// <summary>
    /// 获取当前路径下文件的绝对路径。
    /// </summary>
    /// <param name="fileName">文件名。</param>
    /// <returns>文件的绝对路径。</returns>

    public static string GetFilePath(string fileName)
    {
        return System.IO.Path.Combine(AppContext.BaseDirectory, fileName);
    }
}