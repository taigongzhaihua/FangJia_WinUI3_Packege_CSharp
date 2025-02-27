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
using FangJia.Common;
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Dispatching;
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
/// 
/// </summary>
public sealed partial class FormulationPage
{
    internal readonly FormulationViewModel ViewModel = Locator.GetService<FormulationViewModel>();
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public FormulationPage()
    {
        InitializeComponent();
        _ = ViewModel.LoadCategoriesAsync(_dispatcherQueue);
    }

    private void TreeView_OnSelectionChanged(TreeView _, TreeViewSelectionChangedEventArgs args)
    {
        if (args.AddedItems.FirstOrDefault() is not FormulationCategory selectedCategory) return;
        // 递归展开选中的项
        ExpandTreeViewItem(selectedCategory);

        // 递归收起未被选中的项
        CollapseUnselectedItems(FormulationCategoryTree.RootNodes, selectedCategory);
        if (!selectedCategory.IsCategory)
        {
            // 创建关键帧动画对象
            var animation = new DoubleAnimationUsingKeyFrames
            {
                // 总时长：1秒下行 + 50毫秒停顿 + 1秒上行 = 2.05秒
                Duration = TimeSpan.FromSeconds(1.0)
            };

            // 添加起始关键帧：透明度1.0（动画开始时）
            animation.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                Value = 1.0,
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))
            });

            // 1秒后，透明度过渡到0.0
            animation.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                Value = 0.0,
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2))
            });

            // 插入一个离散关键帧，在1.05秒时依然保持0.0，起到50毫秒的停顿效果
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame
            {
                Value = 0.0,
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8))
            });

            // 2.05秒时，透明度过渡回1.0
            animation.KeyFrames.Add(new LinearDoubleKeyFrame
            {
                Value = 1.0,
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.0))
            });

            // 创建 Storyboard 并添加动画
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            // 设置动画目标属性和目标控件
            Storyboard.SetTarget(animation, Viewer);
            Storyboard.SetTargetProperty(animation, "Opacity");

            // 启动动画
            storyboard.Begin();
        }
    }

    /// <summary>
    /// 递归查找 `TreeViewNode` 并展开匹配的项
    /// </summary>
    private void ExpandTreeViewItem(FormulationCategory? selectedCategory)
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
    /// 递归收起未被选中的 `TreeViewNode`
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
    /// 检查 `TreeViewNode` 是否包含选中的子项
    /// </summary>
    private static bool ContainsSelectedItem(TreeViewNode node, FormulationCategory? selectedCategory)
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
            var suggestions =
                ViewModel.SearchWords
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

    private async void SearchBox_OnQuerySubmitted(AutoSuggestBox _, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        try
        {
            var query = args.ChosenSuggestion as string ?? args.QueryText;
            if (string.IsNullOrEmpty(query)) return;

            await SelectFormulation(query);
        }
        catch (Exception e)
        {
            Logger.Error($"搜索时出错：{e.Message}", e);
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
                    Opacity = 0.2,
                    BlurRadius = 8,
                    Offset = "2"
                });
                break;
        }
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ViewModel.IsFormulationSelected) return;
        if (ViewModel.Flag)
        {
            ViewModel.Flag = false;
            return;
        }

        var query = ViewModel.SelectedFormulation?.Name;
        ViewModel.UpdateFormulation("CategoryId");
        _ = SelectFormulation(query!);
    }

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
        // 创建 ContentDialog
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
        // 模式选择区：两个 RadioButton（默认选择“分类”）
        var modePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        var rbCategory = new RadioButton { Content = "分类", IsChecked = true, GroupName = "ModeGroup" };
        var rbFormulation = new RadioButton { Content = "方剂", GroupName = "ModeGroup" };
        modePanel.Children.Add(rbCategory);
        modePanel.Children.Add(rbFormulation);
        mainPanel.Children.Add(modePanel);

        // 分类模式面板
        var categoryPanel = new StackPanel { Spacing = 8 };
        // 第一行：一级分类选择
        var categoryFirstRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var cbMainCategory = new ComboBox
        {
            Width = 150,
            PlaceholderText = "一级分类",
            // 这里假设 ViewModel.CategoryList 是一个集合，比如 List<string> 或其他类型
            ItemsSource = ViewModel.Categories,
            DisplayMemberPath = "Name"
        };

        var txtNewMainCategory = new TextBox
        { PlaceholderText = "新一级分类", Width = 150, Visibility = Visibility.Collapsed };
        var tbNewCategory = new ToggleButton { Content = new FontIcon { Glyph = "\uECC8" }, Padding = new Thickness(5) };
        ToolTipService.SetToolTip(tbNewCategory, "新一级分类");
        // ToggleButton 切换：选中时显示 TextBox，否则显示 ComboBox
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


        // 方剂模式面板（初始隐藏）
        var formulationPanel = new StackPanel
        { Spacing = 8, Visibility = Visibility.Collapsed, Orientation = Orientation.Horizontal };

        // 第一个 ComboBox：选择分类（假设 ItemsSource 同 CategoryList，且 SelectedValuePath 为 "Id"）
        var cbCategoryForFormulation = new ComboBox
        {
            Width = 120,
            PlaceholderText = "选择分类",
            ItemsSource = ViewModel.Categories,
            DisplayMemberPath = "Name"
        };

        // 第二个 ComboBox：选择二级分类
        var cbSubCategoryForFormulation = new ComboBox
        {
            Width = 120,
            PlaceholderText = "二级分类",
            DisplayMemberPath = "Name"
        };

        cbCategoryForFormulation.SelectionChanged += (_, args) =>
        {
            cbSubCategoryForFormulation.ItemsSource =
                (args.AddedItems.FirstOrDefault() as FormulationCategory)?.Children;
        };
        // 新建方剂名称的 TextBox
        var txtFormulationName = new TextBox { PlaceholderText = "新建方剂", Width = 120 };

        formulationPanel.Children.Add(cbCategoryForFormulation);
        formulationPanel.Children.Add(cbSubCategoryForFormulation);
        formulationPanel.Children.Add(txtFormulationName);

        // 将两个模式面板添加到主容器中
        mainPanel.Children.Add(categoryPanel);
        mainPanel.Children.Add(formulationPanel);

        // 根据 RadioButton 切换显示的面板
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

        // 设置对话框内容
        dialog.Content = mainPanel;

        // PrimaryButton 点击事件：构造 Command 参数并调用 ViewModel 命令
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            try
            {
                object? commandParam;

                if (rbCategory.IsChecked == true)
                {
                    // 分类模式：先验证一级分类是否填写
                    if (tbNewCategory.IsChecked == true)
                    {
                        // 新大类模式：必须填写新一级分类
                        if (string.IsNullOrWhiteSpace(txtNewMainCategory.Text))
                        {
                            args.Cancel = true;
                            infoBar.IsOpen = true;
                            infoBar.Message = "请填写新增一级分类。";
                            return;
                        }
                    }
                    else
                    {
                        // 非新大类模式：必须选择一个一级分类
                        if (cbMainCategory.SelectedItem == null)
                        {
                            args.Cancel = true;
                            infoBar.IsOpen = true;
                            infoBar.Message = "请选择一级分类。";
                            return;
                        }
                    }

                    // 分类模式：二级分类为必填项
                    if (string.IsNullOrWhiteSpace(txtSubCategory.Text))
                    {
                        args.Cancel = true;
                        infoBar.IsOpen = true;
                        infoBar.Message = "请填写新增二级分类。";
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
                    // 方剂模式：两个 ComboBox 和新建方剂名称均为必填项
                    if (cbCategoryForFormulation.SelectedItem == null)
                    {
                        args.Cancel = true;
                        infoBar.IsOpen = true;
                        infoBar.Message = "请选择分类。";
                        return;
                    }

                    if (cbSubCategoryForFormulation.SelectedItem == null)
                    {
                        args.Cancel = true;
                        infoBar.IsOpen = true;
                        infoBar.Message = "请选择二级分类。";
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(txtFormulationName.Text))
                    {
                        args.Cancel = true;
                        infoBar.IsOpen = true;
                        infoBar.Message = "请填写方剂名称。";
                        return;
                    }

                    // 这里假设 cbCategoryForFormulation.SelectedValue 为 int 类型
                    var categoryId = ((FormulationCategory)cbSubCategoryForFormulation.SelectedItem).Id;
                    var formulationName = txtFormulationName.Text;
                    commandParam = Tuple.Create(categoryId, formulationName);
                }

                // 调用 ViewModel 中的命令
                if (ViewModel.InsertCategoryAndFormulationCommand.CanExecute(commandParam))
                {
                    ViewModel.InsertCategoryAndFormulationCommand.Execute(commandParam);
                }

                await Task.CompletedTask;
                if (commandParam is (int, string name))
                {
                    // 选中新增的方剂
                    await SelectFormulation(name);
                }
            }
            catch (Exception exception)
            {
                Logger.Error($"新增分类或方剂时出错：{exception.Message}", exception);
                Debug.WriteLine(exception);
            }
        };


        // 显示对话框
        _ = dialog.ShowAsync();
    }
}