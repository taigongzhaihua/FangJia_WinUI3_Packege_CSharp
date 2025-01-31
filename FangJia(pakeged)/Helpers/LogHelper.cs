using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;


namespace FangJia.Helpers;

public static class LogHelper
{
    private const string LogLevelKey = "LogLevel";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log.db");

    public static void Initialize()
    {
        LogLevel = LogLevel.FromString(ApplicationData.Current.LocalSettings.Values[LogLevelKey] as string ?? "Trace");
        CreateDatabaseIfNotExists();
    }

    public static void CreateDatabaseIfNotExists()
    {
        // 检查文件是否存在
        if (!File.Exists(DatabasePath))
        {
            Logger.Info("未找到数据库文件。正在创建新数据库...");

            // 创建数据库文件并初始化表
            CreateDatabaseAndTable(DatabasePath);

            Logger.Info("数据库文件和日志表创建成功。");
        }
        else
        {
            Logger.Info("数据库文件已存在。");
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
                ? LogLevel.FromString(logLevelValue as string ?? "Trace")
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

    public static List<LogItem> GetLogs(DateTime? startDate, DateTime? endDate, string? level = null)
    {
        var logs = new List<LogItem>();
        var connectionString = $"Data Source=\"{DatabasePath}\";";

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var command = connection.CreateCommand();

        // 构建 SQL 查询语句
        command.CommandText = "SELECT TimestampUtc, Application, Level, Message, Exception, Logger, EventId FROM Logs WHERE 1=1";

        if (startDate.HasValue)
        {
            command.CommandText += " AND TimestampUtc >= @StartDate";
            command.Parameters.AddWithValue("@StartDate", new DateTimeOffset(startDate.Value).ToUnixTimeMilliseconds());
        }

        if (endDate.HasValue)
        {
            command.CommandText += " AND TimestampUtc <= @EndDate";
            command.Parameters.AddWithValue("@EndDate", new DateTimeOffset(endDate.Value).ToUnixTimeMilliseconds());
        }

        if (!string.IsNullOrEmpty(level))
        {
            command.CommandText += " AND Level = @Level";
            command.Parameters.AddWithValue("@Level", level);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(new LogItem
            {
                TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)).UtcDateTime,
                Application = reader.GetString(1),
                Level = reader.GetString(2),
                Message = reader.GetString(3),
                Exception = reader.IsDBNull(4) ? null : reader.GetString(4),
                Logger = reader.IsDBNull(5) ? null : reader.GetString(5),
                EventId = reader.GetInt32(6)
            });
        }

        return logs;
    }
}

public class LogItem
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
        return $"[{TimestampUtc:yyyy-MM-dd HH:mm:ss.fffz}] [{Level}] {Logger}\t：{Message}:{Exception}";
    }
}