using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

/// <summary>
/// SQLite 连接池管理器
/// </summary>
/// <remarks>
/// 创建新的连接池
/// </remarks>
public partial class SqliteConnectionPool : IDisposable
{
    private readonly ConcurrentBag<PooledConnection> _availableConnections = [];
    private readonly ConcurrentDictionary<SqliteConnection, bool> _busyConnections = new();
    private readonly string _connectionString;
    private readonly SemaphoreSlim _poolLock = new(1, 1);
    private readonly int _maxPoolSize;
    private readonly TimeSpan _connectionTimeout;
    private bool _disposed;

    public SqliteConnectionPool(string connectionString, int maxPoolSize = 10, int connectionTimeoutSeconds = 15)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        _connectionString = connectionString;
        _maxPoolSize = Math.Max(1, maxPoolSize); // Ensure at least 1 connection
        _connectionTimeout = TimeSpan.FromSeconds(Math.Max(1, connectionTimeoutSeconds));
    }

    /// <summary>
    /// 异步获取连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>池化的连接对象</returns>
    public async Task<PooledSqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) ObjectDisposedException.ThrowIf(_disposed, typeof(SqliteConnectionPool));
        await _poolLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // 尝试获取可用连接
            while (_availableConnections.TryTake(out var pooledConnection))
            {
                var connection = pooledConnection.Connection;

                // 确保连接不为空

                // 检查连接状态
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    try
                    {
                        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // 如果重新打开失败，则丢弃此连接并尝试下一个
                        SafeDisposeConnection(connection);
                        continue;
                    }
                }

                // 检查连接有效性，执行一个简单查询
                try
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT 1;";
                    await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // 连接已失效，丢弃并尝试下一个
                    SafeDisposeConnection(connection);
                    continue;
                }

                // 连接可用
                _busyConnections.TryAdd(connection, true);
                return new PooledSqliteConnection(connection, this);
            }

            // 如果没有可用连接且未达到最大连接数，则创建新连接
            if (_busyConnections.Count < _maxPoolSize)
            {
                var connection = await CreateNewConnectionAsync(cancellationToken).ConfigureAwait(false);
                _busyConnections.TryAdd(connection, true);
                return new PooledSqliteConnection(connection, this);
            }

            // 已达到最大连接数，等待连接释放
            throw new TimeoutException($"无法获取数据库连接：连接池已达到最大容量 {_maxPoolSize}");
        }
        finally
        {
            _poolLock.Release();
        }
    }

    /// <summary>
    /// 返回连接到池
    /// </summary>
    internal void ReturnConnection(SqliteConnection connection)
    {
        if (_disposed)
            return;

        if (!_busyConnections.TryRemove(connection, out _)) return;
        try
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                // 重新设置连接状态，清除任何未提交的事务
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode = DELETE;"; // Reset WAL if enabled
                cmd.ExecuteNonQuery();

                _availableConnections.Add(new PooledConnection(connection, DateTime.UtcNow));
            }
            else
            {
                // 连接已关闭，丢弃并释放资源
                SafeDisposeConnection(connection);
            }
        }
        catch (Exception)
        {
            // 如果操作失败，安全地释放连接
            SafeDisposeConnection(connection);
        }
    }

    /// <summary>
    /// 安全释放连接资源
    /// </summary>
    private static void SafeDisposeConnection(SqliteConnection connection)
    {
        try
        {
            connection.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disposing SQLite connection: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建新的连接
    /// </summary>
    private async Task<SqliteConnection> CreateNewConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 初始化连接设置
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON;"; // 启用外键约束
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return connection;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating new SQLite connection: {ex.Message}");
            throw new InvalidOperationException("无法创建数据库连接", ex);
        }
    }

    /// <summary>
    /// 清理连接池中的空闲连接
    /// </summary>
    public async Task CleanIdleConnectionsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _poolLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var currentTime = DateTime.UtcNow;
            var tempPool = new ConcurrentBag<PooledConnection>();

            // 移除超时连接
            while (_availableConnections.TryTake(out var pooledConn))
            {
                if (currentTime - pooledConn.CreationTime > _connectionTimeout)
                {
                    SafeDisposeConnection(pooledConn.Connection);
                }
                else
                {
                    tempPool.Add(pooledConn);
                }
            }

            // 将未超时的连接放回池中
            foreach (var conn in tempPool)
            {
                _availableConnections.Add(conn);
            }
        }
        finally
        {
            _poolLock.Release();
        }
    }

    /// <summary>
    /// 释放所有连接资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // 释放所有可用连接
        while (_availableConnections.TryTake(out var pooledConnection))
        {
            SafeDisposeConnection(pooledConnection.Connection);
        }

        // 释放所有正在使用的连接
        foreach (var connection in _busyConnections.Keys)
        {
            SafeDisposeConnection(connection);
        }

        _busyConnections.Clear();
        _poolLock.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 池化连接内部类
    /// </summary>
    private class PooledConnection(SqliteConnection connection, DateTime creationTime)
    {
        public SqliteConnection Connection { get; } = connection;
        public DateTime CreationTime { get; } = creationTime;
    }
}

/// <summary>
/// 池化的SQLite连接包装类
/// </summary>
public partial class PooledSqliteConnection : IAsyncDisposable, IDisposable
{
    private SqliteConnection _connection;
    private readonly SqliteConnectionPool _pool;
    private bool _isDisposed;

    /// <summary>
    /// 获取底层的SQLite连接对象
    /// </summary>
    /// <exception cref="ObjectDisposedException">连接已释放时抛出</exception>
    public SqliteConnection Connection =>
        !_isDisposed ? _connection : throw new ObjectDisposedException(nameof(PooledSqliteConnection));

    internal PooledSqliteConnection(SqliteConnection connection, SqliteConnectionPool pool)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
    }

    /// <summary>
    /// 同步释放连接资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;

        var conn = Interlocked.Exchange(ref _connection!, null);
        _isDisposed = true;

        _pool.ReturnConnection(conn);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 异步释放连接资源
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose 方法应调用 SuppressFinalize", Justification = "<挂起>")]
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}