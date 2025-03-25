namespace Crawlers.Models;

/// <summary>
/// 中医词典条目
/// </summary>
public class DictionaryEntry
{
    public int Id { get; set; }

    public string Term { get; set; } = string.Empty;    // 词条名称
    public string PinYin { get; set; } = string.Empty;  // 拼音
    public string FirstLetter { get; set; } = string.Empty;  // 首字母
    public string Category { get; set; } = string.Empty; // 分类(基础理论、诊断、方剂等)
    public string Url { get; set; } = string.Empty;      // 来源URL

    public List<DictionaryContent> Contents { get; set; } = [];

    public string SearchText { get; set; } = string.Empty; // 用于全文搜索的文本

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 生成用于搜索的文本
    public void UpdateSearchText()
    {
        var textParts = new List<string> { Term, PinYin };

        foreach (var content in Contents)
        {
            textParts.Add(content.Definition);

            if (!string.IsNullOrEmpty(content.Reference))
                textParts.Add(content.Reference);
        }

        SearchText = string.Join(" ", textParts.Where(t => !string.IsNullOrEmpty(t)));
    }
}

/// <summary>
/// 词条内容(可能有多个来源的解释)
/// </summary>
public class DictionaryContent
{
    public string Source { get; set; } = string.Empty;      // 来源(如《中医词典》、《中医大辞典》等)
    public string Definition { get; set; } = string.Empty;  // 定义/解释
    public string Reference { get; set; } = string.Empty;   // 参考文献
    public List<string> RelatedTerms { get; set; } = []; // 相关词条
}