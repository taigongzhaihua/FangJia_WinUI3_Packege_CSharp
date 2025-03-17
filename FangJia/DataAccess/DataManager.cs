//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using FangJia.DataAccess.Sql;
using FangJia.Helpers;
using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

/// <summary>
/// 数据管理器：提供数据库访问和管理功能
/// </summary>
internal partial class DataManager : IAsyncDisposable, IDisposable
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly string DatabasePath = AppHelper.GetFilePath("Data.db");
    private static readonly string ConnectionString = GetConnectionString();

    // 保留静态连接池引用，以兼容现有代码
    public static readonly SqliteConnectionPool Pool;

    private static readonly Lazy<DataManager> _instance = new(() => new DataManager());
    private readonly SqliteConnectionPool _pool;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 静态构造函数，初始化静态连接池
    /// </summary>
    static DataManager()
    {
        Pool = new SqliteConnectionPool(ConnectionString, 20);
    }

    /// <summary>
    /// 获取数据管理器实例
    /// </summary>
    public static DataManager Instance => _instance.Value;

    /// <summary>
    /// 获取数据库是否已初始化
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 私有构造函数，防止外部直接实例化
    /// </summary>
    private DataManager()
    {
        // 使用静态池引用，避免创建两个池实例
        _pool = Pool;
    }

    /// <summary>
    /// 获取优化的SQLite连接字符串
    /// </summary>
    private static string GetConnectionString()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false, // 我们使用自定义连接池，禁用ADO.NET内置连接池
            ForeignKeys = true
        };
        return builder.ToString();
    }

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

    /// <summary>
    /// 创建数据库和表结构
    /// </summary>
    private static void CreateDatabaseAndTable(string databasePath)
    {
        // 确保目录存在
        _ = Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? string.Empty);

        // 获取连接并创建表结构
        var pooledConnection = Task.Run(async () => await Pool.GetConnectionAsync()).GetAwaiter().GetResult();

        using (pooledConnection)
        {
            var connection = pooledConnection.Connection;
            connection.Open();
            var command = connection.CreateCommand();
            // 使用集中管理的SQL语句创建表结构
            command.CommandText = SqlQueries.Database.CreateTables;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// 异步初始化数据库
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsInitialized) return;

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsInitialized) return;

            Logger.Debug($"正在初始化数据库。路径：\"{DatabasePath}\"");

            var directoryPath = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Logger.Debug($"已创建数据库目录：{directoryPath}");
            }

            var fileExists = File.Exists(DatabasePath);
            if (!fileExists)
            {
                Logger.Info("未找到数据库文件。正在创建新数据库文件...");
            }

            // 创建或升级数据库结构
            await CreateOrUpdateDatabaseSchemaAsync(cancellationToken).ConfigureAwait(false);

            // 执行优化配置
            await ExecuteDatabaseOptimizationAsync(cancellationToken).ConfigureAwait(false);

            IsInitialized = true;
            Logger.Info($"数据库初始化完成。位置：{DatabasePath}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "数据库初始化失败");
            throw new InvalidOperationException("无法初始化数据库", ex);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// 创建或更新数据库架构
    /// </summary>
    private async Task CreateOrUpdateDatabaseSchemaAsync(CancellationToken cancellationToken)
    {
        // 使用集中管理的SQL语句创建表结构
        await using var cmdResult = await ExecuteCommandAsync(SqlQueries.Database.CreateTables, cancellationToken).ConfigureAwait(false);
        Logger.Info("数据库表结构创建或更新成功");
    }

    /// <summary>
    /// 执行数据库优化设置
    /// </summary>
    private async Task ExecuteDatabaseOptimizationAsync(CancellationToken cancellationToken)
    {
        // 使用集中管理的SQL语句应用性能优化配置
        await using var cmdResult = await ExecuteCommandAsync(SqlQueries.Database.OptimizationPragmas, cancellationToken).ConfigureAwait(false);
        Logger.Debug("已应用数据库性能优化配置");
    }

    /// <summary>
    /// 异步创建命令并执行
    /// </summary>
    /// <param name="commandText">SQL命令文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令和连接包装器</returns>
    public async Task<CommandResult> ExecuteCommandAsync(string commandText, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsInitialized && !commandText.Contains("PRAGMA") && !commandText.Contains("CREATE TABLE"))
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        var pooledConnection = await _pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = pooledConnection.Connection;
            var command = connection.CreateCommand();
            command.CommandText = commandText;
            return new CommandResult(command, pooledConnection);
        }
        catch (Exception)
        {
            // 如果创建命令失败，确保释放连接
            await pooledConnection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 创建命令对象和连接（静态兼容方法）
    /// </summary>
    public static async Task<(SqliteCommand command, PooledSqliteConnection pooledConnection)> CreateCommandAsync(
        string commandText, CancellationToken cancellationToken = default)
    {
        var pooledConnection = await Pool.GetConnectionAsync(cancellationToken);
        var connection = pooledConnection.Connection;
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (command, pooledConnection);
    }

    /// <summary>
    /// 异步创建带参数的命令
    /// </summary>
    /// <param name="commandText">SQL命令文本</param>
    /// <param name="parameters">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令和连接包装器</returns>
    public async Task<CommandResult> ExecuteCommandAsync(string commandText,
        IEnumerable<(string name, object? value)> parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCommandAsync(commandText, cancellationToken).ConfigureAwait(false);
        foreach (var (name, value) in parameters)
        {
            result.Command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        return result;
    }

    /// <summary>
    /// 异步执行查询并返回结果集的第一个值
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="commandText">SQL命令文本</param>
    /// <param name="parameters">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果的第一个值</returns>
    public async Task<T?> ExecuteScalarAsync<T>(string commandText,
        IEnumerable<(string name, object? value)>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var cmdResult = parameters != null
            ? await ExecuteCommandAsync(commandText, parameters, cancellationToken).ConfigureAwait(false)
            : await ExecuteCommandAsync(commandText, cancellationToken).ConfigureAwait(false);

        var result = await cmdResult.Command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is DBNull or null)
            return default;

        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// 异步执行非查询语句并返回影响的行数
    /// </summary>
    /// <param name="commandText">SQL命令文本</param>
    /// <param name="parameters">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> ExecuteNonQueryAsync(string commandText,
        IEnumerable<(string name, object? value)>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var cmdResult = parameters != null
            ? await ExecuteCommandAsync(commandText, parameters, cancellationToken).ConfigureAwait(false)
            : await ExecuteCommandAsync(commandText, cancellationToken).ConfigureAwait(false);

        return await cmdResult.Command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步执行查询命令，并通过回调处理每个结果行
    /// </summary>
    /// <param name="commandText">SQL命令文本</param>
    /// <param name="rowCallback">行处理回调</param>
    /// <param name="parameters">命令参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理的行数</returns>
    public async Task<int> ExecuteReaderAsync(string commandText,
        Func<SqliteDataReader, Task> rowCallback,
        IEnumerable<(string name, object? value)>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var cmdResult = parameters != null
            ? await ExecuteCommandAsync(commandText, parameters, cancellationToken).ConfigureAwait(false)
            : await ExecuteCommandAsync(commandText, cancellationToken).ConfigureAwait(false);

        var count = 0;
        await using var reader = await cmdResult.Command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            await rowCallback(reader).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    /// <summary>
    /// 执行批量操作，在单个事务中
    /// </summary>
    /// <param name="batchAction">批处理操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    public async Task ExecuteInTransactionAsync(Func<SqliteConnection, Task> batchAction, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsInitialized) await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var pooledConnection = await _pool.GetConnectionAsync(cancellationToken).ConfigureAwait(false);
        var connection = pooledConnection.Connection;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await batchAction(connection).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    public async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        // 使用参数化查询以避免SQL注入风险
        var parameters = new List<(string name, object? value)> { ("@TableName", tableName) };
        var result = await ExecuteScalarAsync<int?>(
            SqlQueries.Database.TableExists,
            parameters,
            cancellationToken).ConfigureAwait(false);

        return result is 1;
    }

    /// <summary>
    /// 获取数据库文件大小（字节）
    /// </summary>
    public static long GetDatabaseSize()
    {
        try
        {
            var fileInfo = new FileInfo(DatabasePath);
            return fileInfo.Exists ? fileInfo.Length : 0;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "获取数据库大小失败");
            return 0;
        }
    }

    /// <summary>
    /// 优化数据库（收缩空间等）
    /// </summary>
    public async Task OptimizeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsInitialized) await InitializeAsync(cancellationToken).ConfigureAwait(false);

        Logger.Info("开始优化数据库...");
        await ExecuteNonQueryAsync(
            SqlQueries.Database.DatabaseMaintenance,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Logger.Info("数据库优化完成");
    }

    /// <summary>
    /// 清理连接池中的空闲连接（实例方法）
    /// </summary>
    public Task CleanIdleConnectionsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _pool.CleanIdleConnectionsAsync(cancellationToken);
    }

    /// <summary>
    /// 清理连接池中的空闲连接（静态兼容方法）
    /// </summary>
    public static Task CleanPoolIdleConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return Pool.CleanIdleConnectionsAsync(cancellationToken);
    }

    /// <summary>
    /// 备份数据库到指定路径
    /// </summary>
    public async Task<bool> BackupDatabaseAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsInitialized) await InitializeAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // 确保目标目录存在
            var directory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建备份连接
            await using var sourceConnection = new SqliteConnection(ConnectionString);
            await sourceConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 创建目标连接
            await using var backupConnection = new SqliteConnection($"Data Source={backupPath};Mode=ReadWriteCreate;");
            await backupConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 执行备份
            sourceConnection.BackupDatabase(backupConnection);
            Logger.Info($"数据库已备份到: {backupPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"备份数据库失败: {backupPath}");
            return false;
        }
    }

    /// <summary>
    /// 释放资源（同步）
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 不释放静态池，防止其他组件还在使用
        _initializationLock.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源（异步）
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // 不释放静态池，防止其他组件还在使用
        _initializationLock.Dispose();

        await Task.CompletedTask.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// SQL命令结果包装类
    /// </summary>
    public class CommandResult(SqliteCommand command, PooledSqliteConnection connection) : IAsyncDisposable
    {
        public SqliteCommand Command { get; } = command ?? throw new ArgumentNullException(nameof(command));
        private readonly PooledSqliteConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));

        public async ValueTask DisposeAsync()
        {
            await Command.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}