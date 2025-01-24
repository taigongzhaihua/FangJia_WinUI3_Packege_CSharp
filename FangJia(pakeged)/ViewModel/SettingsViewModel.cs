using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Helpers;

namespace FangJia.ViewModel
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string? _themeMode;
        [ObservableProperty] private bool _isMicaTheme;
        public SettingsViewModel()
        {
            ThemeMode = ThemeHelper.RootTheme.ToString();
            IsMicaTheme = ThemeHelper.IsMicaTheme;
        }

        partial void OnIsMicaThemeChanged(bool value)
        {
            ThemeHelper.IsMicaTheme = value;
            ThemeHelper.SetWindowBackground();
        }
    }
}
