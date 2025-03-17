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
using CommunityToolkit.WinUI.UI.Controls;
using FangJia.Common;
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using NLog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FangJia.Pages;

/// <summary>
/// 方剂页面
/// </summary>
public sealed partial class FormulationPage
{
    internal readonly FormulationViewModel ViewModel = Locator.GetService<FormulationViewModel>();
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


    // 常量定义
    private const string CategoryIdProperty = "CategoryId";

    public FormulationPage()
    {
        InitializeComponent();
        _ = ViewModel.LoadCategoriesAsync(App.MainDispatcherQueue!);
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // 注册事件处理
        ViewModel.SelectedFormulaChanged += ViewModelSelectedFormulaChanged;
        ViewModel.FormulaImageChanged += ViewModelOnFormulaImageChanged;

        // 添加页面卸载时的事件取消订阅
        Unloaded += Page_Unloaded;
    }


    // 在Page_Unloaded方法中移除清除缓存的代码
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // 取消事件订阅，防止内存泄漏
        ViewModel.SelectedFormulaChanged -= ViewModelSelectedFormulaChanged;
        ViewModel.FormulaImageChanged -= ViewModelOnFormulaImageChanged;
        Unloaded -= Page_Unloaded;

        // 移除: _animationCache.Clear();
    }

    #region 动画处理

    /// <summary>
    /// 应用淡入淡出动画到指定元素 - 修复版，避免重用正在运行的Storyboard
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyFadeAnimation(UIElement element, double fromValue, double toValue, double durationSeconds)
    {
        try
        {
            // 每次创建新的动画实例，避免重用正在运行的动画
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(durationSeconds)
            };

            animation.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                Value = fromValue,
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))
            });

            animation.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                Value = toValue,
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(durationSeconds))
            });

            // 每次都创建新的Storyboard
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, "Opacity");

            // 启动动画
            storyboard.Begin();
        }
        catch (Exception ex)
        {
            // 静默处理动画错误，避免影响主要功能
            Logger.Warn($"应用动画时出错: {ex.Message}");

            // 直接设置最终状态
            element.Opacity = toValue;
        }
    }

    /// <summary>
    /// 对多个元素应用淡入淡出动画 - 修复版
    /// </summary>
    private static void ApplyFadeAnimationToElements(double fromValue, double toValue, double durationSeconds, params UIElement[] elements)
    {
        try
        {
            // 为每个元素单独应用动画，避免共享Storyboard
            foreach (var element in elements)
            {
                ApplyFadeAnimation(element, fromValue, toValue, durationSeconds);
            }
        }
        catch (Exception ex)
        {
            // 静默处理动画错误，避免影响主要功能
            Logger.Warn($"应用多元素动画时出错: {ex.Message}");

            // 直接设置最终状态
            foreach (var element in elements)
            {
                element.Opacity = toValue;
            }
        }
    }

    private void ViewModelOnFormulaImageChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyFadeAnimation(Image, 0.0, 1.0, 0.5);
        }
        catch
        {
            // 直接设置最终状态
            Image.Opacity = 1.0;
        }
    }

    private void ViewModelSelectedFormulaChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyFadeAnimation(Viewer, 0.0, 1.0, 0.2);
        }
        catch
        {
            // 直接设置最终状态
            Viewer.Opacity = 1.0;
        }
    }

    #endregion

    #region TreeView 操作

    // 修复TreeView_OnSelectionChanged方法中的并行处理
    private void TreeView_OnSelectionChanged(TreeView _, TreeViewSelectionChangedEventArgs args)
    {
        if (args.AddedItems.FirstOrDefault() is not FormulationCategory selectedCategory) return;

        // 不要使用Task.Run进行这些UI相关操作
        // 直接在UI线程上执行
        ExpandTreeViewItem(selectedCategory);
        CollapseUnselectedItems(FormulationCategoryTree.RootNodes, selectedCategory);

        if (selectedCategory.IsCategory || args.RemovedItems.Count <= 0) return;

        // 创建渐隐动画
        ApplyFadeAnimationToElements(1.0, 0.0, 0.1, Viewer, Image);
    }

    // 修复SelectFormulation方法中的并行处理
    private async Task SelectFormulation(string query)
    {
        // 精确匹配优先，再找包含项
        FormulationCategory? targetNode;

        // 直接查找完全匹配
        if (ViewModel.SearchDictionary.TryGetValue(query, out var exactMatch))
        {
            targetNode = exactMatch;
        }
        else
        {
            // 在UI线程上查找部分匹配，避免跨线程访问
            targetNode = ViewModel.SearchDictionary.Values
                .FirstOrDefault(f => FastContainsIgnoreCase(f.Name, query));
        }

        if (targetNode == null) return;

        // 使用线程安全的方式查找和展开树节点
        var found = false;
        var firstCategory = default(FormulationCategory);
        var secondCategory = default(FormulationCategory);
        var formulation = default(FormulationCategory);

        // 在UI线程上遍历
        foreach (var category in ViewModel.Categories)
        {
            foreach (var child in category.Children)
            {
                foreach (var item in child.Children)
                {
                    if (item.Name != targetNode.Name) continue;

                    firstCategory = category;
                    secondCategory = child;
                    formulation = item;
                    found = true;
                    break;
                }

                if (found) break;
            }

            if (found) break;
        }

        if (!found) return;
        // 都在UI线程上执行，无需延迟
        ExpandTreeViewItem(firstCategory);
        await Task.Delay(20);  // 等待展开动画完成
        ExpandTreeViewItem(secondCategory);
        await Task.Delay(50);  // 等待展开动画完成
        ViewModel.SelectedCategory = formulation;
        CollapseUnselectedItems(FormulationCategoryTree.RootNodes, formulation);
    }

    /// <summary>
    /// 递归查找并展开匹配的项 - 优化版
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ExpandTreeViewItem(FormulationCategory? selectedCategory)
    {
        if (selectedCategory == null) return;

        // 优化：使用栈替代递归，减少内存压力
        Stack<TreeViewNode> nodesToProcess = new(FormulationCategoryTree.RootNodes);

        while (nodesToProcess.Count > 0)
        {
            var node = nodesToProcess.Pop();

            if (node.Content is FormulationCategory category && category == selectedCategory)
            {
                // 找到匹配项，向上展开所有父节点
                ExpandParentNodes(node, FormulationCategoryTree);
                return;
            }

            // 将子节点添加到栈中
            foreach (var child in node.Children)
            {
                nodesToProcess.Push(child);
            }
        }
    }

    /// <summary>
    /// 向上展开所有父节点 - 简化版本
    /// </summary>
    private static void ExpandParentNodes(TreeViewNode node, TreeView treeView)
    {
        // 为目标节点设置展开状态
        node.IsExpanded = true;

        Task.Delay(100);  // 等待展开动画完成
        // 查找目标节点的祖先节点并展开它们
        FindAndExpandAncestors(treeView.RootNodes, node);
    }

    /// <summary>
    /// 递归查找并展开祖先节点
    /// </summary>
    private static bool FindAndExpandAncestors(IList<TreeViewNode> nodes, TreeViewNode targetNode)
    {
        foreach (var node in nodes)
        {
            // 如果当前节点包含目标节点作为直接子节点
            if (node.Children.Contains(targetNode))
            {
                node.IsExpanded = true;
                return true;
            }

            // 递归查找子节点
            if (!FindAndExpandAncestors(node.Children, targetNode)) continue;
            // 如果在子层级找到目标，展开当前节点并返回true
            node.IsExpanded = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 递归收起未被选中的节点 - 优化版
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void CollapseUnselectedItems(IList<TreeViewNode> nodes, FormulationCategory? selectedCategory)
    {
        // 创建一个包含选中项路径的哈希集合，避免重复计算
        var selectedPath = new HashSet<FormulationCategory>();

        // 先找到选中项及其路径
        if (selectedCategory != null)
        {
            FindSelectedPath(nodes, selectedCategory, selectedPath);
        }

        // 然后收起不在路径上的节点
        foreach (var node in nodes)
        {
            if (node.Content is not FormulationCategory category) continue;

            // 如果当前项不在选中路径上，则收起
            if (!selectedPath.Contains(category))
            {
                node.IsExpanded = false;
            }

            // 递归处理子项
            CollapseUnselectedItems(node.Children, selectedCategory);
        }
    }

    /// <summary>
    /// 查找选中项路径
    /// </summary>
    private static bool FindSelectedPath(IList<TreeViewNode> nodes, FormulationCategory selectedCategory,
        HashSet<FormulationCategory> path)
    {
        foreach (var node in nodes)
        {
            if (node.Content is not FormulationCategory category) continue;

            // 找到选中项
            if (category == selectedCategory)
            {
                path.Add(category);
                return true;
            }

            // 检查子项
            if (!FindSelectedPath(node.Children, selectedCategory, path)) continue;
            path.Add(category);  // 添加当前节点到路径
            return true;
        }

        return false;
    }

    /*
        /// <summary>
        /// 检查节点是否包含选中的子项 - 优化版本
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsSelectedItem(TreeViewNode node, FormulationCategory? selectedCategory)
        {
            if (selectedCategory == null) return false;

            if (node.Content is FormulationCategory category && category == selectedCategory)
            {
                return true;
            }

            return node.Children.Any(child => ContainsSelectedItem(child, selectedCategory));
        }
    */

    #endregion

    #region 搜索功能

    /// <summary>
    /// 使用unsafe代码优化字符串搜索，提高性能
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool FastContainsIgnoreCase(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return true;
        if (string.IsNullOrEmpty(haystack)) return false;

        // 简单情况直接用内置方法
        if (needle.Length == 1 || haystack.Length <= 12)
        {
            return haystack.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
        }

        // 使用unsafe代码进行更快的字符串搜索
        fixed (char* pHaystack = haystack)
        fixed (char* pNeedle = needle)
        {
            var haystackLength = haystack.Length;
            var needleLength = needle.Length;

            // 使用Boyer-Moore-Horspool搜索算法的简化版本
            for (var i = 0; i <= haystackLength - needleLength; i++)
            {
                var j = 0;
                for (; j < needleLength; j++)
                {
                    if (char.ToLowerInvariant(pHaystack[i + j]) != char.ToLowerInvariant(pNeedle[j]))
                        break;
                }

                if (j == needleLength)
                    return true;
            }

            return false;
        }
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
            return;
        }

        // 使用ArrayPool提高性能，减少GC压力
        var maxResults = 20; // 限制结果数量
        var results = ArrayPool<string>.Shared.Rent(maxResults);
        var resultCount = 0;

        // 根据输入关键字过滤数据（不区分大小写的匹配）
        foreach (var word in ViewModel.SearchWords)
        {
            if (!FastContainsIgnoreCase(word, query)) continue;
            results[resultCount++] = word;
            if (resultCount >= maxResults) break;
        }

        // 创建结果列表
        var suggestions = new List<string>(resultCount);
        for (var i = 0; i < resultCount; i++)
        {
            suggestions.Add(results[i]);
        }

        // 归还数组到池
        ArrayPool<string>.Shared.Return(results);

        sender.ItemsSource = suggestions;
    }

    private void SearchBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string selectedItem)
        {
            sender.Text = selectedItem;
        }
    }

    private async void SearchBox_OnQuerySubmitted(AutoSuggestBox _, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        try
        {
            var query = args.ChosenSuggestion as string ?? args.QueryText;
            if (string.IsNullOrEmpty(query)) return;

            await SelectFormulation(query);
        }
        catch (Exception ex)
        {
            HandleException(ex, "搜索");
        }
    }

    #endregion

    #region UI 交互处理

    private void PaneOpenOrCloseButton_OnClick(object _, RoutedEventArgs _1)
    {
        SplitView.IsPaneOpen = !SplitView.IsPaneOpen;
    }

    private void OnAdaptiveStatesCurrentStateChanged(object _, VisualStateChangedEventArgs e)
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

    private void OnCategorySelectionChanged(object _0, SelectionChangedEventArgs _1)
    {
        // 增强检查逻辑
        if (!ViewModel.IsFormulationSelected || ViewModel.Flag)
        {
            // 重置标志但不继续处理
            ViewModel.Flag = false;
            return;
        }

        // 设置Flag防止循环
        ViewModel.Flag = true;

        try
        {
            var query = ViewModel.SelectedFormulation?.Name;
            ViewModel.UpdateFormulation(CategoryIdProperty);
            _ = SelectFormulation(query!);
        }
        catch (Exception ex)
        {
            HandleException(ex, "分类选择变更");
        }
    }

    #endregion

    #region 删除和新增操作

    private void CategoryDeleteButton_OnClick(object _1, RoutedEventArgs _2)
    {
        var s = ViewModel.SelectedCategory is { IsCategory: true } ? "分类" : "药物";
        var dialog = new ContentDialog
        {
            Title = $"删除{s}",
            Content = $"是否永久删除此{s}，删除后将无法恢复，确定要删除吗？",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            PrimaryButtonCommand = ViewModel.DeleteCategoryCommand,
            XamlRoot = XamlRoot
        };
        _ = dialog.ShowAsync();
    }

    private void CategoryInsertButton_OnClick(object sender, RoutedEventArgs e)
    {
        // 创建并显示对话框
        var dialog = CreateInsertDialog();
        _ = dialog.ShowAsync();
    }

    /// <summary>
    /// 创建新增对话框 - 优化版
    /// </summary>
    private ContentDialog CreateInsertDialog()
    {
        var dialog = new ContentDialog
        {
            Title = "新增",
            PrimaryButtonText = "确认",
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        // 主容器
        var mainPanel = new StackPanel { Spacing = 12 };

        // 错误提示
        var infoBar = new InfoBar
        {
            Title = "错误",
            Severity = InfoBarSeverity.Error,
            IsOpen = false
        };
        mainPanel.Children.Add(infoBar);

        // 模式选择区
        var rbCategory = new RadioButton { Content = "分类", IsChecked = true, GroupName = "ModeGroup" };
        var rbFormulation = new RadioButton { Content = "方剂", GroupName = "ModeGroup" };

        var modePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        modePanel.Children.Add(rbCategory);
        modePanel.Children.Add(rbFormulation);
        mainPanel.Children.Add(modePanel);

        // 创建两个面板
        var categoryPanel = CreateCategoryPanel();
        var formulationPanel = CreateFormulationPanel();
        formulationPanel.Visibility = Visibility.Collapsed;

        mainPanel.Children.Add(categoryPanel);
        mainPanel.Children.Add(formulationPanel);

        rbCategory.Checked += CheckedHandler;
        rbFormulation.Checked += CheckedHandler;

        dialog.Content = mainPanel;

        // 设置确认按钮处理
        dialog.PrimaryButtonClick += (_, args) =>
            ValidateAndProcessDialog(args, rbCategory.IsChecked == true, infoBar,
                categoryPanel, formulationPanel);

        return dialog;

        // 处理面板切换 - 优化版，避免不必要的UI更新
        void CheckedHandler(object _, RoutedEventArgs _1)
        {
            if (rbCategory.IsChecked == true)
            {
                if (categoryPanel.Visibility == Visibility.Visible) return;
                categoryPanel.Visibility = Visibility.Visible;
                formulationPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (formulationPanel.Visibility == Visibility.Visible) return;
                categoryPanel.Visibility = Visibility.Collapsed;
                formulationPanel.Visibility = Visibility.Visible;
            }
        }
    }

    /// <summary>
    /// 创建分类面板 - 优化版
    /// </summary>
    private StackPanel CreateCategoryPanel()
    {
        var categoryPanel = new StackPanel { Spacing = 8 };

        // 第一行：一级分类选择
        var categoryFirstRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var cbMainCategory = new ComboBox
        {
            Width = 150,
            PlaceholderText = "一级分类",
            ItemsSource = ViewModel.Categories,
            DisplayMemberPath = "Name"
        };

        var txtNewMainCategory = new TextBox
        {
            PlaceholderText = "新一级分类",
            Width = 150,
            Visibility = Visibility.Collapsed
        };

        var tbNewCategory = new ToggleButton
        {
            Content = new FontIcon { Glyph = "\uECC8" },
            Padding = new Thickness(5)
        };
        ToolTipService.SetToolTip(tbNewCategory, "新一级分类");

        // ToggleButton 切换控制 - 优化版
        tbNewCategory.Checked += (_, _) =>
        {
            cbMainCategory.Visibility = Visibility.Collapsed;
            txtNewMainCategory.Visibility = Visibility.Visible;
            txtNewMainCategory.Focus(FocusState.Programmatic);
        };
        tbNewCategory.Unchecked += (_, _) =>
        {
            cbMainCategory.Visibility = Visibility.Visible;
            txtNewMainCategory.Visibility = Visibility.Collapsed;
            cbMainCategory.Focus(FocusState.Programmatic);
        };

        // 新增二级分类的 TextBox
        var txtSubCategory = new TextBox { PlaceholderText = "新增二级分类", Width = 150 };

        categoryFirstRow.Children.Add(cbMainCategory);
        categoryFirstRow.Children.Add(txtNewMainCategory);
        categoryFirstRow.Children.Add(tbNewCategory);
        categoryFirstRow.Children.Add(txtSubCategory);

        categoryPanel.Children.Add(categoryFirstRow);

        // 将ComboBox和TextBox设置为面板的Tag，以便后续访问
        categoryPanel.Tag = new
        {
            MainCategoryComboBox = cbMainCategory,
            NewMainCategoryTextBox = txtNewMainCategory,
            NewCategoryToggle = tbNewCategory,
            SubCategoryTextBox = txtSubCategory
        };

        return categoryPanel;
    }

    /// <summary>
    /// 创建方剂面板 - 优化版
    /// </summary>
    private StackPanel CreateFormulationPanel()
    {
        var formulationPanel = new StackPanel
        {
            Spacing = 8,
            Orientation = Orientation.Horizontal
        };

        // 分类选择
        var cbCategoryForFormulation = new ComboBox
        {
            Width = 120,
            PlaceholderText = "选择分类",
            ItemsSource = ViewModel.Categories,
            DisplayMemberPath = "Name"
        };

        // 二级分类选择
        var cbSubCategoryForFormulation = new ComboBox
        {
            Width = 120,
            PlaceholderText = "二级分类",
            DisplayMemberPath = "Name",
            IsEnabled = false // 初始禁用
        };

        // 绑定联动 - 优化版
        cbCategoryForFormulation.SelectionChanged += (_, args) =>
        {
            cbSubCategoryForFormulation.IsEnabled = args.AddedItems.Count > 0;

            if (args.AddedItems.FirstOrDefault() is FormulationCategory selectedCategory)
            {
                cbSubCategoryForFormulation.ItemsSource = selectedCategory.Children;
                if (selectedCategory.Children.Count > 0)
                {
                    cbSubCategoryForFormulation.SelectedIndex = 0;
                }
            }
            else
            {
                cbSubCategoryForFormulation.ItemsSource = null;
            }
        };

        // 新建方剂名称
        var txtFormulationName = new TextBox { PlaceholderText = "新建方剂", Width = 120 };

        formulationPanel.Children.Add(cbCategoryForFormulation);
        formulationPanel.Children.Add(cbSubCategoryForFormulation);
        formulationPanel.Children.Add(txtFormulationName);

        // 将控件存储在Tag中以便后续访问
        formulationPanel.Tag = new
        {
            CategoryComboBox = cbCategoryForFormulation,
            SubCategoryComboBox = cbSubCategoryForFormulation,
            FormulationNameTextBox = txtFormulationName
        };

        return formulationPanel;
    }

    /// <summary>
    /// 验证并处理对话框输入 - 优化版
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private async void ValidateAndProcessDialog(ContentDialogButtonClickEventArgs args, bool isCategoryMode,
        InfoBar infoBar, StackPanel categoryPanel, StackPanel formulationPanel)
    {
        try
        {
            object? commandParam;

            if (isCategoryMode)
            {
                // 获取分类面板中的控件
                dynamic categoryControls = categoryPanel.Tag;
                var cbMainCategory = (ComboBox)categoryControls.MainCategoryComboBox;
                var txtNewMainCategory = (TextBox)categoryControls.NewMainCategoryTextBox;
                var tbNewCategory = (ToggleButton)categoryControls.NewCategoryToggle;
                var txtSubCategory = (TextBox)categoryControls.SubCategoryTextBox;

                // 验证一级分类
                string? mainCategory;
                if (tbNewCategory.IsChecked == true)
                {
                    if (string.IsNullOrWhiteSpace(txtNewMainCategory.Text))
                    {
                        ShowValidationError(args, infoBar, "请填写新增一级分类。");
                        return;
                    }
                    mainCategory = txtNewMainCategory.Text.Trim();
                }
                else
                {
                    if (cbMainCategory.SelectedItem == null)
                    {
                        ShowValidationError(args, infoBar, "请选择一级分类。");
                        return;
                    }
                    mainCategory = ((FormulationCategory)cbMainCategory.SelectedItem).Name;
                }

                // 验证二级分类
                if (string.IsNullOrWhiteSpace(txtSubCategory.Text))
                {
                    ShowValidationError(args, infoBar, "请填写新增二级分类。");
                    return;
                }
                var subCategory = txtSubCategory.Text.Trim();

                // 避免使用Tuple，直接使用ValueTuple减少内存分配
                commandParam = (mainCategory, subCategory);
            }
            else
            {
                // 获取方剂面板中的控件
                dynamic formulationControls = formulationPanel.Tag;
                var cbCategoryForFormulation = (ComboBox)formulationControls.CategoryComboBox;
                var cbSubCategoryForFormulation = (ComboBox)formulationControls.SubCategoryComboBox;
                var txtFormulationName = (TextBox)formulationControls.FormulationNameTextBox;

                // 验证输入
                if (cbCategoryForFormulation.SelectedItem == null)
                {
                    ShowValidationError(args, infoBar, "请选择分类。");
                    return;
                }

                if (cbSubCategoryForFormulation.SelectedItem == null)
                {
                    ShowValidationError(args, infoBar, "请选择二级分类。");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtFormulationName.Text))
                {
                    ShowValidationError(args, infoBar, "请填写方剂名称。");
                    return;
                }

                // 构造参数
                var categoryId = ((FormulationCategory)cbSubCategoryForFormulation.SelectedItem).Id;
                var formulationName = txtFormulationName.Text.Trim();

                // 避免使用Tuple，直接使用ValueTuple减少内存分配
                commandParam = (categoryId, formulationName);
            }

            // 调用命令
            if (ViewModel.InsertCategoryAndFormulationCommand.CanExecute(commandParam))
            {
                ViewModel.InsertCategoryAndFormulationCommand.Execute(commandParam);
            }

            // 如果是方剂模式，选中新增的方剂
            if (commandParam is not ValueTuple<int, string> valueTuple) return;
            var name = valueTuple.Item2;
            await SelectFormulation(name);
        }
        catch (Exception ex)
        {
            HandleException(ex, "新增分类或方剂");
        }
    }

    /// <summary>
    /// 显示验证错误 - 优化版
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ShowValidationError(ContentDialogButtonClickEventArgs args, InfoBar infoBar, string message)
    {
        args.Cancel = true;

        if (infoBar.IsOpen && infoBar.Message == message) return;
        infoBar.Message = message;
        infoBar.IsOpen = true;
    }

    #endregion

    #region 数据编辑

    private void DataGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.DataContext is not FormulationComposition composition) return;
        var propertyName = e.Column?.Tag?.ToString();
        if (string.IsNullOrEmpty(propertyName)) return;

        var updateCommand = composition.UpdateFormulationCompositionCommand;
        if (updateCommand.CanExecute(propertyName))
        {
            updateCommand.Execute(propertyName);
        }
    }

    #endregion

    #region 异常处理

    /// <summary>
    /// 统一处理异常 - 优化版
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void HandleException(Exception ex, string operation)
    {
        Logger.Error($"{operation}时出错：{ex.Message}", ex);
        Debug.WriteLine(ex);

        // 使用内联避免额外的函数调用开销
        if (!Application.Current.Resources.TryGetValue("ErrorInfoBar", out var resource) ||
            resource is not InfoBar errorInfoBar) return;
        errorInfoBar.Message = $"{operation}失败: {ex.Message}";
        errorInfoBar.IsOpen = true;
    }

    #endregion
}