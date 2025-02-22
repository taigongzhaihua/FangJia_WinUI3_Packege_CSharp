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
