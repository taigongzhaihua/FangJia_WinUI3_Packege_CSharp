namespace Crawlers.Models;

public class MedicineDataSource
{
    public string Title { get; set; } = string.Empty;
    public string Aliases { get; set; } = string.Empty;  // 别名
    public string Source { get; set; } = string.Empty;   // 来源
    public string Properties { get; set; } = string.Empty;  // 性味
    public string Meridian { get; set; } = string.Empty;   // 归经
    public string Effects { get; set; } = string.Empty;   // 功效
    public string Indications { get; set; } = string.Empty;  // 主治
    public string Usage { get; set; } = string.Empty;    // 用法用量
    public List<MedicineItem> Items { get; set; } = [];
}