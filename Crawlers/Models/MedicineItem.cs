namespace Crawlers.Models;

public class MedicineItem
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsImportant { get; set; } = false;
}