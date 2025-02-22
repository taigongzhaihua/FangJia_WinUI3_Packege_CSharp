using System;
using System.Reflection;

namespace FangJia.Helpers;

public static class EnumHelper
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
}