using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.IO;

namespace FangJia.DataAccess;

internal class DataManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data.db");
    private static readonly string ConnectionString = $"Data Source={DatabasePath};";

    /// <summary>
    /// 初始化数据库，并创建表（如果不存在）
    /// </summary>
    public static void Initialize()
    {
        // 检查文件是否存在
        if (!File.Exists(DatabasePath))
        {
            Logger.Info($"未找到数据库文件\"{DatabasePath}\"。");
            Logger.Info("正在创建新数据库文件和表...");
            // 创建数据库文件并初始化表
            CreateDatabaseAndTable(DatabasePath);
            Logger.Info("数据库文件和表创建成功。");
        }
        else
        {
            Logger.Info($"数据库文件\"{DatabasePath}\"已存在。");
        }
    }

    private static void CreateDatabaseAndTable(string databasePath)
    {
        // SQLite 连接字符串
        _ = Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? string.Empty);
        using var connection = new SqliteConnection(ConnectionString);
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
}