using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace FangJia.Helpers
{

    internal class TitleBarHelper
    {
        // workaround as AppWindow title bar doesn't update caption button colors correctly when changed while app is running
        // 解决方法，因为在应用程序运行时更改时，AppWindow 标题栏不会正确更新标题按钮颜色
        // https://task.ms/44172495

        public static Windows.UI.Color ApplySystemThemeToCaptionButtons(Window? window)
        {
            var color = ThemeHelper.ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black;
            SetCaptionButtonColors(window, color);
            SetCaptionButtonHoverForegroundColor(window, color);
            SetCaptionButtonPressedForegroundColor(window, color);

            var hoverColor = ThemeHelper.IsDarkTheme()
                ? Color.FromArgb(48, 255, 255, 255)
                : Color.FromArgb(12, 0, 0, 0);
            SetCaptionButtonHoverBackgroundColor(window, hoverColor);

            var pressedColor = ThemeHelper.IsDarkTheme()
                ? Color.FromArgb(96, 255, 255, 255)
                : Color.FromArgb(24, 0, 0, 0);
            SetCaptionButtonPressedBackgroundColor(window, pressedColor);


            return color;
        }

        public static void SetCaptionButtonColors(Window? window, Windows.UI.Color color)
        {
            if (window == null) return;
            window.AppWindow.TitleBar.ButtonForegroundColor = color;
            window.AppWindow.TitleBar.ForegroundColor = color;
        }

        public static void SetCaptionButtonBackgroundColors(Window window, Windows.UI.Color? color)
        {
            var titleBar = window.AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = color;
        }

        public static void SetForegroundColor(Window window, Windows.UI.Color? color)
        {
            var titleBar = window.AppWindow.TitleBar;
            titleBar.ForegroundColor = color;
        }

        public static void SetBackgroundColor(Window window, Windows.UI.Color? color)
        {
            var titleBar = window.AppWindow.TitleBar;
            titleBar.BackgroundColor = color;
        }

        public static void SetCaptionButtonHoverBackgroundColor(Window? window, Windows.UI.Color? color)
        {
            if (window == null) return;
            var titleBar = window.AppWindow.TitleBar;
            titleBar.ButtonHoverBackgroundColor = color;
        }
        public static void SetCaptionButtonPressedBackgroundColor(Window? window, Windows.UI.Color? color)
        {
            if (window == null) return;
            var titleBar = window.AppWindow.TitleBar;
            titleBar.ButtonPressedBackgroundColor = color;
        }

        public static void SetCaptionButtonHoverForegroundColor(Window? window, Windows.UI.Color? color)
        {
            if (window == null) return;
            var titleBar = window.AppWindow.TitleBar;
            titleBar.ButtonHoverForegroundColor = color;
        }
        public static void SetCaptionButtonPressedForegroundColor(Window? window, Windows.UI.Color? color)
        {
            if (window == null) return;
            var titleBar = window.AppWindow.TitleBar;
            titleBar.ButtonPressedForegroundColor = color;
        }
    }
}
