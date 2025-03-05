//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------


using Microsoft.Data.Sqlite;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Windows.Storage;
using TGZH.Control;


namespace FangJia.Helpers;
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public static class LogHelper
{
    private const string LogLevelKey = "LogLevel";
    private static readonly Logger Logger = GetLogger(typeof(LogHelper).FullName);
    public static readonly string DatabasePath = AppHelper.GetFilePath("Log.db");


    public static void Initialize()
    {
        CreateDatabaseIfNotExists();
        ConfigureLogging(LogLevel);
    }

    public static void CreateDatabaseIfNotExists()
    {
        // 检查文件是否存在
        if (!File.Exists(DatabasePath))
        {
            Logger.Info($"未找到数据库文件\"{DatabasePath}\"。");
            Logger.Info("正在创建新数据库文件和日志表...");

            // 创建数据库文件并初始化表
            CreateDatabaseAndTable(DatabasePath);

            Logger.Info("数据库文件和日志表创建成功。");
        }
        else
        {
            Logger.Info($"数据库文件\"{DatabasePath}\"已存在。");
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
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Logs (
                TimestampUtc INTEGER DEFAULT (strftime('%s', 'now') * 1000 + (strftime('%f', 'now') - strftime('%S', 'now')) * 1000),
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
    /// <summary>
    /// 配置 NLog 日志目标和规则。
    /// </summary>
    /// <param name="minLevel">初始最小日志级别。</param>
    private static void ConfigureLogging(LogLevel minLevel)
    {
        try
        {
            var config = new LoggingConfiguration();
#if DEBUG
            // 日志格式(layout)，与 XML 配置保持一致
            const string layout =
                "[${longdate}] [${level:uppercase=true}] ${logger}\t${message}\t${exception}";
            const string stacktrace = "${stacktrace}";
#endif


            // 1. 配置 SQLite 数据库目标 (NLog.Database)
            var dbTarget = new DatabaseTarget("SQLiteDB")
            {
                // SQLite 连接字符串，根据需要修改数据库文件路径或连接方式
                ConnectionString = $"Data Source=\"{DatabasePath}\";",
                DBProvider = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite", // 使用 SQLite 提供程序
                CommandText = """
                              insert into Logs (TimestampUtc, Application, Level, Message, Exception, Logger, EventId)
                              values ((strftime('%s', @timestamputc) * 1000) + (strftime('%f', @timestamputc) - strftime('%S', @timestamputc)) * 1000, @application, @level, @message, @exception, @logger, @EventId);
                              """

            };
            // 设置参数，对应上述 CommandText 中的占位符
            dbTarget.Parameters.Add(new DatabaseParameterInfo("@timestamputc", "${longdate}"));
            dbTarget.Parameters.Add(new DatabaseParameterInfo("@application", "FangJia"));
            dbTarget.Parameters.Add(new DatabaseParameterInfo("@level", "${uppercase:${level}}"));
            dbTarget.Parameters.Add(new DatabaseParameterInfo("@logger", "${logger}"));
            dbTarget.Parameters.Add(new DatabaseParameterInfo("@message", "${message}"));
            dbTarget.Parameters.Add(new DatabaseParameterInfo("@exception", "${exception:format=tostring}"));
            dbTarget.Parameters.Add(new DatabaseParameterInfo("@EventId",
                "${event-properties:item=EventId_Id:whenEmpty=0}"));
#if DEBUG
            // 2. 配置 Debug 输出目标 (使用 MethodCallTarget 调用 Debug.WriteLine)
            var debugTarget = new MethodCallTarget("debugOutput")
            {
                Parameters =
                {
                    new MethodCallParameter("logMessage", layout) ,
                    new MethodCallParameter("stacktrace", stacktrace),
                    new MethodCallParameter("level", "${uppercase:${level}}")
                }, // 使用相同的layout格式生成日志消息文本
                ClassName = typeof(LogHelper).FullName + ", FangJia", // 指定静态方法所在类
                MethodName = nameof(WriteToDebug) // 指定要调用的静态方法名
            };
#endif
            // 3. 注册我们将在本类中定义的静态方法，以匹配 MethodCallTarget 调用签名
            config.AddTarget(dbTarget);
#if DEBUG
            config.AddTarget(debugTarget);
#endif
            // 4. 设置日志规则：所有 Logger 名称 (*) 从指定级别及以上的日志，写入上述两个目标
            var rule = new LoggingRule("*", minLevel, dbTarget);
            config.LoggingRules.Add(rule);
#if DEBUG
            var debugRule = new LoggingRule("*", minLevel, debugTarget);
            config.LoggingRules.Add(debugRule);
#endif
            // 5. 应用配置
            LogManager.Configuration = config;
            LogManager.ThrowConfigExceptions = true;
            LogManager.ThrowExceptions = true;
            NLog.Common.InternalLogger.LogFile = AppHelper.GetFilePath("nlog-internal.log");
            NLog.Common.InternalLogger.LogLevel = LogLevel.Warn;
            NLog.Common.InternalLogger.LogToConsole = true;
            NLog.Common.InternalLogger.IncludeTimestamp = true;
            // 6. 重新配置现有的 Logger
            LogManager.ReconfigExistingLoggers();
        }
        catch (NLogConfigurationException exception)
        {
            Logger.Error(exception.Message, exception);
        }
        catch (Exception exception)
        {
            Logger.Error(exception);
        }
    }
#if DEBUG


    /// <summary>
    /// 提供给 MethodCallTarget 调用的静态方法。
    /// 根据日志级别选择控制台前景色并输出日志到 Debug。
    /// </summary>
    public static void WriteToDebug(string logMessage, string stacktrace, string level)
    {

        // 输出日志到调试控制台
        Debug.WriteLine(logMessage);
        switch (level)
        {
            case "WARM" or "ERROR" or "FATAL":
                Debug.WriteLine(stacktrace);
                break;
        }
    }
#endif

    public static LogLevel LogLevel
    {
        get =>
            // 从 LocalSettings 加载日志级别
            ApplicationData.Current.LocalSettings.Values.TryGetValue(LogLevelKey, out var logLevelValue)
                ? LogLevel.FromString(logLevelValue as string ?? "Debug")
                : LogLevel.Trace;
        set
        {
            var config = LogManager.Configuration;

            if (config != null)
            {
                foreach (var rule in config.LoggingRules)
                {
                    rule.SetLoggingLevels(value, LogLevel.Fatal);
                }

                ApplicationData.Current.LocalSettings.Values[LogLevelKey] = value.Name;

                LogManager.ReconfigExistingLoggers();
                Logger.Info($@"日志级别已设置为: {value.Name}");
            }
            else
            {
                Logger.Error(@"未找到 NLog 配置。");
            }
        }
    }

    public static async IAsyncEnumerable<LogItem> GetLogsAsync(long? startTime, List<string> logLevels)
    {
        var connectionString = $"Data Source=\"{DatabasePath}\";";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // 基础查询语句
        var query = "SELECT TimestampUtc, Application, Level, Message, Exception, Logger, EventId FROM Logs";

        // 用来存放各个过滤条件
        var conditions = new List<string>();

        // 如果提供了开始时间，就添加 TimestampUtc 的过滤条件
        if (startTime.HasValue)
        {
            conditions.Add("TimestampUtc >= $startTime");
            command.Parameters.AddWithValue("$startTime", startTime.Value);
        }

        // 如果提供了日志级别，并且列表中至少有一个级别，就添加 Level 的过滤条件
        if (logLevels is { Count: > 0 })
        {
            // 为了参数化，动态生成参数名
            var levelParams = new List<string>();
            for (var i = 0; i < logLevels.Count; i++)
            {
                var paramName = $"$logLevel{i}";
                levelParams.Add(paramName);
                command.Parameters.AddWithValue(paramName, logLevels[i]);
            }
            // 添加 IN 条件
            conditions.Add($"Level IN ({string.Join(", ", levelParams)})");
        }

        // 如果有任何条件，则把它们拼接到查询语句中
        if (conditions.Count > 0)
        {
            query += " WHERE " + string.Join(" AND ", conditions);
        }

        // 排序（根据需要可以调整排序顺序）
        query += " ORDER BY TimestampUtc DESC";

        command.CommandText = query;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            yield return new LogItem
            {
                TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)).DateTime,
                Application = reader.GetString(1),
                Level = reader.GetString(2),
                Message = reader.GetString(3),
                Exception = reader.IsDBNull(4) ? null : reader.GetString(4),
                Logger = reader.IsDBNull(5) ? null : reader.GetString(5),
                EventId = reader.GetInt32(6)
            };
        }
    }
    /// <summary>
    /// 获取 NLog 的 Logger 对象，供外部记录日志使用。
    /// </summary>
    public static Logger GetLogger(string? className)
    {
        return LogManager.GetLogger(className);
    }
}
