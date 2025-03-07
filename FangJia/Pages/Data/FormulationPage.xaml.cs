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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // 取消事件订阅，防止内存泄漏
        ViewModel.SelectedFormulaChanged -= ViewModelSelectedFormulaChanged;
        ViewModel.FormulaImageChanged -= ViewModelOnFormulaImageChanged;
        Unloaded -= Page_Unloaded;
    }

    #region 动画处理

    /// <summary>
    /// 应用淡入淡出动画到指定元素
    /// </summary>
    private static void ApplyFadeAnimation(UIElement element, double fromValue, double toValue, double durationSeconds)
    {
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

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);

        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");

        storyboard.Begin();
    }

    /// <summary>
    /// 对多个元素应用淡入淡出动画
    /// </summary>
    private static void ApplyFadeAnimationToElements(double fromValue, double toValue, double durationSeconds, params UIElement[] elements)
    {
        var storyboard = new Storyboard();

        foreach (var element in elements)
        {
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

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);
        }

        storyboard.Begin();
    }

    private void ViewModelOnFormulaImageChanged(object sender, RoutedEventArgs e)
    {
        ApplyFadeAnimation(Image, 0.0, 1.0, 0.5);
    }

    private void ViewModelSelectedFormulaChanged(object sender, RoutedEventArgs e)
    {
        ApplyFadeAnimation(Viewer, 0.0, 1.0, 0.2);
    }

    #endregion

    #region TreeView 操作

    private void TreeView_OnSelectionChanged(TreeView _, TreeViewSelectionChangedEventArgs args)
    {
        if (args.AddedItems.FirstOrDefault() is not FormulationCategory selectedCategory) return;

        // 递归展开选中的项
        ExpandTreeViewItem(selectedCategory);

        // 递归收起未被选中的项
        CollapseUnselectedItems(FormulationCategoryTree.RootNodes, selectedCategory);

        if (selectedCategory.IsCategory || args.RemovedItems.Count <= 0) return;

        // 创建渐隐动画
        ApplyFadeAnimationToElements(1.0, 0.0, 0.1, Viewer, Image);
    }

    /// <summary>
    /// 递归查找并展开匹配的项
    /// </summary>
    private void ExpandTreeViewItem(FormulationCategory? selectedCategory)
    {
        if (selectedCategory == null) return;

        foreach (var node in FormulationCategoryTree.RootNodes)
        {
            if (ExpandIfMatch(node, selectedCategory))
                break;
        }
    }

    /// <summary>
    /// 递归展开匹配的树节点
    /// </summary>
    private static bool ExpandIfMatch(TreeViewNode node, FormulationCategory? targetCategory)
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
    /// 递归收起未被选中的节点
    /// </summary>
    private static void CollapseUnselectedItems(IList<TreeViewNode> nodes, FormulationCategory? selectedCategory)
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
    /// 检查节点是否包含选中的子项
    /// </summary>
    private static bool ContainsSelectedItem(TreeViewNode node, FormulationCategory? selectedCategory)
    {
        if (node.Content is FormulationCategory category && category == selectedCategory)
        {
            return true;
        }

        return node.Children.Any(child => ContainsSelectedItem(child, selectedCategory));
    }

    #endregion

    #region 搜索功能

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

        // 根据输入关键字过滤数据（不区分大小写的匹配）
        var suggestions = ViewModel.SearchWords
            .Where(item => item.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

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

    private async Task SelectFormulation(string query)
    {
        // 精确匹配优先，再找包含项
        var targetNode = ViewModel.SearchDictionary.TryGetValue(query, out var value)
            ? value
            : ViewModel.SearchDictionary.Values.FirstOrDefault(f =>
                f.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        if (targetNode == null) return;

        // 使用LINQ简化嵌套循环
        var categoryEnumerable = from rootNode in FormulationCategoryTree.RootNodes
                                 where rootNode.Content is FormulationCategory
                                 let category = (FormulationCategory)rootNode.Content
                                 from child in category.Children
                                 from formulation in child.Children
                                 where formulation.Name == targetNode.Name
                                 select new { Category = category, SubCategory = child, Formulation = formulation };

        var foundItem = categoryEnumerable.FirstOrDefault();
        if (foundItem != null)
        {
            ExpandTreeViewItem(foundItem.Category);
            await Task.Delay(50);
            ExpandTreeViewItem(foundItem.SubCategory);
            await Task.Delay(50);
            ViewModel.SelectedCategory = foundItem.Formulation;
            // 递归收起未被选中的项
            CollapseUnselectedItems(FormulationCategoryTree.RootNodes, foundItem.Formulation);
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

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
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

    private void CategoryDeleteButton_OnClick(object sender, RoutedEventArgs e)
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
    /// 创建新增对话框
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

        // 处理面板切换
        rbCategory.Checked += (_, _) =>
        {
            categoryPanel.Visibility = Visibility.Visible;
            formulationPanel.Visibility = Visibility.Collapsed;
        };
        rbFormulation.Checked += (_, _) =>
        {
            categoryPanel.Visibility = Visibility.Collapsed;
            formulationPanel.Visibility = Visibility.Visible;
        };

        dialog.Content = mainPanel;

        // 设置确认按钮处理
        dialog.PrimaryButtonClick += (_, args) =>
            ValidateAndProcessDialog(args, rbCategory.IsChecked == true, infoBar,
                categoryPanel, formulationPanel);

        return dialog;
    }

    /// <summary>
    /// 创建分类面板
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

        // ToggleButton 切换控制
        tbNewCategory.Checked += (_, _) =>
        {
            cbMainCategory.Visibility = Visibility.Collapsed;
            txtNewMainCategory.Visibility = Visibility.Visible;
        };
        tbNewCategory.Unchecked += (_, _) =>
        {
            cbMainCategory.Visibility = Visibility.Visible;
            txtNewMainCategory.Visibility = Visibility.Collapsed;
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
    /// 创建方剂面板
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
            DisplayMemberPath = "Name"
        };

        // 绑定联动
        cbCategoryForFormulation.SelectionChanged += (_, args) =>
        {
            cbSubCategoryForFormulation.ItemsSource =
                (args.AddedItems.FirstOrDefault() as FormulationCategory)?.Children;
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
    /// 验证并处理对话框输入
    /// </summary>
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
                if (tbNewCategory.IsChecked == true)
                {
                    if (string.IsNullOrWhiteSpace(txtNewMainCategory.Text))
                    {
                        ShowValidationError(args, infoBar, "请填写新增一级分类。");
                        return;
                    }
                }
                else if (cbMainCategory.SelectedItem == null)
                {
                    ShowValidationError(args, infoBar, "请选择一级分类。");
                    return;
                }

                // 验证二级分类
                if (string.IsNullOrWhiteSpace(txtSubCategory.Text))
                {
                    ShowValidationError(args, infoBar, "请填写新增二级分类。");
                    return;
                }

                // 构造参数
                var mainCategory = tbNewCategory.IsChecked == true
                    ? txtNewMainCategory.Text
                    : ((FormulationCategory)cbMainCategory.SelectedItem)?.Name;
                var subCategory = txtSubCategory.Text;
                commandParam = Tuple.Create(mainCategory, subCategory);
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
                var formulationName = txtFormulationName.Text;
                commandParam = Tuple.Create(categoryId, formulationName);
            }

            // 调用命令
            if (ViewModel.InsertCategoryAndFormulationCommand.CanExecute(commandParam))
            {
                ViewModel.InsertCategoryAndFormulationCommand.Execute(commandParam);
            }

            // 如果是方剂模式，选中新增的方剂
            if (commandParam is (int, string name))
            {
                await SelectFormulation(name);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, "新增分类或方剂");
        }
    }

    /// <summary>
    /// 显示验证错误
    /// </summary>
    private void ShowValidationError(ContentDialogButtonClickEventArgs args, InfoBar infoBar, string message)
    {
        args.Cancel = true;
        infoBar.IsOpen = true;
        infoBar.Message = message;
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
    /// 统一处理异常
    /// </summary>
    private void HandleException(Exception ex, string operation)
    {
        Logger.Error($"{operation}时出错：{ex.Message}", ex);
        Debug.WriteLine(ex);
        // 可以考虑添加用户提示逻辑
    }

    #endregion
}