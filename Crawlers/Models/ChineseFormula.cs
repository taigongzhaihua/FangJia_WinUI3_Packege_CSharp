namespace Crawlers.Models;

public class ChineseFormula
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string PinYin { get; set; } = string.Empty;
    public string FirstLetter { get; set; } = string.Empty;
    public List<FormulaDataSource> DataSources { get; set; } = [];

    // 索引用属性（合并所有数据源的主要信息）
    public string AllAliases { get; set; } = string.Empty;
    public string AllComposition { get; set; } = string.Empty;
    public string AllIndications { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 更新索引属性
    public void UpdateIndexProperties()
    {
        var aliases = new List<string>();
        var composition = new List<string>();
        var indications = new List<string>();

        foreach (var source in DataSources)
        {
            if (!string.IsNullOrWhiteSpace(source.Aliases))
                aliases.Add(source.Aliases);

            if (!string.IsNullOrWhiteSpace(source.Composition))
                composition.Add(source.Composition);

            if (!string.IsNullOrWhiteSpace(source.Indications))
                indications.Add(source.Indications);
        }

        AllAliases = string.Join("; ", aliases.Distinct());
        AllComposition = string.Join("; ", composition.Distinct());
        AllIndications = string.Join("; ", indications.Distinct());
    }
}

public class FormulaDataSource
{
    public string Title { get; set; } = string.Empty;
    public string Aliases { get; set; } = string.Empty;  // 别名
    public string Source { get; set; } = string.Empty;   // 出处
    public string Composition { get; set; } = string.Empty;  // 组成
    public string Preparation { get; set; } = string.Empty;  // 制法
    public string Indications { get; set; } = string.Empty;  // 主治
    public string Usage { get; set; } = string.Empty;    // 用法用量
    public string Theory { get; set; } = string.Empty;   // 方解
    public string Application { get; set; } = string.Empty; // 临床应用
    public List<FormulaItem> Items { get; set; } = [];
}

public class FormulaItem
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsImportant { get; set; } = false;
}