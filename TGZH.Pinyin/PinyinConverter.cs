using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TGZH.Pinyin;

/// <summary>
/// 拼音格式转换工具
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal static partial class PinyinConverter
{
    /// <summary>
    /// 移除拼音中的声调标记
    /// </summary>
    public static string RemoveToneMarks(string pinyin)
    {
        if (string.IsNullOrEmpty(pinyin))
            return string.Empty;

        var result = pinyin;

        // 替换带声调的元音
        result = result.Replace("ā", "a").Replace("á", "a").Replace("ǎ", "a").Replace("à", "a");
        result = result.Replace("ē", "e").Replace("é", "e").Replace("ě", "e").Replace("è", "e");
        result = result.Replace("ī", "i").Replace("í", "i").Replace("ǐ", "i").Replace("ì", "i");
        result = result.Replace("ō", "o").Replace("ó", "o").Replace("ǒ", "o").Replace("ò", "o");
        result = result.Replace("ū", "u").Replace("ú", "u").Replace("ǔ", "u").Replace("ù", "u");
        result = result.Replace("ǖ", "ü").Replace("ǘ", "ü").Replace("ǚ", "ü").Replace("ǜ", "ü");
        result = result.Replace("ń", "n").Replace("ň", "n").Replace("ǹ", "n");
        result = result.Replace("ḿ", "m").Replace("m̀", "m");

        return result;
    }

    /// <summary>
    /// 将带声调的拼音转换为带数字声调的拼音
    /// </summary>
    public static string ConvertToToneNumber(string pinyin)
    {
        if (string.IsNullOrEmpty(pinyin))
            return string.Empty;

        // 多音节拼音处理
        if (!pinyin.Contains(' ')) return ConvertSyllableToToneNumber(pinyin);
        var parts = pinyin.Split(' ');
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = ConvertSyllableToToneNumber(parts[i]);
        }
        return string.Join(" ", parts);

    }

    /// <summary>
    /// 转换单个音节为数字声调
    /// </summary>
    private static string ConvertSyllableToToneNumber(string syllable)
    {
        if (string.IsNullOrEmpty(syllable))
            return string.Empty;

        // 轻声
        if (IsNeutralTone(syllable))
        {
            return RemoveToneMarks(syllable) + "0";
        }

        // 寻找声调
        var toneNumber = 0;

        if (syllable.Contains('ā') || syllable.Contains('ē') || syllable.Contains('ī') ||
            syllable.Contains('ō') || syllable.Contains('ū') || syllable.Contains('ǖ') ||
            syllable.Contains('ń') || syllable.Contains('ḿ'))
        {
            toneNumber = 1;
        }
        else if (syllable.Contains('á') || syllable.Contains('é') || syllable.Contains('í') ||
                 syllable.Contains('ó') || syllable.Contains('ú') || syllable.Contains('ǘ'))
        {
            toneNumber = 2;
        }
        else if (syllable.Contains('ǎ') || syllable.Contains('ě') || syllable.Contains('ǐ') ||
                 syllable.Contains('ǒ') || syllable.Contains('ǔ') || syllable.Contains('ǚ') ||
                 syllable.Contains('ň'))
        {
            toneNumber = 3;
        }
        else if (syllable.Contains('à') || syllable.Contains('è') || syllable.Contains('ì') ||
                 syllable.Contains('ò') || syllable.Contains('ù') || syllable.Contains('ǜ') ||
                 syllable.Contains('ǹ') || syllable.Contains("m\u0300"))
        {
            toneNumber = 4;
        }

        // 移除声调并附加数字
        var result = RemoveToneMarks(syllable);

        if (toneNumber > 0)
        {
            result += toneNumber;
        }

        return result;
    }

    /// <summary>
    /// 判断是否为轻声音节
    /// </summary>
    private static bool IsNeutralTone(string syllable)
    {
        // 检查常见的轻声词
        var lower = syllable.ToLowerInvariant();
        if (lower is "de" or "le" or "me" or "ne" or "ge" or "zi" or "zhe" or "ma")
        {
            return true;
        }

        // 检查是否不包含任何带声调的字符
        return !ContainsToneMark(syllable);
    }

    /// <summary>
    /// 检查字符串是否包含声调标记
    /// </summary>
    private static bool ContainsToneMark(string text)
    {
        return text.Contains('ā') || text.Contains('á') || text.Contains('ǎ') || text.Contains('à') ||
               text.Contains('ē') || text.Contains('é') || text.Contains('ě') || text.Contains('è') ||
               text.Contains('ī') || text.Contains('í') || text.Contains('ǐ') || text.Contains('ì') ||
               text.Contains('ō') || text.Contains('ó') || text.Contains('ǒ') || text.Contains('ò') ||
               text.Contains('ū') || text.Contains('ú') || text.Contains('ǔ') || text.Contains('ù') ||
               text.Contains('ǖ') || text.Contains('ǘ') || text.Contains('ǚ') || text.Contains('ǜ') ||
               text.Contains('ń') || text.Contains('ň') || text.Contains('ǹ') ||
               text.Contains('ḿ') || text.Contains("m\u0300");
    }

    /// <summary>
    /// 将拼音首字母大写
    /// </summary>
    public static string CapitalizePinyin(string pinyin)
    {
        if (string.IsNullOrEmpty(pinyin))
            return string.Empty;

        if (!pinyin.Contains(' ')) return CapitalizeSyllable(pinyin);
        var parts = pinyin.Split(' ');
        for (var i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]))
            {
                parts[i] = CapitalizeSyllable(parts[i]);
            }
        }
        return string.Join(" ", parts);

    }

    /// <summary>
    /// 将单个拼音音节首字母大写
    /// </summary>
    private static string CapitalizeSyllable(string syllable)
    {
        if (string.IsNullOrEmpty(syllable))
            return string.Empty;

        if (syllable.Length == 1)
            return syllable.ToUpper();

        return char.ToUpper(syllable[0]) + syllable[1..];
    }

    /// <summary>
    /// 从数字声调格式转换为带声调拼音
    /// </summary>
    public static string ConvertToneNumberToMark(string pinyin)
    {
        if (string.IsNullOrEmpty(pinyin))
            return string.Empty;

        // 处理多音节拼音
        if (!pinyin.Contains(' ')) return ConvertSyllableToneNumberToMark(pinyin);
        var parts = pinyin.Split(' ');
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = ConvertSyllableToneNumberToMark(parts[i]);
        }
        return string.Join(" ", parts);

    }

    /// <summary>
    /// 将单个数字声调音节转换为带声调拼音
    /// </summary>
    private static string ConvertSyllableToneNumberToMark(string syllable)
    {
        if (string.IsNullOrEmpty(syllable))
            return string.Empty;

        // 查找声调数字
        var match = MyRegex3().Match(syllable);
        if (!match.Success)
            return syllable;

        var word = match.Groups[1].Value;
        var tone = int.Parse(match.Groups[2].Value);

        // 轻声无需处理音调符号
        return tone == 0 ? word : AddToneMarkToSyllable(word, tone);
    }

    /// <summary>
    /// 在音节上添加声调标记
    /// </summary>
    private static string AddToneMarkToSyllable(string syllable, int toneNumber)
    {
        if (string.IsNullOrEmpty(syllable) || toneNumber < 1 || toneNumber > 4)
            return syllable;

        // 对于拼音 a, o, e, i, u, ü, 声调标记优先级
        const string vowels = "aoeiu\u00FC"; // ü
        foreach (var vowel in vowels)
        {
            var index = syllable.IndexOf(vowel);
            if (index < 0) continue;
            var tonedVowel = GetTonedVowel(vowel, toneNumber);
            return syllable[..index] + tonedVowel + syllable[(index + 1)..];
        }

        return syllable;
    }

    /// <summary>
    /// 获取带有声调的元音字母
    /// </summary>
    private static char GetTonedVowel(char vowel, int tone)
    {
        return vowel switch
        {
            'a' => tone switch
            {
                1 => 'ā',
                2 => 'á',
                3 => 'ǎ',
                _ => 'à'
            },
            'e' => tone switch
            {
                1 => 'ē',
                2 => 'é',
                3 => 'ě',
                _ => 'è'
            },
            'i' => tone switch
            {
                1 => 'ī',
                2 => 'í',
                3 => 'ǐ',
                _ => 'ì'
            },
            'o' => tone switch
            {
                1 => 'ō',
                2 => 'ó',
                3 => 'ǒ',
                _ => 'ò'
            },
            'u' => tone switch
            {
                1 => 'ū',
                2 => 'ú',
                3 => 'ǔ',
                _ => 'ù'
            },
            'ü' => tone switch
            {
                1 => 'ǖ',
                2 => 'ǘ',
                3 => 'ǚ',
                _ => 'ǜ'
            },
            _ => vowel
        };
    }

    /// <summary>
    /// 将拼音转换为指定格式
    /// </summary>
    public static string ConvertFormat(string pinyin, PinyinFormat targetFormat)
    {
        if (string.IsNullOrEmpty(pinyin))
            return string.Empty;

        // 首先确定当前格式
        var currentFormat = DetectPinyinFormat(pinyin);
        if (currentFormat == targetFormat)
            return pinyin; // 已经是目标格式

        // 先转换为带声调的格式
        string withToneMark = null;
        switch (currentFormat)
        {
            case PinyinFormat.WithToneNumber:
                withToneMark = ConvertToneNumberToMark(pinyin);
                break;
            case PinyinFormat.WithoutTone:
                return pinyin; // 从无调格式无法恢复声调，直接返回
            case PinyinFormat.WithToneMark:
            case PinyinFormat.FirstLetter:
                break;
            default:
                withToneMark = pinyin;
                break;
        }

        // 然后转换为目标格式
        switch (targetFormat)
        {
            case PinyinFormat.WithToneMark:
                return withToneMark;
            case PinyinFormat.WithoutTone:
                return RemoveToneMarks(withToneMark);
            case PinyinFormat.WithToneNumber:
                return ConvertToToneNumber(withToneMark);
            case PinyinFormat.FirstLetter:
                // 获取每个音节的首字母
                var withoutTone = RemoveToneMarks(withToneMark);
                var parts = withoutTone.Split(' ');
                for (var i = 0; i < parts.Length; i++)
                {
                    if (!string.IsNullOrEmpty(parts[i]))
                        parts[i] = parts[i][..1];
                }
                return string.Join(" ", parts);
            default:
                return pinyin;
        }
    }

    /// <summary>
    /// 检测拼音格式
    /// </summary>
    private static PinyinFormat DetectPinyinFormat(string pinyin)
    {
        if (string.IsNullOrEmpty(pinyin))
            return PinyinFormat.WithoutTone;

        // 检查是否为首字母格式
        if (MyRegex1().IsMatch(pinyin))
            return PinyinFormat.FirstLetter;

        // 检查是否包含数字声调
        if (MyRegex2().IsMatch(pinyin))
            return PinyinFormat.WithToneNumber;

        // 检查是否包含声调标记
        return ContainsToneMark(pinyin) ? PinyinFormat.WithToneMark : PinyinFormat.WithoutTone;
    }

    [GeneratedRegex("[a-z]+[0-4]")]
    private static partial Regex MyRegex2();
    [GeneratedRegex(@"^[a-z](\s[a-z])*$")]
    private static partial Regex MyRegex1();
    [GeneratedRegex("([a-zA-ZüÜ]+)([0-4])")]
    private static partial Regex MyRegex3();
}