using FangJia.Common;
using FangJia.Helpers;
using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

public partial class FormulationManager : IDisposable
{
    private static readonly string DatabasePath = AppHelper.GetFilePath("Data.db");
    private readonly string _connectionString = $"Data Source={DatabasePath};";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // 连接池
    private readonly SqliteConnectionPool _connectionPool;

    // 常用的SQL查询语句，减少字符串拼接开销
    private static class SqlQueries
    {
        public const string GetFirstCategories = "SELECT DISTINCT FirstCategory FROM Category ORDER BY Id ASC";
        public const string GetSecondCategories = "SELECT Id, SecondCategory FROM Category WHERE FirstCategory = @FirstCategory ORDER BY Id ASC";
        public const string GetFormulations = "SELECT Id, Name FROM Formulation WHERE CategoryId = @CategoryId ORDER BY Id ASC";
        public const string GetFormulationById = "SELECT Id, Name, CategoryId, Usage, Effect, Indication, Disease, Application, Supplement, Song, Notes, Source FROM Formulation WHERE Id = @FormulationId";
        public const string GetFormulationCompositions = """
                                                         
                                                                     SELECT
                                                                         fc.Id,
                                                                         fc.FormulationId,
                                                                         fc.DrugID,
                                                                         fc.DrugName,
                                                                         fc.Effect,
                                                                         fc.Position,
                                                                         fc.Notes
                                                                     FROM FormulationComposition fc
                                                                     WHERE fc.FormulationId = @FormulationId
                                                         """;
        public const string GetFormulationImage = "SELECT Id, Image FROM FormulationImage WHERE FormulationId = @FormulationId";
        public const string InsertFormulationComposition = """
                                                           
                                                                       INSERT INTO FormulationComposition (FormulationId, DrugId, DrugName, Effect, Position, Notes)
                                                                       VALUES (@FormulationId, @DrugId, @DrugName, @Effect, @Position, @Notes);
                                                                       SELECT last_insert_rowid();
                                                           """;
        public const string DeleteFormulationComposition = "DELETE FROM FormulationComposition WHERE Id = @Id";
        public const string DeleteCategory = "DELETE FROM Category WHERE Id = @Id";
        public const string DeleteFormulation = "DELETE FROM Formulation WHERE Id = @Id";
        public const string InsertFormulation = """
                                                
                                                            INSERT INTO Formulation (Name, CategoryId, Usage, Effect, Indication, Disease, Application, Supplement, Song, Notes, Source)
                                                            VALUES (@Name, @CategoryId, @Usage, @Effect, @Indication, @Disease, @Application, @Supplement, @Song, @Notes, @Source);
                                                            SELECT last_insert_rowid();
                                                """;
        public const string InsertCategory = """
                                             
                                                         INSERT INTO Category (FirstCategory, SecondCategory) 
                                                         VALUES (@FirstCategory, @SecondCategory);
                                                         SELECT last_insert_rowid();
                                             """;
    }

    public FormulationManager(int poolSize = 10)
    {
        // 创建连接池，设置合适的大小
        _connectionPool = new SqliteConnectionPool(_connectionString, poolSize);
    }

    /// <summary>
    /// 获取所有大类（FirstCategory）
    /// </summary>
    public async IAsyncEnumerable<FormulationCategory> GetFirstCategoriesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var pooledConnection = await _connectionPool.GetConnectionAsync(cancellationToken);
        var connection = pooledConnection.Connection;

        await using var command = new SqliteCommand(SqlQueries.GetFirstCategories, connection);
        command.CommandTimeout = 30; // 设置命令超时

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var i = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new FormulationCategory(--i, reader.GetString(0), true); // ID 设为 负数，表示大类
        }
    }

    /// <summary>
    /// 获取所有子类（SecondCategory）
    /// </summary>
    public async IAsyncEnumerable<FormulationCategory> GetSecondCategoriesAsync(string firstCategory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var pooledConnection = await _connectionPool.GetConnectionAsync(cancellationToken);
        var connection = pooledConnection.Connection;

        await using var command = new SqliteCommand(SqlQueries.GetSecondCategories, connection);
        command.Parameters.AddWithValue("@FirstCategory", firstCategory);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new FormulationCategory(reader.GetInt32(0), reader.GetString(1), true);
        }
    }

    /// <summary>
    /// 获取所有方剂（Formulation）
    /// </summary>
    public async IAsyncEnumerable<FormulationCategory> GetFormulationsAsync(int categoryId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var pooledConnection = await _connectionPool.GetConnectionAsync(cancellationToken);
        var connection = pooledConnection.Connection;

        await using var command = new SqliteCommand(SqlQueries.GetFormulations, connection);
        command.Parameters.AddWithValue("@CategoryId", categoryId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new FormulationCategory(reader.GetInt32(0), reader.GetString(1), false);
        }
    }

    public async Task<Formulation?> GetFormulationByIdAsync(int formulationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync(cancellationToken);
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.GetFormulationById, connection);
            command.Parameters.AddWithValue("@FormulationId", formulationId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new Formulation
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    CategoryId = reader.GetInt32(2),
                    Usage = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Effect = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Indication = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Disease = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    Application = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Supplement = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Song = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    Notes = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    Source = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
                };
            }

            return null;
        }
        catch (Exception e)
        {
            Logger.Error(e, "获取方剂详情失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 异步流式读取指定方剂的组成数据
    /// </summary>
    /// <param name="formulationId">方剂ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步迭代器，流式返回 FormulationComposition 对象</returns>
    public async IAsyncEnumerable<FormulationComposition> GetFormulationCompositionsAsync(
        int formulationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var pooledConnection = await _connectionPool.GetConnectionAsync(cancellationToken);
        var connection = pooledConnection.Connection;

        await using var command = new SqliteCommand(SqlQueries.GetFormulationCompositions, connection);
        command.Parameters.AddWithValue("@FormulationId", formulationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new FormulationComposition
            {
                Id = reader.GetInt32(0),
                FormulationId = reader.GetInt32(1),
                DrugId = reader.GetInt32(2),
                DrugName = reader.GetString(3),
                Effect = reader.IsDBNull(4) ? null : reader.GetString(4),
                Position = reader.IsDBNull(5) ? null : reader.GetString(5),
                Notes = reader.IsDBNull(6) ? null : reader.GetString(6)
            };
        }
    }

    /// <summary>
    /// 读取方剂图片
    /// </summary>
    /// <param name="formulationId">方剂ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>FormulationImage类</returns>
    public async Task<FormulationImage?> GetFormulationImageAsync(int formulationId,
    CancellationToken cancellationToken = default)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync(cancellationToken);
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.GetFormulationImage, connection);
            command.Parameters.AddWithValue("@FormulationId", formulationId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new FormulationImage
                {
                    Id = reader.GetInt32(0),
                    FormulationId = formulationId,
                    Image = (byte[])reader[1]
                };
            }
            return null;
        }
        catch (Exception e)
        {
            Logger.Error(e, "获取方剂图片失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task UpdateFormulationAsync(int formulationId, params (string key, string value)[]? tuples)
    {
        if (tuples == null || tuples.Length == 0)
            return;

        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            // 开启事务以提高多个更新的性能
            await using var transaction = connection.BeginTransaction();

            try
            {
                // 构造 UPDATE 语句的 SET 部分
                var setClauses = new List<string>(tuples.Length);
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.Parameters.AddWithValue("@Id", formulationId);

                for (var i = 0; i < tuples.Length; i++)
                {
                    var (key, value) = tuples[i];
                    setClauses.Add($"{key} = @p{i}");
                    command.Parameters.AddWithValue($"@p{i}", value);
                }

                command.CommandText = $"UPDATE Formulation SET {string.Join(", ", setClauses)} WHERE Id = @Id";
                await command.ExecuteNonQueryAsync();

                // 提交事务
                transaction.Commit();
            }
            catch
            {
                // 出现异常回滚事务
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "更新方剂失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task<int> InsertFormulationComposition(FormulationComposition composition)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.InsertFormulationComposition, connection);
            command.Parameters.AddWithValue("@FormulationId", composition.FormulationId);
            command.Parameters.AddWithValue("@DrugId", composition.DrugId);
            command.Parameters.AddWithValue("@DrugName", composition.DrugName);
            command.Parameters.AddWithValue("@Effect", composition.Effect == null ? DBNull.Value : composition.Effect);
            command.Parameters.AddWithValue("@Position", composition.Position == null ? DBNull.Value : composition.Position);
            command.Parameters.AddWithValue("@Notes", composition.Notes == null ? DBNull.Value : composition.Notes);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception e)
        {
            Logger.Error(e, "插入方剂组成失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task DeleteFormulationComposition(int compositionId)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.DeleteFormulationComposition, connection);
            command.Parameters.AddWithValue("@Id", compositionId);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除方剂组成失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task UpdateFormulationComposition(int id, params (string key, string? value)[]? tuples)
    {
        if (tuples == null || tuples.Length == 0) return;

        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var transaction = connection.BeginTransaction();

            try
            {
                var setClauses = new List<string>(tuples.Length);
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.Parameters.AddWithValue("@Id", id);

                for (var i = 0; i < tuples.Length; i++)
                {
                    var (key, value) = tuples[i];
                    setClauses.Add($"{key} = @p{i}");
                    command.Parameters.AddWithValue($"@p{i}", value == null ? DBNull.Value : value);
                }

                command.CommandText =
                    $"UPDATE FormulationComposition SET {string.Join(", ", setClauses)} WHERE Id = @Id";
                await command.ExecuteNonQueryAsync();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "更新方剂组成失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task DeleteCategory(int id)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.DeleteCategory, connection);
            command.Parameters.AddWithValue("@Id", id);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除分类失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task DeleteFormulation(int id)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.DeleteFormulation, connection);
            command.Parameters.AddWithValue("@Id", id);
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除方剂失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task<int> InsertFormulationAsync(Formulation formulation)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.InsertFormulation, connection);
            command.Parameters.AddWithValue("@Name", formulation.Name);
            command.Parameters.AddWithValue("@CategoryId", formulation.CategoryId);
            command.Parameters.AddWithValue("@Usage", formulation.Usage ?? string.Empty);
            command.Parameters.AddWithValue("@Effect", formulation.Effect ?? string.Empty);
            command.Parameters.AddWithValue("@Indication", formulation.Indication ?? string.Empty);
            command.Parameters.AddWithValue("@Disease", formulation.Disease ?? string.Empty);
            command.Parameters.AddWithValue("@Application", formulation.Application ?? string.Empty);
            command.Parameters.AddWithValue("@Supplement", formulation.Supplement ?? string.Empty);
            command.Parameters.AddWithValue("@Song", formulation.Song ?? string.Empty);
            command.Parameters.AddWithValue("@Notes", formulation.Notes ?? string.Empty);
            command.Parameters.AddWithValue("@Source", formulation.Source ?? string.Empty);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception e)
        {
            Logger.Error(e, "插入方剂失败: {Message}", e.Message);
            throw;
        }
    }

    public async Task<int> InsertCategoryAsync(string firstCategory, string secondCategory)
    {
        try
        {
            await using var pooledConnection = await _connectionPool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.InsertCategory, connection);
            command.Parameters.AddWithValue("@FirstCategory", firstCategory);
            command.Parameters.AddWithValue("@SecondCategory", secondCategory);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception e)
        {
            Logger.Error(e, "插入分类失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 清理连接池中的空闲连接
    /// </summary>
    public Task CleanIdleConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return _connectionPool.CleanIdleConnectionsAsync(cancellationToken);
    }

    public void Dispose()
    {
        _connectionPool.Dispose();
        GC.SuppressFinalize(this);
    }
}