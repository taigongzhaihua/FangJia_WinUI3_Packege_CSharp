using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NLog;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static DispatcherQueue? MainDispatcherQueue { get; private set; }
    /// <summary>
    /// 互斥体的名称，用于标识应用程序的唯一实例。
    /// </summary>
    private const string MutexName = "FangJia";
    private static Mutex? _mutex;
    /// <summary>
    /// 初始化单例应用程序对象。这是执行的第一行编写代码，因此是 main() 或 WinMain() 的逻辑等效项。
    /// </summary>
    public App()
    {
        InitializeComponent();

        // 步骤4：初始化服务
        var container = new UnityContainer(); // 创建一个Unity容器
        RegisterServices(container); // 注册服务

        Locator.Initialize(container);
    }

    /// <summary>
    /// 在启动应用程序时调用。
    /// </summary>
    /// <param name="args">有关启动请求和过程的详细信息。</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (!string.IsNullOrEmpty(args.Arguments) && args.Arguments.Contains("ReStart"))
        {
            PipeHelper.StartApp("RESTART");
        }

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
        PipeHelper.StartApp("SHOW");
        ThemeHelper.Initialize();
        TitleBarHelper.ApplySystemThemeToCaptionButtons(Window);

        // 数据库文件路径
        var databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.db");

        // 检查文件是否存在
        if (!File.Exists(databasePath))
        {
            Logger.Info(@"Database file not found. Creating new database...");

            // 创建数据库文件并初始化表
            CreateDatabaseAndTable(databasePath);

            Logger.Info(@"Database file and Logs table created successfully.");
        }
        else
        {
            Logger.Info(@"Database file already exists.");
        }

    }

    private static void CreateDatabaseAndTable(string databasePath)
    {
        // SQLite 连接字符串
        var connectionString = $"Data Source=\"{databasePath}\";";

        _ = Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? string.Empty);

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var command = connection.CreateCommand();

        // 创建表的 SQL 语句
        command.CommandText = """
                              
                                              CREATE TABLE IF NOT EXISTS Logs (
                                                  TimestampUtc TEXT NOT NULL,
                                                  Application TEXT NOT NULL,
                                                  Level TEXT NOT NULL,
                                                  Message TEXT NOT NULL,
                                                  Exception TEXT,
                                                  Logger TEXT,
                                                  EventId INTEGER DEFAULT 0
                                              )
                              """;


        command.ExecuteNonQuery();
    }

    private static void RegisterServices(UnityContainer container)
    {
        container.RegisterType<MainPageViewModel>(new ContainerControlledLifetimeManager());
        container.RegisterType<DataViewModel>(new ContainerControlledLifetimeManager());
        container.RegisterType<SettingsViewModel>(new ContainerControlledLifetimeManager());
    }

    public static Window? Window { get; private set; }

    public static TEnum GetEnum<TEnum>(string? text) where TEnum : struct
    {
        if (!typeof(TEnum).GetTypeInfo().IsEnum)
        {
            throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
        }

        return (TEnum)Enum.Parse(typeof(TEnum), text!);
    }

    ~App()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}