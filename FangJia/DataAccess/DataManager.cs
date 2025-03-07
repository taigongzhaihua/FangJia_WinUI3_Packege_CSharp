//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using FangJia.Helpers;
using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

internal partial class DataManager : IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string DatabasePath = AppHelper.GetFilePath("Data.db");
    private static readonly string ConnectionString = $"Data Source={DatabasePath};";
    public static readonly SqliteConnectionPool Pool = new(ConnectionString, 20);

    /// <summary>
    /// 初始化数据库，并创建表（如果不存在）
    /// </summary>
    public static void Initialize()
    {
        Logger.Debug($"查找数据库文件。路径：\"{DatabasePath}\"");
        // 检查文件是否存在
        if (!File.Exists(DatabasePath))
        {
            Logger.Info("未找到数据库文件。正在创建新数据库文件和表...");
            // 创建数据库文件并初始化表
            CreateDatabaseAndTable(DatabasePath);
            Logger.Info("数据库文件和表创建成功。");
        }
        else
        {
            Logger.Info("数据库文件已存在。");
        }
    }

    private static void CreateDatabaseAndTable(string databasePath)
    {
        // SQLite 连接字符串
        _ = Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? string.Empty);
        using var poolConnection = Pool.GetConnectionAsync().Result;
        var connection = poolConnection.Connection;
        connection.Open();
        var command = connection.CreateCommand();
        // 创建表的 SQL 语句
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Category(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstCategory TEXT NOT NULL,
                SecondCategory TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Drug (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT,
                EnglishName TEXT,
                LatinName   TEXT,
                Category    TEXT,
                Origin      TEXT,
                Properties  TEXT,
                Quality     TEXT,
                Taste       TEXT,
                Meridian    TEXT,
                Effect      TEXT,
                Notes       TEXT,
                Processed   TEXT,
                Source      TEXT
            );
            CREATE TABLE IF NOT EXISTS DrugImage (
                Id     INTEGER PRIMARY KEY AUTOINCREMENT,
                DrugId INTEGER,
                Image  BLOB,
                FOREIGN KEY (
                    DrugId
                )
                REFERENCES Drug (Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS Formulation (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT,
                CategoryId  INTEGER,
                Usage       TEXT,
                Effect      TEXT,
                Indication  TEXT,
                Disease     TEXT,
                Application TEXT,
                Supplement  TEXT,
                Song        TEXT,
                Notes       TEXT,
                Source      TEXT,
                FOREIGN KEY (
                    CategoryId
                )
                REFERENCES Category (Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS FormulationComposition (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                FormulationId INTEGER,
                DrugID        INTEGER REFERENCES Drug (Id),
                DrugName      TEXT,
                Effect        TEXT,
                Position      TEXT,
                Notes         TEXT,
                FOREIGN KEY (
                    FormulationId
                )
                REFERENCES Formulation (Id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS FormulationImage (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                FormulationId INTEGER,
                Image         BLOB,
                FOREIGN KEY (
                    FormulationId
                )
                REFERENCES Formulation (Id) ON DELETE CASCADE
            );
            """;
        command.ExecuteNonQuery();
    }

    public static async Task<(SqliteCommand command, PooledSqliteConnection pooledConnection)> CreateCommandAsync(string commandText, CancellationToken cancellationToken = default)
    {
        var pooledConnection = await Pool.GetConnectionAsync(cancellationToken);
        var connection = pooledConnection.Connection;
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (command, pooledConnection);
    }
    /// <summary>
    /// 清理连接池中的空闲连接
    /// </summary>
    public static Task CleanIdleConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return Pool.CleanIdleConnectionsAsync(cancellationToken);
    }

    public void Dispose()
    {
        Pool.Dispose();
        GC.SuppressFinalize(this);
    }
}