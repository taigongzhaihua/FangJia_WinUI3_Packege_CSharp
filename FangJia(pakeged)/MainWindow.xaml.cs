using FangJia.Common;
using FangJia.Helpers;
using FangJia.Pages;
using FangJia.ViewModel;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using Category = FangJia.Common.Category;

#pragma warning disable CA1416



// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FangJia;

/// <summary>
/// 一个可以单独使用或在 Frame 中导航到的空窗口。
/// </summary>

public sealed partial class MainWindow
{
    private readonly AppWindow _appWindow;
    internal MainPageViewModel ViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // 假设 "this" 是一个 XAML 窗口。在不使用 WinUI 3 1.3 或更高版本的项目中，使用互操作 API 获取 AppWindow。
        // WinUI 3 1.3 或更高版本，使用互操作 API 获取 AppWindow。

        ViewModel = Locator.GetService<MainPageViewModel>();
        _appWindow = AppWindow;
        _appWindow.Changed += AppWindow_Changed;
        Activated += MainWindow_Activated;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        AppTitleBar.Loaded += AppTitleBar_Loaded;
        ContentFrame.Navigated += ContentFrame_Navigated;

        ExtendsContentIntoTitleBar = true;
        if (ExtendsContentIntoTitleBar)
        {
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }
        SetTitleBar(AppTitleBar);
#if WINDOWS10_0_19041_0_OR_GREATER
        TitleBarTextBlock.Text = AppInfo.Current.DisplayInfo.DisplayName;
#endif
        // 设置窗口图标
        _appWindow.SetIcon("Assets/StoreLogo.ico");

        // 设置窗口标题栏按钮颜色
        _appWindow.TitleBar.ButtonForegroundColor =

            ((SolidColorBrush)Application.Current.Resources["WindowCaptionForeground"]).Color;

        _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;

        // Replace the line causing the error
        // var window = new WinUIEx.WindowManager(this);

        // With the correct instantiation
        var window = WinUIEx.WindowManager.Get(this);
        window.MinWidth = 610;
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (sender is not Frame frame) return;
        var type = frame.Content.GetType().ToString().Replace("FangJia.Pages.", "");

        switch (NavigationViewControl.SelectedItem)
        {
            case CategoryBase selectedItem:
                {
                    var item = selectedItem as Category;
                    var selectedPath = item?.Path;
                    if (type == "SettingsPage")
                    {
                        NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
                    }
                    else if (!string.IsNullOrWhiteSpace(selectedPath) && !type.Contains(selectedPath))
                    {
                        NavigationViewControl.SelectedItem = NavigationHelper.Categorizes[type.Replace("Page", "")];
                    }

                    break;
                }
            case NavigationViewItem { Content: "设置" }:
                {
                    if (!type.Contains("SettingsPage"))
                    {
                        NavigationViewControl.SelectedItem = NavigationHelper.Categorizes[type.Replace("Page", "")];
                    }

                    break;
                }
        }

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
        }
    }

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar)

        {
            // Set the initial interactive regions.
            // 设置初始交互区域。
            SetRegionsForCustomTitleBar();
        }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {

        if (ExtendsContentIntoTitleBar)

        {
            // Update interactive regions if the size of the window changes.
            // 如果窗口大小发生变化，更新交互区域。

            SetRegionsForCustomTitleBar();
        }
    }

    private void SetRegionsForCustomTitleBar()
    {
        // 指定标题栏的交互区域。

        var scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

        RightPaddingColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scaleAdjustment);
        LeftPaddingColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scaleAdjustment);

        // 获取 PersonPicture 控件周围的矩形。
        var transform = PersonPic.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0,
            PersonPic.ActualWidth,
            PersonPic.ActualHeight));
        var personPicRect = GetRect(bounds, scaleAdjustment);

        // 获取全屏按钮周围的矩形。
        transform = FullScreenButton.TransformToVisual(null);
        bounds = transform.TransformBounds(new Rect(0, 0,
            FullScreenButton.ActualWidth,
            FullScreenButton.ActualHeight));
        var fullScreenRect = GetRect(bounds, scaleAdjustment);

        var rectArray = new[] { personPicRect, fullScreenRect };

        var nonClientInputSrc =
            InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
    }

    private static RectInt32 GetRect(Rect bounds, double scale) => new(
        _X: (int)Math.Round(bounds.X * scale),
        _Y: (int)Math.Round(bounds.Y * scale),
        _Width: (int)Math.Round(bounds.Width * scale),
        _Height: (int)Math.Round(bounds.Height * scale)
    );

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            TitleBarTextBlock.Opacity = 0.5;
        }
        else
        {
            TitleBarTextBlock.Opacity = 1;
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPresenterChange) return;
        switch (sender.Presenter.Kind)
        {
            case AppWindowPresenterKind.CompactOverlay:
                // 紧凑覆盖 - 隐藏自定义标题栏 并 使用默认的系统标题栏。
                AppTitleBar.Visibility = Visibility.Collapsed;
                sender.TitleBar.ResetToDefault();
                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                ViewModel.IsFullScreen = false;
                break;

            case AppWindowPresenterKind.FullScreen:
                // 全屏 - 隐藏自定义标题栏 和 默认的系统标题栏。
                // AppTitleBar.Visibility = Visibility.Collapsed;

                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
                ViewModel.IsFullScreen = true;
                break;

            case AppWindowPresenterKind.Overlapped:
                // 正常 - 隐藏系统标题栏 并 使用自定义标题栏。
                AppTitleBar.Visibility = Visibility.Visible;
                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                ViewModel.IsFullScreen = false;
                break;

            case AppWindowPresenterKind.Default:
            default:
                // 使用默认的系统标题栏。
                sender.TitleBar.ResetToDefault();
                sender.TitleBar.ExtendsContentIntoTitleBar = true;
                _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
                ViewModel.IsFullScreen = false;
                break;
        }
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            if (ContentFrame.CurrentSourcePageType == NavigationHelper.GetType("SettingsPage")) return;
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }
        if (args.SelectedItem is not Category selectedCategory) return;
        var path = selectedCategory.Path;
        if (string.IsNullOrEmpty(path)) return;
        var type = NavigationHelper.GetType(path);
        if (ContentFrame.CurrentSourcePageType == type) return;
        ContentFrame.Navigate(type);
    }


    private void NavigationView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is NavigationView navigationView)
        {
            navigationView.SelectedItem = NavigationHelper.Categorizes["Home"];
        }
    }
    // 切换窗口显示模式
    private void FullScreen(object sender, RoutedEventArgs _)
    {
        _appWindow.SetPresenter(ViewModel.IsFullScreen
            ? AppWindowPresenterKind.Default
            : AppWindowPresenterKind.FullScreen);
        if (sender is not Button button) return;
        button.Content = new FontIcon()
        {
            FontSize = 10,
            Glyph = ViewModel.IsFullScreen ? "\uE73F" : "\uE740"
        };
        ToolTipService.SetToolTip(button, ViewModel.IsFullScreen ? "退出全屏" : "全屏");

    }
    private void OnPaneDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs _)
    {
        AppTitleBar.Margin = sender.PaneDisplayMode switch
        {
            NavigationViewPaneDisplayMode.Top => new Thickness(16, 0, 0, 0),
            _ => sender is { DisplayMode: NavigationViewDisplayMode.Minimal }
                ? new Thickness(96, 0, 0, 0)
                : new Thickness(48, 0, 0, 0)
        };
    }
    private void BreadcrumbBar2_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var items = PageTitleBreadcrumbBar.ItemsSource as ObservableCollection<Category>;
        for (var i = items!.Count - 1; i >= args.Index + 1; i--)
        {
            items.RemoveAt(i);
        }

        ContentFrame.Navigate(NavigationHelper.GetType(items[args.Index].Path));
    }

    private void NavigationViewControl_OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        // 当 NavigationView 的返回按钮被点击时，执行 Frame 的返回操作
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    private void MainGrid_OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (!ThemeHelper.IsMicaTheme)
        {
            ThemeHelper.SetWindowBackground();
        }

        TitleBarHelper.ApplySystemThemeToCaptionButtons(this);
    }

}
