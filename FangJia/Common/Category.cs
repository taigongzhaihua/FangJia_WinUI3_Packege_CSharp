//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

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

