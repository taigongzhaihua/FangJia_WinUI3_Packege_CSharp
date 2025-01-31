using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Helpers;
using NLog;

namespace FangJia.ViewModel
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string? _themeMode;
        [ObservableProperty] private bool _isMicaTheme;
        [ObservableProperty] private string? _logWriteLevel;
        public SettingsViewModel()
        {
            ThemeMode = ThemeHelper.RootTheme.ToString();
            IsMicaTheme = ThemeHelper.IsMicaTheme;
            LogWriteLevel = LogHelper.LogLevel.Name;
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
    }
}
