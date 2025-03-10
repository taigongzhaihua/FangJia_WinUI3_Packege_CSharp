using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace TGZH.Pinyin;

/// <summary>
/// 拼音转换辅助类
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class PinyinHelper
{
    private static PinyinManager _manager;
    private static readonly object LockObj = new();

    /// <summary>
    /// 初始化拼音库
    /// </summary>
    /// <param name="options">初始化选项</param>
    public static async Task InitializeAsync(PinyinLibraryOptions options = null)
    {
        if (_manager != null)
            return;

        lock (LockObj)
        {
            if (_manager != null)
                return;

            _manager = new PinyinManager();
        }

        await _manager.InitializeAsync(options ?? new PinyinLibraryOptions());
    }

    /// <summary>
    /// 获取单个汉字的拼音
    /// </summary>
    /// <param name="c">汉字</param>
    /// <param name="format">拼音格式</param>
    /// <returns>拼音数组（多音字可能有多个读音）</returns>
    public static async Task<string[]> GetAsync(char c, PinyinFormat format = PinyinFormat.WithToneMark)
    {
        EnsureInitialized();
        return await _manager.GetCharPinyinAsync(c, format);
    }


    /// <summary>
    /// 获取文本的拼音
    /// </summary>
    /// <param name="text">中文文本</param>
    /// <param name="format">拼音格式</param>
    /// <param name="separator">拼音之间的分隔符</param>
    /// <returns>拼音字符串</returns>
    public static async Task<string> GetAsync(string text, PinyinFormat format = PinyinFormat.WithToneMark, string separator = " ")
    {
        EnsureInitialized();
        return await _manager.GetTextPinyinAsync(text, format, separator);
    }

    /// <summary>
    /// 获取中文文本的首字母
    /// </summary>
    /// <param name="text">中文文本</param>
    /// <param name="separator">分隔符</param>
    /// <returns>拼音首字母</returns>
    public static async Task<string> GetFirstLettersAsync(string text, string separator = "")
    {
        EnsureInitialized();
        return await _manager.GetTextPinyinAsync(text, PinyinFormat.FirstLetter, separator);
    }

    /// <summary>
    /// 获取文本的拼音（同步方法，需要先初始化）
    /// </summary>
    /// <param name="text">中文文本</param>
    /// <param name="format">拼音格式</param>
    /// <param name="separator">拼音之间的分隔符</param>
    /// <returns>拼音字符串</returns>
    public static string Get(string text, PinyinFormat format = PinyinFormat.WithToneMark, string separator = " ")
    {
        EnsureInitialized();
        return _manager.GetTextPinyinSync(text, format, separator);
    }

    /// <summary>
    /// 获取中文文本的首字母（同步方法）
    /// </summary>
    /// <param name="text">中文文本</param>
    /// <param name="separator">分隔符</param>
    /// <returns>拼音首字母</returns>
    public static string GetFirstLetters(string text, string separator = "")
    {
        EnsureInitialized();
        return _manager.GetTextPinyinSync(text, PinyinFormat.FirstLetter, separator);
    }

    /// <summary>
    /// 批量获取文本拼音（高性能优化版本）
    /// </summary>
    public static async Task<Dictionary<string, string>> GetBatchAsync(string[] texts,
        PinyinFormat format = PinyinFormat.WithToneMark, string separator = " ")
    {
        EnsureInitialized();
        return await _manager.GetTextPinyinBatchAsync(texts, format, separator);
    }

    /// <summary>
    /// 批量获取文本的首字母（高性能优化版本）
    /// </summary>
    public static async Task<Dictionary<string, string>> GetFirstLettersBatchAsync(string[] texts, string separator = "")
    {
        EnsureInitialized();
        return await _manager.GetTextPinyinBatchAsync(texts, PinyinFormat.FirstLetter, separator);
    }

    /// <summary>
    /// 同步方法 - 批量获取文本拼音
    /// </summary>
    public static Dictionary<string, string> GetBatch(string[] texts,
        PinyinFormat format = PinyinFormat.WithToneMark, string separator = " ")
    {
        EnsureInitialized();
        return _manager.GetTextPinyinBatchSync(texts, format, separator);
    }

    /// <summary>
    /// 流式获取拼音 - 处理大量文本时可逐步返回结果
    /// </summary>
    public static async IAsyncEnumerable<KeyValuePair<string, string>> GetStreamingAsync(
        IEnumerable<string> texts,
        PinyinFormat format = PinyinFormat.WithToneMark,
        string separator = " ")
    {
        EnsureInitialized();
        await foreach (var result in _manager.GetTextPinyinStreamingAsync(texts, format, separator))
        {
            yield return result;
        }
    }

    /// <summary>
    /// 流式获取文本首字母 - 处理大量文本时可逐步返回结果
    /// </summary>
    public static async IAsyncEnumerable<KeyValuePair<string, string>> GetFirstLettersStreamingAsync(
        IEnumerable<string> texts,
        string separator = "")
    {
        EnsureInitialized();
        await foreach (var result in _manager.GetTextPinyinStreamingAsync(texts, PinyinFormat.FirstLetter, separator))
        {
            yield return result;
        }
    }

    /// <summary>
    /// 流式处理单个大文本，分段返回结果
    /// </summary>
    public static async IAsyncEnumerable<string> GetTextChunkStreamingAsync(
        string text,
        PinyinFormat format = PinyinFormat.WithToneMark,
        string separator = " ",
        int chunkSize = 100)
    {
        EnsureInitialized();
        await foreach (var chunk in _manager.GetTextChunkStreamingAsync(text, format, separator, chunkSize))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// 判断字符是否是中文
    /// </summary>
    public static bool IsChinese(char c)
    {
        // 基本汉字范围 (CJK Unified Ideographs)
        return c >= 0x4E00 && c <= 0x9FFF;
    }

    /// <summary>
    /// 获取拼音的首字母
    /// </summary>
    public static string GetInitial(string pinyin)
    {
        return string.IsNullOrEmpty(pinyin) ? string.Empty : pinyin[..1].ToLower();
    }

    /// <summary>
    /// 检查拼音库是否已初始化
    /// </summary>
    private static void EnsureInitialized()
    {
        if (_manager is not { IsInitialized: true })
        {
            InitializeAsync().Wait();
        }
    }
}