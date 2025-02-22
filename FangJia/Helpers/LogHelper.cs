using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using WinRT;


namespace FangJia.Helpers;

public static class LogHelper
{
    private const string LogLevelKey = "LogLevel";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log.db");

    public static void Initialize()
    {
        LogLevel = LogLevel.FromString(ApplicationData.Current.LocalSettings.Values[LogLevelKey] as string ?? "Debug");
        CreateDatabaseIfNotExists();
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
}
[GeneratedBindableCustomProperty]
public partial class LogItem
{
    public DateTime TimestampUtc { get; set; }
    public string? Application { get; set; }
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? Exception { get; set; }
    public string? Logger { get; set; }
    public int EventId { get; set; }

    public override string ToString()
    {
        var l = $"[{TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Logger}\t：{Message}";
        if (!string.IsNullOrWhiteSpace(Exception)) l += $" - {Exception}";
        return l;
    }
}