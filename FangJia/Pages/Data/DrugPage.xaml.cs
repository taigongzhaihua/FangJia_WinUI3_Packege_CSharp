//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Media;
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FangJia.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DrugPage
{
    internal readonly DrugViewModel ViewModel = Locator.GetService<DrugViewModel>();


    public DrugPage()
    {
        InitializeComponent();
    }

    private void SearchBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        if (sender is { Text: { Length: > 0 } text })
        {
            ViewModel.SearchDrugSummaries(text);
        }
        else
        {
            ViewModel.ClearSearch();
        }
    }

    // 选择建议时
    private static void SearchBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string selectedItem)
        {
            sender.Text = selectedItem;
        }
    }

    // 提交查询时
    private void SearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = args.ChosenSuggestion as string ?? args.QueryText;
        if (string.IsNullOrEmpty(query)) return;
        ViewModel.SelectDrug(query);
    }

    private void PaneOpenOrCloseButton_OnClick(object _, RoutedEventArgs _1)
    {
        SplitView.IsPaneOpen = !SplitView.IsPaneOpen;
    }

    private void OnAdaptiveStatesCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
    {
        switch (e.NewState.Name)
        {
            case "WideState":
                Effects.SetShadow(Border, null!);
                break;
            case "NarrowState":
                Effects.SetShadow(Border, new AttachedCardShadow
                {
                    Opacity = 0.4,
                    BlurRadius = 8,
                    Offset = "1"
                });
                break;
        }
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && sender is ListView listView)
        {
            listView.ScrollIntoView(e.AddedItems[0]);
        }
    }
}