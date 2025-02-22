using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace FangJia.Common;
[ContentProperty(Name = "ItemTemplate")]
public partial class FormulationCategoryItemTemplateSelector : DataTemplateSelector
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