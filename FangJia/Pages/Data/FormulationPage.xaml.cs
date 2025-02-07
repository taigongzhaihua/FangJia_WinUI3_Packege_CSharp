// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using FangJia.Common;
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FangJia.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FormulationPage
    {
        internal readonly FormulationViewModel ViewModel = Locator.GetService<FormulationViewModel>();
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public FormulationPage()
        {
            InitializeComponent();
            Task.Run(() => ViewModel.LoadCategoriesAsync(_dispatcherQueue));
        }

        private void TreeView_OnSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            if (args.AddedItems.FirstOrDefault() is not FormulationCategory selectedCategory) return;
            // 递归展开选中的项
            ExpandTreeViewItem(selectedCategory);

            // 递归收起未被选中的项
            CollapseUnselectedItems(FormulationCategoryTree.RootNodes, selectedCategory);
        }

        /// <summary>
        /// 递归查找 `TreeViewNode` 并展开匹配的项
        /// </summary>
        private void ExpandTreeViewItem(FormulationCategory selectedCategory)
        {
            foreach (var node in FormulationCategoryTree.RootNodes)
            {
                if (ExpandIfMatch(node, selectedCategory))
                    break;
            }
        }

        /// <summary>
        /// 递归展开匹配的 `TreeViewNode`
        /// </summary>
        /// <param name="node"></param>
        /// <param name="targetCategory"></param>
        /// <returns></returns>
        private static bool ExpandIfMatch(TreeViewNode node, FormulationCategory targetCategory)
        {
            if (node.Content is FormulationCategory category && category == targetCategory)
            {
                node.IsExpanded = true;
                return true;
            }

            if (!node.Children.Any(child => ExpandIfMatch(child, targetCategory))) return false;
            node.IsExpanded = true; // 递归展开父级
            return true;

        }

        /// <summary>
        /// 递归收起未被选中的 `TreeViewNode`
        /// </summary>
        private static void CollapseUnselectedItems(IList<TreeViewNode> nodes, FormulationCategory selectedCategory)
        {
            foreach (var node in nodes)
            {
                if (node.Content is not FormulationCategory) continue;
                // 如果当前项不是选中项，且它的子项中不包含选中项，则收起
                if (!ContainsSelectedItem(node, selectedCategory))
                {
                    node.IsExpanded = false;
                }

                // 递归处理子项
                CollapseUnselectedItems(node.Children, selectedCategory);
            }
        }

        /// <summary>
        /// 检查 `TreeViewNode` 是否包含选中的子项
        /// </summary>
        private static bool ContainsSelectedItem(TreeViewNode node, FormulationCategory selectedCategory)
        {
            if (node.Content is FormulationCategory category && category == selectedCategory)
            {
                return true;
            }

            return node.Children.Any(child => ContainsSelectedItem(child, selectedCategory));
        }

        private void SearchBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // 只在用户输入时更新建议（忽略程序设置文本的情况）
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var query = sender.Text.Trim();

            // 如果输入为空，则清空建议列表
            if (string.IsNullOrEmpty(query))
            {
                sender.ItemsSource = null;
            }
            else
            {
                // 根据输入关键字过滤数据（不区分大小写的匹配）
                var suggestions = ViewModel.SearchWords
                    .Where(item => item.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();

                sender.ItemsSource = suggestions;
            }
        }


        private void SearchBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is string selectedItem)
            {
                sender.Text = selectedItem;
            }
        }

        private async void SearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            try
            {
                var query = args.ChosenSuggestion as string ?? args.QueryText;
                if (string.IsNullOrEmpty(query)) return;

                // 精确匹配优先，再找包含项
                var targetNode = ViewModel.SearchDictionary.GetValueOrDefault(query) ??
                                 ViewModel.SearchDictionary.Values.FirstOrDefault(f =>
                                     f.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));

                if (targetNode == null) return;
                {
                    foreach (var node in FormulationCategoryTree.RootNodes)
                    {
                        if (node.Content is not FormulationCategory c) continue;
                        foreach (var child in c.Children)
                        {
                            foreach (var f in child.Children)
                            {
                                if (f.Name != targetNode.Name) continue;
                                ExpandTreeViewItem(c);
                                await Task.Delay(50);
                                ExpandTreeViewItem(child);
                                await Task.Delay(50);
                                ViewModel.SelectedCategory = f;
                                // 递归收起未被选中的项
                                CollapseUnselectedItems(FormulationCategoryTree.RootNodes, f);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"搜索时出错：{e.Message}", e);
            }
        }
    }
}
