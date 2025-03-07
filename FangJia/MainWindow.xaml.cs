
//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using FangJia.Common;
using FangJia.Helpers;
using FangJia.Pages;
using FangJia.ViewModel;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NLog;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;
using Category = FangJia.Common.Category;

#pragma warning disable CA1416

namespace FangJia;
public sealed partial class MainWindow
{
    private readonly AppWindow _appWindow;
    internal MainPageViewModel ViewModel = Locator.GetService<MainPageViewModel>();
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string CloseModeClose = "Close";
    private const string CloseModeHide = "Hide";
    public MainWindow()
    {
        InitializeComponent();

        // 1. 获取应用窗口，并注册相关事件
        _appWindow = AppWindow;
        _appWindow.Changed += AppWindow_Changed;
        Activated += MainWindow_Activated;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        AppTitleBar.Loaded += AppTitleBar_Loaded;
        ContentFrame.Navigated += ContentFrame_Navigated;
        _appWindow.Closing += OnClosing;
        // TrayIcon.Loaded += TrayIcon_Loaded;


        // 2. 扩展标题栏
        ExtendsContentIntoTitleBar = true;
        if (ExtendsContentIntoTitleBar)
        {
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }
        SetTitleBar(AppTitleBar);

#if WINDOWS10_0_19041_0_OR_GREATER
        // 3. 设置窗口标题
        TitleBarTextBlock.Text = AppInfo.Current.DisplayInfo.DisplayName;
#endif

        // 4. 设置窗口图标
        _appWindow.SetIcon("Assets/StoreLogo.ico");

        // 5. 设置标题栏按钮颜色
        TitleBarHelper.ApplySystemThemeToCaptionButtons(this);

        // 6. 设置窗口最小宽度
        var window = WindowManager.Get(this);
        window.MinWidth = 610;
    }

    /// <summary>
    /// 托盘图标加载时触发，确保窗口激活并显示。
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void TrayIcon_Loaded(object sender, RoutedEventArgs e)
    {
        this.Show(); // 显示窗口
        Activate(); // 使窗口获得焦点

    }

    /// <summary>
    /// 处理窗口关闭事件，根据用户设置决定关闭或隐藏窗口，并提供确认对话框。
    /// </summary>
    /// <param name="sender">应用窗口实例</param>
    /// <param name="args">窗口关闭事件参数</param>
    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        try
        {
            // 1. 阻止默认关闭行为
            args.Cancel = true;

            // 2. 获取设置视图模型
            var settingsViewModel = Locator.GetService<SettingsViewModel>();

            // 3. 根据关闭模式执行操作
            switch (settingsViewModel.CloseMode)
            {
                case CloseModeClose:
                    Application.Current.Exit(); // 直接退出应用
                    return;
                case CloseModeHide:
                    this.Hide(); // 隐藏窗口
                    return;
            }

            // 4. 显示关闭确认对话框，获取用户选择
            var (dialogResult, rememberChoice) = await ShowClosingDialogAsync();

            // 5. 更新设置（如果用户选择记住选项）
            if (rememberChoice)
            {
                settingsViewModel.CloseMode = dialogResult switch
                {
                    ContentDialogResult.Primary => CloseModeClose,  // 退出
                    ContentDialogResult.Secondary => CloseModeHide, // 隐藏
                    _ => settingsViewModel.CloseMode
                };
            }

            // 6. 根据用户选择执行操作
            switch (dialogResult)
            {
                case ContentDialogResult.Primary:
                    Application.Current.Exit(); // 退出应用
                    break;
                case ContentDialogResult.Secondary:
                    this.Hide(); // 隐藏窗口
                    ViewModel.IsWindowVisible = false;
                    break;
                case ContentDialogResult.None:
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            // 7. 记录异常信息
            Logger.Error($"关闭发生错误：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 显示关闭确认对话框，用户可选择关闭应用或最小化到托盘，并支持“记住选择”选项。
    /// </summary>
    /// <returns>返回一个元组，包含用户选择的对话框结果，以及是否选中了“记住选择”。</returns>
    private async Task<(ContentDialogResult dialogResult, bool rememberChoice)> ShowClosingDialogAsync()
    {
        // 1. 构造对话框内容
        var content = new StackPanel { Margin = new Thickness(0) };
        content.Children.Add(new TextBlock { Text = "是否关闭应用程序？" });

        // 2. 记住选择复选框
        var rememberChoiceCheckBox = new CheckBox
        {
            Content = "记住我的选择",
            Margin = new Thickness(16, 10, 0, 0)
        };
        content.Children.Add(rememberChoiceCheckBox);

        // 3. 创建并配置 ContentDialog
        var dialog = new ContentDialog
        {
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "是否关闭",
            PrimaryButtonText = "关闭",
            SecondaryButtonText = "最小化到托盘",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = content,
            XamlRoot = ContentFrame.XamlRoot, // 设置 XAML 根元素（仅适用于 WinUI）
            RequestedTheme = ThemeHelper.RootTheme // 适配当前主题
        };

        // 4. 显示对话框并获取用户选择
        var result = await dialog.ShowAsync();

        // 5. 返回用户选择的结果以及“记住选择”状态
        return (result, rememberChoiceCheckBox.IsChecked == true);
    }
    /// <summary>
    /// 处理页面导航事件，根据导航目标更新 NavigationView 选中项，并设置页面标题路径。
    /// </summary>
    /// <param name="sender">触发事件的控件</param>
    /// <param name="e">导航事件参数</param>
    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        // 1. 获取当前导航的页面类型
        if (sender is not Frame frame) return;
        var type = frame.Content.GetType().ToString().Replace("FangJia.Pages.", "");

        // 2. 根据导航目标更新 NavigationView 选中项
        switch (NavigationViewControl.SelectedItem)
        {
            case CategoryBase selectedItem:
                {
                    var item = selectedItem as Category;
                    var selectedPath = item?.Path;

                    if (type is "SettingsPage" or "LogsPage")
                    {
                        // 选中“设置”项
                        NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
                    }
                    else if (!string.IsNullOrWhiteSpace(selectedPath) && !type.Contains(selectedPath))
                    {
                        // 选中对应类别
                        NavigationViewControl.SelectedItem = NavigationHelper.Categorizes[type.Replace("Page", "")];
                    }

                    break;
                }
            case NavigationViewItem { Content: "设置" }:
                {
                    // 确保当前选中项正确
                    if (type is not ("SettingsPage" or "LogsPage"))
                    {
                        NavigationViewControl.SelectedItem = NavigationHelper.Categorizes[type.Replace("Page", "")];
                    }

                    break;
                }
        }

        // 3. 更新 ViewModel 页面标题
        ViewModel.PageHeader.Clear();
        switch (type)
        {
            case "HomePage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Home"] as Category)!);
                break;
            case "DataPage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Data"] as Category)!);
                break;
            case "AboutPage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["About"] as Category)!);
                break;
            case "FormulationPage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Data"] as Category)!);
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Formulation"] as Category)!);
                break;
            case "DrugPage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Data"] as Category)!);
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Drug"] as Category)!);
                break;
            case "ClassicPage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Data"] as Category)!);
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Classic"] as Category)!);
                break;
            case "CasePage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Data"] as Category)!);
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Case"] as Category)!);
                break;
            case "SettingsPage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Settings"] as Category)!);
                break;
            case "LogsPage":
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Settings"] as Category)!);
                ViewModel.PageHeader.Add((NavigationHelper.Categorizes["Logs"] as Category)!);
                break;
        }
    }
    /// <summary>
    /// 标题栏加载完成时触发，若扩展了内容到标题栏，则初始化交互区域。
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar)
        {
            // 1. 设置初始交互区域
            SetRegionsForCustomTitleBar();
        }
    }

    /// <summary>
    /// 当标题栏大小发生变化时触发，若扩展了内容到标题栏，则更新交互区域。
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">大小变化事件参数</param>
    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar)
        {
            // 1. 更新交互区域
            SetRegionsForCustomTitleBar();
        }
    }

    /// <summary>
    /// 设置自定义标题栏的交互区域，确保窗口交互行为符合预期。
    /// </summary>
    private void SetRegionsForCustomTitleBar()
    {
        // 1. 计算缩放比例
        var scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

        // 2. 计算左右边距，使窗口标题栏的内容正确布局
        RightPaddingColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scaleAdjustment);
        LeftPaddingColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scaleAdjustment);

        // 3. 获取 PersonPicture 控件的交互区域
        var transform = PersonPic.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0, PersonPic.ActualWidth, PersonPic.ActualHeight));
        var personPicRect = GetRect(bounds, scaleAdjustment);

        // 4. 获取全屏按钮的交互区域
        transform = FullScreenButton.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0, FullScreenButton.ActualWidth, FullScreenButton.ActualHeight));
        var fullScreenRect = GetRect(bounds, scaleAdjustment);

        // 5. 设定可交互区域
        var rectArray = new[] { personPicRect, fullScreenRect };
        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
    }
    /// <summary>
    /// 根据窗口的边界和缩放比例，计算并返回整数矩形区域。
    /// </summary>
    /// <param name="bounds">窗口边界</param>
    /// <param name="scale">缩放比例</param>
    /// <returns>转换后的整数矩形区域</returns>
    private static RectInt32 GetRect(Rect bounds, double scale) => new(
        _X: (int)Math.Round(bounds.X * scale),
        _Y: (int)Math.Round(bounds.Y * scale),
        _Width: (int)Math.Round(bounds.Width * scale),
        _Height: (int)Math.Round(bounds.Height * scale)
    );

    /// <summary>
    /// 处理窗口激活状态变化，调整标题栏文本透明度。
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="args">窗口激活事件参数</param>
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // 1. 若窗口失去焦点，降低标题栏文本透明度
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            TitleBarTextBlock.Opacity = 0.5;
        }
        else
        {
            // 2. 窗口激活时恢复透明度
            TitleBarTextBlock.Opacity = 1;
        }
    }

    /// <summary>
    /// 处理窗口状态变化，调整标题栏的可见性和交互行为。
    /// </summary>
    /// <param name="sender">触发事件的窗口</param>
    /// <param name="args">窗口变化事件参数</param>
    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        // 1. 如果窗口的显示模式未改变，则无需更新
        if (!args.DidPresenterChange) return;

        // 2. 根据不同窗口模式调整标题栏显示
        switch (sender.Presenter.Kind)
        {
            case AppWindowPresenterKind.CompactOverlay:
                // 2.1 紧凑模式 - 隐藏自定义标题栏，使用系统默认标题栏
                AppTitleBar.Visibility = Visibility.Collapsed;
                sender.TitleBar.ResetToDefault();
                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                ViewModel.IsFullScreen = false;
                break;

            case AppWindowPresenterKind.FullScreen:
                // 2.2 全屏模式 - 隐藏所有标题栏
                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
                ViewModel.IsFullScreen = true;
                break;

            case AppWindowPresenterKind.Overlapped:
                // 2.3 普通窗口模式 - 隐藏系统标题栏，使用自定义标题栏
                AppTitleBar.Visibility = Visibility.Visible;
                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                ViewModel.IsFullScreen = false;
                break;

            case AppWindowPresenterKind.Default:
            default:
                // 2.4 默认模式 - 使用系统标题栏
                sender.TitleBar.ResetToDefault();
                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                ViewModel.IsFullScreen = false;
                break;
        }
    }
    /// <summary>
    /// 处理 NavigationView 选项变化，根据选择的项导航到相应页面。
    /// </summary>
    /// <param name="sender">NavigationView 控件</param>
    /// <param name="args">导航选项更改事件参数</param>
    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // 1. 若选择“设置”选项，跳转到设置页面
        if (args.IsSettingsSelected)
        {
            if (ContentFrame.CurrentSourcePageType == NavigationHelper.GetType("SettingsPage")) return;
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        // 2. 确保选中的项为有效的类别
        if (args.SelectedItem is not Category selectedCategory) return;

        // 3. 获取导航路径，并跳转到对应页面
        var path = selectedCategory.Path;
        if (string.IsNullOrEmpty(path)) return;
        var type = NavigationHelper.GetType(path);
        if (ContentFrame.CurrentSourcePageType == type) return;

        ContentFrame.Navigate(type);
    }

    /// <summary>
    /// 处理 NavigationView 加载事件，默认选中首页。
    /// </summary>
    /// <param name="sender">NavigationView 控件</param>
    /// <param name="e">事件参数</param>
    private void NavigationView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationView navigationView)
        {
            // 1. 默认选中首页
            navigationView.SelectedItem = NavigationHelper.Categorizes["Home"];
        }
    }

    /// <summary>
    /// 切换窗口全屏模式，并更新按钮图标及提示文本。
    /// </summary>
    /// <param name="sender">触发事件的控件</param>
    /// <param name="_">事件参数（未使用）</param>
    private void FullScreen(object sender, RoutedEventArgs _)
    {
        // 1. 切换窗口显示模式
        _appWindow.SetPresenter(ViewModel.IsFullScreen
            ? AppWindowPresenterKind.Default
            : AppWindowPresenterKind.FullScreen);

        // 2. 更新按钮图标
        if (sender is not Button button) return;
        button.Content = new FontIcon()
        {
            FontSize = 10,
            Glyph = ViewModel.IsFullScreen ? "\uE73F" : "\uE740" // 切换全屏/退出全屏图标
        };

        // 3. 更新工具提示文本
        ToolTipService.SetToolTip(button, ViewModel.IsFullScreen ? "退出全屏" : "全屏");
    }
    /// <summary>
    /// 处理 NavigationView 面板显示模式变化，调整标题栏的边距。
    /// </summary>
    /// <param name="sender">NavigationView 控件</param>
    /// <param name="_">事件参数（未使用）</param>
    private void OnPaneDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs _)
    {
        // 1. 根据不同的面板显示模式调整标题栏的边距
        AppTitleBar.Margin = sender.PaneDisplayMode switch
        {
            NavigationViewPaneDisplayMode.Top => new Thickness(16, 0, 0, 0),
            _ => sender is { DisplayMode: NavigationViewDisplayMode.Minimal }
                ? new Thickness(96, 0, 0, 0)
                : new Thickness(48, 0, 0, 0)
        };
    }

    /// <summary>
    /// 处理面包屑导航，点击某项时移除后续项并跳转至对应页面。
    /// </summary>
    /// <param name="sender">BreadcrumbBar 控件</param>
    /// <param name="args">面包屑点击事件参数</param>
    private void BreadcrumbBar2_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        // 1. 获取面包屑导航项列表
        var items = PageTitleBreadcrumbBar.ItemsSource as ObservableCollection<Category>;

        // 2. 移除点击项后面的所有导航项
        for (var i = items!.Count - 1; i >= args.Index + 1; i--)
        {
            items.RemoveAt(i);
        }

        // 3. 导航到点击的目标页面
        ContentFrame.Navigate(NavigationHelper.GetType(items[args.Index].Path));
    }

    /// <summary>
    /// 处理 NavigationView 的返回按钮点击事件，执行 Frame 返回操作。
    /// </summary>
    /// <param name="sender">NavigationView 控件</param>
    /// <param name="args">返回按钮点击事件参数</param>
    private void NavigationViewControl_OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        // 1. 若 Frame 允许返回，则执行返回操作
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    /// <summary>
    /// 监听主题变化，调整窗口背景并应用系统主题到标题栏按钮。
    /// </summary>
    /// <param name="sender">触发事件的控件</param>
    /// <param name="args">事件参数（未使用）</param>
    private void MainGrid_OnActualThemeChanged(FrameworkElement sender, object args)
    {
        // 1. 若当前主题非 Mica 主题，则设置窗口背景
        if (!ThemeHelper.IsMicaTheme)
        {
            ThemeHelper.SetWindowBackground();
        }

        // 2. 应用系统主题到标题栏按钮
        TitleBarHelper.ApplySystemThemeToCaptionButtons(this);
    }

    /// <summary>
    /// 处理“设置”菜单项点击事件，显示窗口并跳转到设置页面。
    /// </summary>
    /// <param name="sender">触发事件的菜单项</param>
    /// <param name="e">事件参数</param>
    private void SettingsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        // 1. 显示窗口并激活
        this.Show();
        Activate();

        // 2. 导航到“设置”页面
        ContentFrame.Navigate(NavigationHelper.GetType("SettingsPage"));
    }

    /// <summary>
    /// 处理“日志”菜单项点击事件，显示窗口并跳转到日志页面。
    /// </summary>
    /// <param name="sender">触发事件的菜单项</param>
    /// <param name="e">事件参数</param>
    private void LogsMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        // 1. 显示窗口并激活
        this.Show();
        Activate();

        // 2. 导航到“日志”页面
        ContentFrame.Navigate(NavigationHelper.GetType("LogsPage"));
    }

    private void MainWindow_OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        ShowOrHideMenuItem.Text = (bool)ViewModel.IsWindowVisible! ? "最小化到托盘" : "打开窗口";
    }
}
