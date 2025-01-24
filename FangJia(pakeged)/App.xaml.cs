using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Xaml;
using System;
using System.Reflection;
using Unity;
using Unity.Lifetime;

#pragma warning disable CA1416

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FangJia;

/// <summary>
/// 提供特定于应用程序的行为以补充默认的 Application 类。
/// </summary>
public partial class App
{
    /// <summary>
    /// 初始化单例应用程序对象。这是执行的第一行编写代码，因此是 main() 或 WinMain() 的逻辑等效项。
    /// </summary>
    public App()
    {
        InitializeComponent();

        // 步骤4：初始化服务
        var container = new UnityContainer(); // 创建一个Unity容器
        RegisterServices(container);          // 注册服务

        Locator.Initialize(container);
    }

    /// <summary>
    /// 在启动应用程序时调用。
    /// </summary>
    /// <param name="args">有关启动请求和过程的详细信息。</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {

        Window = new MainWindow();
        WindowHelper.TrackWindow(Window);
        Window.Activate();

        ThemeHelper.Initialize();
        TitleBarHelper.ApplySystemThemeToCaptionButtons(Window);

    }

    private static void RegisterServices(UnityContainer container)
    {
        container.RegisterType<MainPageViewModel>(new ContainerControlledLifetimeManager());
        container.RegisterType<DataViewModel>(new ContainerControlledLifetimeManager());
        container.RegisterType<SettingsViewModel>(new ContainerControlledLifetimeManager());
    }

    public Window? Window { get; private set; }

    public static TEnum GetEnum<TEnum>(string? text) where TEnum : struct
    {
        if (!typeof(TEnum).GetTypeInfo().IsEnum)
        {
            throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
        }
        return (TEnum)Enum.Parse(typeof(TEnum), text!);
    }
}

