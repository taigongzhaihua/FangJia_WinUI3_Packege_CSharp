using System.Collections.ObjectModel;

namespace FangJia.Common;

public class CategoryBase;

public class Category : CategoryBase
{
    public string? Name { get; set; }
    public string? Tooltip { get; set; }
    public string? Glyph { get; set; }
    public string? Path { get; set; }
    public ObservableCollection<CategoryBase>? Children { get; set; }
}

public class Header : CategoryBase
{
    public string? Name { get; set; }
}

public class Separator : CategoryBase;

