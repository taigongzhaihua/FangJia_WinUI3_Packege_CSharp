using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

/// <summary>
/// 高性能 SQLite 连接池管理器
/// </summary>
/// <remarks>
/// 优化的连接池实现，支持预热和无锁获取
/// </remarks>
public partial class SqliteConnectionPool : IDisposable
{
    // 可用连接栈，使用高性能栈代替ConcurrentBag
    private readonly ConcurrentStack<PooledConnection> _availableConnections = new();
    // 使用整数ID而非对象引用作为键，减少字典开销
    private readonly ConcurrentDictionary<int, SqliteConnection> _busyConnections = new();
    private readonly string _connectionString;
    private readonly ReaderWriterLockSlim _poolLock = new(LockRecursionPolicy.NoRecursion);
    private readonly int _maxPoolSize;
    private readonly TimeSpan _connectionTimeout;
    private readonly TimeSpan _acquireTimeout;
    private volatile bool _disposed;

    // 连接统计信息
    private long _totalConnectionsCreated;
    private long _totalConnectionsReused;
    private long _connectionAcquireFailures;

    // 无锁计数器用于生成唯一ID
    private int _connectionIdCounter;

    public SqliteConnectionPool(string connectionString, int maxPoolSize = 10, int connectionTimeoutSeconds = 15,
        int acquireTimeoutSeconds = 30, bool preWarmConnections = true)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        _connectionString = connectionString;
        _maxPoolSize = Math.Max(1, maxPoolSize);
        _connectionTimeout = TimeSpan.FromSeconds(Math.Max(1, connectionTimeoutSeconds));
        _acquireTimeout = TimeSpan.FromSeconds(Math.Max(1, acquireTimeoutSeconds));

        // 预热连接池
        if (preWarmConnections)
        {
            PreWarmPool(Math.Min(maxPoolSize / 2, 5)); // 预热一半的连接，最多5个
        }
    }

    /// <summary>
    /// 预热连接池，提前创建连接
    /// </summary>
    private void PreWarmPool(int count)
    {
        // 使用并行操作加速预热过程
        Parallel.For(0, count, _ =>
        {
            try
            {
                var connection = CreateNewConnectionSync();
                _availableConnections.Push(new PooledConnection(connection, DateTime.UtcNow));
                Interlocked.Increment(ref _totalConnectionsCreated);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"预热连接池失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 获取连接池状态信息
    /// </summary>
    public ConnectionPoolStatistics GetStatistics()
    {
        return new ConnectionPoolStatistics
        {
            AvailableConnections = _availableConnections.Count,
            BusyConnections = _busyConnections.Count,
            TotalCreated = Interlocked.Read(ref _totalConnectionsCreated),
            TotalReused = Interlocked.Read(ref _totalConnectionsReused),
            AcquireFailures = Interlocked.Read(ref _connectionAcquireFailures)
        };
    }

    /// <summary>
    /// 异步获取连接（优化版）
    /// </summary>
    public async ValueTask<PooledSqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) ObjectDisposedException.ThrowIf(_disposed, typeof(SqliteConnectionPool));

        var startTime = Stopwatch.GetTimestamp();
        var timeout = Stopwatch.Frequency * _acquireTimeout.TotalSeconds;

        while (Stopwatch.GetTimestamp() - startTime < timeout)
        {
            // 尝试从池中获取可用连接（无锁路径）
            if (TryGetAvailableConnection(out var connection) && connection != null) // 修复CS8604: 确保connection不为null
            {
                Interlocked.Increment(ref _totalConnectionsReused);
                return new PooledSqliteConnection(connection, this, GetNextConnectionId());
            }

            // 如果没有可用连接但未达到最大数量，创建新连接
            if (_busyConnections.Count < _maxPoolSize)
            {
                _poolLock.EnterUpgradeableReadLock();
                try
                {
                    // 二次检查，确保没有其他线程同时创建太多连接
                    if (_busyConnections.Count < _maxPoolSize)
                    {
                        var newConnection = await CreateNewConnectionAsync(cancellationToken).ConfigureAwait(false);
                        var connectionId = GetNextConnectionId();
                        _busyConnections.TryAdd(connectionId, newConnection);
                        Interlocked.Increment(ref _totalConnectionsCreated);
                        return new PooledSqliteConnection(newConnection, this, connectionId);
                    }
                }
                finally
                {
                    _poolLock.ExitUpgradeableReadLock();
                }
            }

            // 如果已达到最大连接数，等待短暂时间后重试
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
        }

        Interlocked.Increment(ref _connectionAcquireFailures);
        throw new TimeoutException($"无法获取数据库连接：连接池已达到最大容量 {_maxPoolSize}，且等待超时");
    }

    /// <summary>
    /// 尝试无锁方式获取可用连接
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetAvailableConnection(out SqliteConnection? connection)
    {
        connection = null;

        // 快速路径：尝试从栈中获取连接
        if (_availableConnections.TryPop(out var pooledConnection))
        {
            var conn = pooledConnection.Connection;

            // 检查连接状态，避免使用无效连接
            if (conn.State == System.Data.ConnectionState.Open)
            {
                try
                {
                    // 快速验证连接（使用轻量级验证）
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA quick_check(1);"; // 比SELECT 1更轻量级
                    cmd.ExecuteScalar();

                    connection = conn;
                    return true;
                }
                catch
                {
                    // 连接失效，安全释放
                    SafeDisposeConnection(conn);
                }
            }
            else
            {
                try
                {
                    // 尝试重新打开连接
                    conn.Open();
                    connection = conn;
                    return true;
                }
                catch
                {
                    // 重新打开失败，释放
                    SafeDisposeConnection(conn);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 获取下一个连接ID
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetNextConnectionId() => Interlocked.Increment(ref _connectionIdCounter);

    /// <summary>
    /// 返回连接到池
    /// </summary>
    internal void ReturnConnection(SqliteConnection connection, int connectionId)
    {
        if (_disposed)
        {
            SafeDisposeConnection(connection);
            return;
        }

        // 从忙碌列表中移除
        if (!_busyConnections.TryRemove(connectionId, out _))
        {
            SafeDisposeConnection(connection);
            return;
        }

        try
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                // 重置连接状态，使用高效的批处理
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                                  
                                                      PRAGMA journal_mode = DELETE;
                                                      PRAGMA foreign_keys = ON;
                                                      PRAGMA shrink_memory;
                                  """;
                cmd.ExecuteNonQuery();

                // 将连接返回到池中
                _availableConnections.Push(new PooledConnection(connection, DateTime.UtcNow));
            }
            else
            {
                // 连接已关闭，释放资源
                SafeDisposeConnection(connection);
            }
        }
        catch (Exception)
        {
            // 处理连接失败，安全释放
            SafeDisposeConnection(connection);
        }
    }

    /// <summary>
    /// 安全释放连接资源
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SafeDisposeConnection(SqliteConnection connection)
    {
        try
        {
            // 使用try-finally确保即使出现异常也会执行Dispose
            try
            {
                // 尝试关闭连接
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    connection.Close();
                }
            }
            finally
            {
                // 确保释放资源
                connection.Dispose();
            }
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

            // 初始化连接设置，使用批处理提高效率
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                              
                                              PRAGMA foreign_keys = ON; 
                                              PRAGMA journal_mode = WAL; 
                                              PRAGMA synchronous = NORMAL;
                                              PRAGMA cache_size = 10000;
                                              PRAGMA temp_store = MEMORY;
                              """;
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
    /// 同步创建新连接（用于预热）
    /// </summary>
    private SqliteConnection CreateNewConnectionSync()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          
                                      PRAGMA foreign_keys = ON; 
                                      PRAGMA journal_mode = WAL; 
                                      PRAGMA synchronous = NORMAL;
                                      PRAGMA cache_size = 10000;
                                      PRAGMA temp_store = MEMORY;
                          """;
        cmd.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// 清理连接池中的空闲连接
    /// </summary>
    public Task CleanIdleConnectionsAsync(CancellationToken _ = default)
    {
        if (_disposed)
            return Task.CompletedTask;

        _poolLock.EnterWriteLock();
        try
        {
            var currentTime = DateTime.UtcNow;
            var tempStack = new ConcurrentStack<PooledConnection>();

            // 使用临时栈优化处理
            while (_availableConnections.TryPop(out var pooledConn))
            {
                if (currentTime - pooledConn.CreationTime > _connectionTimeout)
                {
                    SafeDisposeConnection(pooledConn.Connection);
                }
                else
                {
                    tempStack.Push(pooledConn);
                }
            }

            // 批量将未超时的连接放回池中
            var remainingItems = tempStack.ToArray();
            foreach (var item in remainingItems)
            {
                _availableConnections.Push(item);
            }
        }
        finally
        {
            _poolLock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 批量清理策略 - 根据池大小和使用情况动态调整
    /// </summary>
    public Task<int> TrimExcessConnectionsAsync(double keepPercentage = 0.5)
    {
        if (_disposed)
            return Task.FromResult(0);

        _poolLock.EnterWriteLock();
        try
        {
            var available = _availableConnections.Count;
            var busy = _busyConnections.Count;
            var total = available + busy;

            // 若空闲连接太多且总连接数超过最大池大小的一半，则清理
            if (available <= 0 || total <= (_maxPoolSize / 2)) return Task.FromResult(0);
            // 计算需要保留的连接数
            var keepCount = Math.Max(2, (int)(available * keepPercentage));
            var removeCount = available - keepCount;

            if (removeCount <= 0)
                return Task.FromResult(0);

            var tempStack = new ConcurrentStack<PooledConnection>();
            var removed = 0;

            // 移除多余连接
            while (_availableConnections.TryPop(out var conn) && removed < removeCount)
            {
                SafeDisposeConnection(conn.Connection);
                removed++;
            }

            // 保留剩余连接
            while (_availableConnections.TryPop(out var conn))
            {
                tempStack.Push(conn);
            }

            // 将保留的连接放回池
            foreach (var conn in tempStack)
            {
                _availableConnections.Push(conn);
            }

            return Task.FromResult(removed);

        }
        finally
        {
            _poolLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 释放所有连接资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        // 使用内存屏障确保所有线程都能看到已释放标志
        _disposed = true;
        Thread.MemoryBarrier();

        _poolLock.EnterWriteLock();
        try
        {
            // 并行释放连接以加速清理
            Parallel.ForEach(_availableConnections.ToArray(), pooledConn =>
            {
                SafeDisposeConnection(pooledConn.Connection);
            });

            Parallel.ForEach(_busyConnections.Values, SafeDisposeConnection);

            _availableConnections.Clear();
            _busyConnections.Clear();
        }
        finally
        {
            _poolLock.ExitWriteLock();
            _poolLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 池化连接内部类
    /// </summary>
    private readonly struct PooledConnection(SqliteConnection connection, DateTime creationTime)
    {
        public SqliteConnection Connection { get; } = connection;
        public DateTime CreationTime { get; } = creationTime;
    }
}

/// <summary>
/// 池化的SQLite连接包装类（优化版）
/// </summary>
public partial class PooledSqliteConnection : IAsyncDisposable, IDisposable
{
    private SqliteConnection? _connection;
    private readonly SqliteConnectionPool _pool;
    private readonly int _connectionId;
    private int _isDisposed;

    /// <summary>
    /// 获取底层的SQLite连接对象
    /// </summary>
    /// <exception cref="ObjectDisposedException">连接已释放时抛出</exception>
    public SqliteConnection Connection =>
        _isDisposed == 0 ? _connection ?? throw new InvalidOperationException("连接对象为空")
                         : throw new ObjectDisposedException(nameof(PooledSqliteConnection));

    internal PooledSqliteConnection(SqliteConnection connection, SqliteConnectionPool pool, int connectionId)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _connectionId = connectionId;
    }

    /// <summary>
    /// 同步释放连接资源
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        // 使用无锁方式确保只释放一次
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        var conn = Interlocked.Exchange(ref _connection, null);

        if (conn != null)
        {
            _pool.ReturnConnection(conn, _connectionId);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 异步释放连接资源
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// 连接池统计信息
/// </summary>
public class ConnectionPoolStatistics
{
    public int AvailableConnections { get; init; }
    public int BusyConnections { get; init; }
    public long TotalCreated { get; init; }
    public long TotalReused { get; init; }
    public long AcquireFailures { get; init; }
}