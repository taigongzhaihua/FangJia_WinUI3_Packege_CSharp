using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using System.Diagnostics.CodeAnalysis;

namespace FangJia.Common;

[ContentProperty(Name = "ItemTemplate")]
[SuppressMessage("ReSharper", "PartialTypeWithSinglePart")]
public partial class MenuItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return item switch
        {
            Separator => SeparatorTemplate,
            Header => HeaderTemplate,
            _ => ItemTemplate
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            Separator => SeparatorTemplate,
            Header => HeaderTemplate,
            _ => ItemTemplate
        };
    }
#if WINDOWS10_0_17763_0_OR_GREATER
    internal DataTemplate? HeaderTemplate = (DataTemplate)XamlReader.Load(
        """
        <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                           <NavigationViewItemHeader Content='{Binding Name}' />
                          </DataTemplate>
        """);

    internal DataTemplate? SeparatorTemplate = (DataTemplate)XamlReader.Load(
        """
        <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                            <NavigationViewItemSeparator />
                          </DataTemplate>
        """);
#endif
}