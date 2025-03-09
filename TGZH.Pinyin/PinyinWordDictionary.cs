using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace TGZH.Pinyin;

/// <summary>
/// 拼音词典，用于处理词语拼音
/// </summary>
/// <remarks>
/// 创建拼音词典实例
/// </remarks>
/// <param name="database">拼音数据库</param>
[SuppressMessage("ReSharper", "StringLiteralTypo")]
internal class PinyinWordDictionary(PinyinDatabase database)
{
    private readonly PinyinDatabase _database = database ?? throw new ArgumentNullException(nameof(database));
    private readonly Dictionary<string, Dictionary<PinyinFormat, string>> _wordCache = [];
    private bool _isInitialized;

    /// <summary>
    /// 初始化词典
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        // 预加载常用词语
        await PreloadCommonWordsAsync();

        _isInitialized = true;
    }

    /// <summary>
    /// 获取词语拼音
    /// </summary>
    public async Task<string> GetWordPinyinAsync(string word, PinyinFormat format, string separator = " ")
    {
        if (string.IsNullOrEmpty(word) || word.Length <= 1)
            return null;

        // 检查缓存
        if (_wordCache.TryGetValue(word, out var formatDict) && formatDict.TryGetValue(format, out var cachedPinyin))
        {
            return cachedPinyin;
        }

        // 从数据库查询
        var pinyin = await _database.GetWordPinyinAsync(word, format);

        if (!string.IsNullOrEmpty(pinyin))
        {
            // 更新缓存
            CacheWordPinyin(word, format, pinyin);
            return pinyin;
        }

        // 数据库中没有此词语，尝试动态组合
        var generatedPinyin = await GenerateWordPinyinAsync(word, format, separator);
        if (!string.IsNullOrEmpty(generatedPinyin))
        {
            // 添加到数据库
            await AddWordToDatabaseAsync(word, generatedPinyin, format);

            // 更新缓存
            CacheWordPinyin(word, format, generatedPinyin);

            return generatedPinyin;
        }

        return null;
    }

    /// <summary>
    /// 将词语拼音缓存
    /// </summary>
    private void CacheWordPinyin(string word, PinyinFormat format, string pinyin)
    {
        if (!_wordCache.TryGetValue(word, out var formatDict))
        {
            formatDict = [];
            _wordCache[word] = formatDict;
        }

        formatDict[format] = pinyin;
    }

    /// <summary>
    /// 动态生成词语拼音
    /// </summary>
    private async Task<string> GenerateWordPinyinAsync(string word, PinyinFormat format, string separator)
    {
        if (string.IsNullOrEmpty(word))
            return null;

        var result = new StringBuilder();

        // 先获取每个字的拼音
        foreach (var c in word)
        {
            if (PinyinHelper.IsChinese(c))
            {
                var pinyins = await _database.GetCharPinyinAsync(c, format);

                if (pinyins is not { Length: > 0 }) continue;
                if (result.Length > 0) result.Append(separator);
                result.Append(pinyins[0]); // 使用第一个拼音
            }
            else
            {
                if (result.Length > 0) result.Append(separator);
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 将词语添加到数据库
    /// </summary>
    private async Task AddWordToDatabaseAsync(string word, string pinyin, PinyinFormat format)
    {
        try
        {
            // 转换为带声调格式
            string withToneMark;

            switch (format)
            {
                case PinyinFormat.WithToneMark:
                    withToneMark = pinyin;
                    break;
                case PinyinFormat.WithToneNumber:
                    withToneMark = PinyinConverter.ConvertToneNumberToMark(pinyin);
                    break;
                default:
                    // 对于无调或首字母格式，无法恢复声调，不保存
                    return;
            }

            // 计算其他格式的拼音
            var withoutTone = PinyinConverter.RemoveToneMarks(withToneMark);
            var withToneNumber = PinyinConverter.ConvertToToneNumber(withToneMark);

            // 计算首字母
            var sb = new StringBuilder();
            foreach (var part in withoutTone.Split(' '))
            {
                if (!string.IsNullOrEmpty(part))
                    sb.Append(part[0]).Append(' ');
            }
            var firstLetter = sb.ToString().Trim();

            // 保存到数据库
            await _database.AddOrUpdateWordAsync(word, withToneMark, withoutTone, withToneNumber, firstLetter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"添加词语到数据库失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 预加载常用词语
    /// </summary>
    private async Task PreloadCommonWordsAsync()
    {
        // 常用中文词语列表
        var commonWords = new List<(string word, string pinyin)>
        {
            ("中国", "zhōng guó"),
            ("北京", "běi jīng"),
            ("上海", "shàng hǎi"),
            ("你好", "nǐ hǎo"),
            ("谢谢", "xiè xie"),
            ("再见", "zài jiàn"),
            ("学生", "xué sheng"),
            ("老师", "lǎo shī"),
            ("朋友", "péng you"),
            ("电脑", "diàn nǎo"),
            ("手机", "shǒu jī"),
            ("时间", "shí jiān"),
            ("银行", "yín háng"),
            ("医院", "yī yuàn"),
            ("学校", "xué xiào"),
            ("公司", "gōng sī"),
            ("工作", "gōng zuò"),
            ("家庭", "jiā tíng"),
            ("餐厅", "cān tīng"),
            ("商店", "shāng diàn"),
            ("图书馆", "tú shū guǎn"),
            ("火车站", "huǒ chē zhàn"),
            ("飞机场", "fēi jī chǎng"),
            ("地铁站", "dì tiě zhàn"),
            ("汽车站", "qì chē zhàn"),
            // 多音字组成的词语
            ("重庆", "chóng qìng"),
            ("长春", "cháng chūn"),
            ("西安", "xī ān"),
            ("行程", "xíng chéng"),
            ("大夫", "dài fu"),
            ("都市", "dū shì"),
            ("银行", "yín háng"),
            ("一天", "yì tiān"),
            ("一个", "yí gè")
        };

        // 批量添加到数据库
        foreach (var (word, pinyin) in commonWords)
        {
            try
            {
                // 生成其他格式
                var withoutTone = PinyinConverter.RemoveToneMarks(pinyin);
                var withToneNumber = PinyinConverter.ConvertToToneNumber(pinyin);

                // 计算首字母
                var sb = new StringBuilder();
                foreach (var part in withoutTone.Split(' '))
                {
                    if (!string.IsNullOrEmpty(part))
                        sb.Append(part[0]).Append(' ');
                }
                var firstLetter = sb.ToString().Trim();

                // 保存到数据库
                await _database.AddOrUpdateWordAsync(word, pinyin, withoutTone, withToneNumber, firstLetter, 1000);

                // 添加到缓存
                if (!_wordCache.TryGetValue(word, out var formatDict))
                {
                    formatDict = [];
                    _wordCache[word] = formatDict;
                }

                formatDict[PinyinFormat.WithToneMark] = pinyin;
                formatDict[PinyinFormat.WithoutTone] = withoutTone;
                formatDict[PinyinFormat.WithToneNumber] = withToneNumber;
                formatDict[PinyinFormat.FirstLetter] = firstLetter;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"预加载词语 {word} 失败: {ex.Message}");
            }
        }
    }
}