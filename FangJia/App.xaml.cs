
//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using Autofac;
using FangJia.DataAccess;
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NLog;
using System.Threading;
using System.Threading.Tasks;



namespace FangJia;

/// <summary>
/// 提供特定于应用程序的行为以补充默认的 Application 类。
/// </summary>
public partial class App
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 获取主线程的 DispatcherQueue。
    /// </summary>
    public static DispatcherQueue? MainDispatcherQueue { get; private set; }

    /// <summary>
    /// 互斥体的名称，用于标识应用程序的唯一实例。
    /// </summary>
    private const string MutexName = "FangJia";

    /// <summary>
    /// 互斥体，用于确保只有一个应用程序实例在运行。
    /// </summary>
    private static Mutex? _mutex;


    /// <summary>
    /// 初始化单例应用程序对象。这是执行的第一行编写代码，因此是 main() 或 WinMain() 的逻辑等效项。
    /// </summary>
    public App()
    {
        InitializeComponent();

        // 1. 注册服务：将服务注册到 Unity 容器中，此操作必须在应用创建时完成。而不能在 OnLaunched 事件中完成。
        var container = BuildContainer(); // 构建容器
        Locator.Initialize(container);

        // 2. 初始化日志：将日志的初始化放在最前面，以确保日志记录器在整个应用程序生命周期内都可用。
        LogHelper.Initialize();

        // 3. 初始化数据：初始化数据管理器，加载数据。
        DataManager.Initialize();
    }

    /// <summary>
    /// 在启动应用程序时调用。
    /// </summary>
    /// <param name="args">有关启动请求和过程的详细信息。</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 取得主线程的 DispatcherQueue
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // 步骤1：检查是否需要重启
        if (!string.IsNullOrEmpty(args.Arguments) && args.Arguments.Contains("ReStart"))
        {
            PipeHelper.StartApp("RESTART");
        }

        // 创建主窗口
        Window = new MainWindow();
        WindowHelper.TrackWindow(Window);

        // 步骤2：检查是否已经有一个实例在运行

        // 创建一个命名互斥体，以确保只有一个应用程序实例在运行。
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // 如果互斥体已经存在，则说明已经有一个实例在运行。
            Logger.Warn("检测到已有实例正在运行，通知已存在的实例");
            PipeHelper.StartApp("SHOW");
            Exit();
            return;
        }

        // 步骤3：启动管道服务端，用于接收来自其他实例的消息。
        Task.Run(PipeHelper.StartPipeServer); // 启动管道服务端

        // 步骤4：启动窗口
        PipeHelper.StartApp("SHOW");

        // 步骤5：初始化主题
        ThemeHelper.Initialize();
        TitleBarHelper.ApplySystemThemeToCaptionButtons(Window);
    }

    /// <summary>
    /// 使用 Autofac 注册服务，并构建容器
    /// </summary>
    /// <returns>构建完成的 Autofac 容器</returns>
    public static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();

        // 注册各个 ViewModel 和 Manager，并指定单例生命周期（等同于 Unity 的 ContainerControlledLifetimeManager）
        builder.RegisterType<MainPageViewModel>().SingleInstance();
        builder.RegisterType<DataViewModel>().SingleInstance();
        builder.RegisterType<SettingsViewModel>().SingleInstance();
        builder.RegisterType<FormulationViewModel>().SingleInstance();
        builder.RegisterType<DrugViewModel>().SingleInstance();

        builder.RegisterType<FormulationManager>().InstancePerLifetimeScope();

        return builder.Build();
    }

    public static Window? Window { get; private set; }
    public static bool HandleClosedEvents { get; set; } = true;

    /// <summary>
    /// 释放资源。
    /// </summary>
    ~App()
    {
        // 释放互斥体
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}