
//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace FangJia.Common;
[ContentProperty(Name = "ItemTemplate")]
public partial class ItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ItemTemplate { get; set; }
    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return ItemTemplate;
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return ItemTemplate; ;
    }
}