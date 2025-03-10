using System;

namespace TGZH.Pinyin;

/// <summary>
/// 拼音格式
/// </summary>
public enum PinyinFormat
{
    /// <summary>
    /// 带声调，如: zhōng guó
    /// </summary>
    WithToneMark,

    /// <summary>
    /// 不带声调，如: zhong guo
    /// </summary>
    WithoutTone,

    /// <summary>
    /// 带数字声调，如: zhong1 guo2
    /// </summary>
    WithToneNumber,

    /// <summary>
    /// 仅首字母，如: z g
    /// </summary>
    FirstLetter
}

/// <summary>
/// 拼音库初始化选项
/// </summary>
public class PinyinLibraryOptions
{
    /// <summary>
    /// 数据库文件路径，为空时使用默认路径
    /// </summary>
    public string DatabasePath { get; set; }

    /// <summary>
    /// 是否预加载常用汉字拼音到内存，提高查询速度
    /// </summary>
    public bool PreloadCommonChars { get; set; } = true;

    /// <summary>
    /// 是否优先使用词语拼音（多音字处理）
    /// </summary>
    public bool PrioritizeWordPinyin { get; set; } = true;

    /// <summary>
    /// 最大词语长度（用于词语拼音识别）
    /// </summary>
    public int MaxWordLength { get; set; } = 8;

    /// <summary>
    /// 数据库更新URL（可选）
    /// </summary>
    public string DataUpdateUrl { get; set; }

    /// <summary>
    /// 并行处理时的最大线程数，默认为处理器核心数
    /// </summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 批量查询时单次数据库操作的最大项数
    /// </summary>
    public int BatchQuerySize { get; set; } = 500;

    /// <summary>
    /// 是否对批量查询结果进行额外缓存
    /// </summary>
    public bool EnableBatchResultCache { get; set; } = true;

    /// <summary>
    /// 流式处理时的块大小
    /// </summary>
    public int StreamingChunkSize { get; set; } = 100;

    /// <summary>
    /// 流式处理时是否预加载字符
    /// </summary>
    public bool PreloadCharsForStreaming { get; set; } = true;

    /// <summary>
    /// 流式处理的缓冲区大小
    /// </summary>
    public int StreamingBufferSize { get; set; } = 10;
}