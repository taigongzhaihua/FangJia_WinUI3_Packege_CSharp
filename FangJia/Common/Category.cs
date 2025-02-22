using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace FangJia.Common;
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class CategoryBase; [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class Category : CategoryBase
{
    public string? Name { get; set; }
    public string? Tooltip { get; set; }
    public string? Glyph { get; set; }
    public string? Path { get; set; }
    public ObservableCollection<CategoryBase>? Children { get; set; }
    public override string ToString()
    {
        return Name ?? string.Empty;
    }
}
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class Header : CategoryBase
{
    public string? Name { get; set; }
}
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public class Separator : CategoryBase;

