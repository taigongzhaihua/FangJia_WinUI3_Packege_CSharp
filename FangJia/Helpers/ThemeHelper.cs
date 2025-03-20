//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------


using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using WinRT;

namespace FangJia.Helpers;

/// <summary>
/// Class providing functionality around switching and restoring theme settings
/// </summary>
public static class ThemeHelper
{
    private const string SelectedAppThemeKey = "SelectedAppTheme";
    private const string IsMicaThemeKey = "IsMicaTheme";

    /// <summary>
    /// Gets the current actual theme of the app based on the requested theme of the
    /// root element, or if that value is Default, the requested theme of the Application.
    /// </summary>
    public static ElementTheme ActualTheme
    {
        get
        {
            foreach (var window in WindowHelper.ActiveWindows)
            {
                if (window?.Content is not FrameworkElement rootElement) continue;
                if (rootElement.RequestedTheme != ElementTheme.Default)
                {
                    return rootElement.RequestedTheme;
                }
            }

            return AppHelper.GetEnum<ElementTheme>(Application.Current.RequestedTheme.ToString());
        }
    }

    /// <summary>
    /// Gets or sets (with LocalSettings persistence) the RequestedTheme of the root element.
    /// </summary>
    public static ElementTheme RootTheme
    {
        get
        {
            foreach (var window in WindowHelper.ActiveWindows)
            {
                if (window?.Content is FrameworkElement rootElement)
                {
                    return rootElement.RequestedTheme;
                }
            }

            return ElementTheme.Default;
        }
        set
        {
            foreach (var window in WindowHelper.ActiveWindows)
            {
                if (window?.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = value;
                }
            }

            if (NativeHelper.IsAppPackaged)
            {
                ApplicationData.Current.LocalSettings.Values[SelectedAppThemeKey] = value.ToString();
            }
        }
    }

    public static bool IsMicaTheme
    {
        get
        {
            var x = ApplicationData.Current.LocalSettings.Values[IsMicaThemeKey]?.As<bool>();
            return x ?? false;
        }
        set
        {
            if (NativeHelper.IsAppPackaged)
            {
                ApplicationData.Current.LocalSettings.Values[IsMicaThemeKey] = value;
            }
        }
    }

    public static void Initialize()
    {
        if (!NativeHelper.IsAppPackaged) return;
        IsMicaTheme = ApplicationData.Current.LocalSettings.Values[IsMicaThemeKey]?.As<bool>() ?? false;

        if (IsMicaTheme)
        {
            SetWindowBackground();
        }

        var savedTheme = ApplicationData.Current.LocalSettings.Values[SelectedAppThemeKey]?.ToString();

        if (savedTheme != null)
        {
            RootTheme = AppHelper.GetEnum<ElementTheme>(savedTheme);
        }

    }

    public static bool IsDarkTheme()
    {
        if (RootTheme == ElementTheme.Default)
        {
            return Application.Current.RequestedTheme == ApplicationTheme.Dark;
        }

        return RootTheme == ElementTheme.Dark;
    }

    public static void SetWindowBackground()
    {
        foreach (var window in WindowHelper.ActiveWindows)
        {
            var panel = window?.Content as Panel;
            if (IsMicaTheme)
            {
                window!.SystemBackdrop = new MicaBackdrop
                {
                    Kind = MicaKind.Base
                };
                panel!.Background = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                window!.SystemBackdrop = null;
                panel!.Background = IsDarkTheme()
                    ? new SolidColorBrush(Colors.Black)
                    : new SolidColorBrush(Colors.White);
            }
        }
    }
}