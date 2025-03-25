namespace Crawlers.Models;

public class ChineseMedicine
{
    public int Id { get; set; }  // LiteDB主键

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string PinYin { get; set; } = string.Empty;
    public string FirstLetter { get; set; } = string.Empty;
    public List<MedicineDataSource> DataSources { get; set; } = [];

    // 索引用属性（合并所有数据源的主要信息）
    public string AllAliases { get; set; } = string.Empty;
    public string AllEffects { get; set; } = string.Empty;
    public string AllIndications { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 更新索引属性
    public void UpdateIndexProperties()
    {
        var aliases = new List<string>();
        var effects = new List<string>();
        var indications = new List<string>();

        foreach (var source in DataSources)
        {
            if (!string.IsNullOrWhiteSpace(source.Aliases))
                aliases.Add(source.Aliases);

            if (!string.IsNullOrWhiteSpace(source.Effects))
                effects.Add(source.Effects);

            if (!string.IsNullOrWhiteSpace(source.Indications))
                indications.Add(source.Indications);
        }

        AllAliases = string.Join("; ", aliases.Distinct());
        AllEffects = string.Join("; ", effects.Distinct());
        AllIndications = string.Join("; ", indications.Distinct());
    }
}