
//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Helpers;
using NLog;
using Windows.Storage;

namespace FangJia.ViewModel
{
    public partial class SettingsViewModel : ObservableObject
    {
        private const string CloseModeKey = "MainWindowCloseMode";
        [ObservableProperty] public partial string? ThemeMode { get; set; } = ThemeHelper.RootTheme.ToString();
        [ObservableProperty] public partial bool IsMicaTheme { get; set; } = ThemeHelper.IsMicaTheme;
        [ObservableProperty] public partial string? LogWriteLevel { get; set; } = LogHelper.LogLevel.Name;
        [ObservableProperty] public partial string? CloseMode { get; set; } = ApplicationData.Current.LocalSettings.Values[CloseModeKey]?.ToString() ?? "Default";

        partial void OnIsMicaThemeChanged(bool value)
        {
            ThemeHelper.IsMicaTheme = value;
            ThemeHelper.SetWindowBackground();
        }

        partial void OnLogWriteLevelChanged(string? value)
        {
            LogHelper.LogLevel = LogLevel.FromString(value);
        }

        partial void OnCloseModeChanged(string? value)
        {
            ApplicationData.Current.LocalSettings.Values[CloseModeKey] = value;
        }
    }
}
