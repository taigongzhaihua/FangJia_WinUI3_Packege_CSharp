// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using CommunityToolkit.WinUI.Controls;
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics.CodeAnalysis;

namespace FangJia.Pages;

/// <summary>
/// 设置页面类，可单独使用或在 Frame 中导航使用。
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public sealed partial class SettingsPage
{
    private SettingsViewModel ViewModel { get; }
    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = Locator.GetService<SettingsViewModel>();
    }

    private void ThemeMode_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var selectedTheme = ((ComboBoxItem)ThemeMode.SelectedItem)?.Tag?.ToString();
        var window = WindowHelper.GetWindowForElement(this);
        if (selectedTheme == null) return;
        ThemeHelper.RootTheme = App.GetEnum<ElementTheme>(selectedTheme);
        string color;
        switch (selectedTheme)
        {
            case "Dark":
                TitleBarHelper.SetCaptionButtonColors(window, Colors.White);
                color = selectedTheme;
                break;
            case "Light":
                TitleBarHelper.SetCaptionButtonColors(window, Colors.Black);
                color = selectedTheme;
                break;
            default:
                color = TitleBarHelper.ApplySystemThemeToCaptionButtons(window) == Colors.White ? "Dark" : "Light";
                break;
        }
        // announce visual change to automation
        UiHelper.AnnounceActionForAccessibility(sender as UIElement, $"Theme changed to {color}",
            "ThemeChangedNotificationActivityId");
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        PipeHelper.RestartApp();
    }

    private void ButtonLog_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is SettingsCard card)
        {
            Frame.Navigate(NavigationHelper.GetType(card.Tag.ToString()));
        }
    }
}