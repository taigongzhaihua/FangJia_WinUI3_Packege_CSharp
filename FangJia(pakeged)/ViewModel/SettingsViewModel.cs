using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Helpers;
using NLog;
using Windows.Storage;

namespace FangJia.ViewModel
{
    public partial class SettingsViewModel : ObservableObject
    {
        private const string CloseModeKey = "MainWindowCloseMode";
        [ObservableProperty] private string? _themeMode;
        [ObservableProperty] private bool _isMicaTheme = ThemeHelper.IsMicaTheme;
        [ObservableProperty] private string? _logWriteLevel;
        [ObservableProperty] private string? _closeMode;
        public SettingsViewModel()
        {
            ThemeMode = ThemeHelper.RootTheme.ToString();
            LogWriteLevel = LogHelper.LogLevel.Name;
            CloseMode = ApplicationData.Current.LocalSettings.Values[CloseModeKey]?.ToString() ?? "Default";
        }

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
