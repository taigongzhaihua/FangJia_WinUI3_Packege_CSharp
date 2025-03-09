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
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using WinRT.Interop;

namespace FangJia.Helpers;
// NavigationHelper 类用于帮助应用程序查找包含特定 UIElement 的窗口（使用 GetWindowForElement 方法）。
// 为实现这一目的，我们会追踪所有活动窗口。
// 因此，应用程序代码必须调用 WindowHelper.CreateWindow 而不是直接实例化 "new Window" ，以便我们能追踪到所有相关窗口。
// 未来，我们希望在平台 API 中对这一功能提供原生支持。

public class WindowHelper
{
    /// <summary>
    /// 创建一个新的窗口。
    /// </summary>
    /// <returns></returns>
    public static Window? CreateWindow()
    {
        var newWindow = new Window
        {
            SystemBackdrop = new MicaBackdrop()
        };
        TrackWindow(newWindow);
        return newWindow;
    }

    /// <summary>
    /// 追踪窗口。
    /// </summary>
    /// <param name="window"></param>
    public static void TrackWindow(Window? window)
    {
        if (window == null) return;
        window.Closed += (_, _) => { ActiveWindows.Remove(window); };
        ActiveWindows.Add(window);
    }

    /// <summary>
    /// 获取指定窗口的 AppWindow 对象。
    /// </summary>
    /// <param name="window"></param>
    /// <returns></returns>
    public static AppWindow GetAppWindow(Window window)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(wndId);
    }

    /// <summary>
    /// 获取包含指定 UIElement 的窗口。
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static Window? GetWindowForElement(UIElement element)
    {
        return element.XamlRoot != null ? ActiveWindows.FirstOrDefault(window => element.XamlRoot == window?.Content.XamlRoot) : null;
    }


    /// <summary>
    /// 获取指定 UIElement 的栅格化比例。
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    public static double GetRasterizationScaleForElement(UIElement element)
    {
        if (element.XamlRoot == null) return 0.0;
        return ActiveWindows.Any(window => element.XamlRoot == window?.Content.XamlRoot) ? element.XamlRoot.RasterizationScale : 0.0;
    }

    /// <summary>
    /// 获取或设置活动窗口列表。
    /// </summary>
    public static List<Window?> ActiveWindows { get; } = [];

    /// <summary>
    /// 获取或设置应用程序的本地文件夹。
    /// </summary>
    public static StorageFolder GetAppLocalFolder()
    {
        var localFolder = !NativeHelper.IsAppPackaged ? Task.Run(async () => await StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory)).Result : ApplicationData.Current.LocalFolder;
        return localFolder;
    }
}