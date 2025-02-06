// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using FangJia.Common;
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace FangJia.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FormulationPage
    {
        internal readonly FormulationViewModel ViewModel = Locator.GetService<FormulationViewModel>();
        private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        public FormulationPage()
        {
            InitializeComponent();
            ViewModel.LoadCategoriesAsync(_dispatcherQueue);
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
                if (node.Content is not FormulationCategory category) continue;
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
    }
}
