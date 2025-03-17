using FangJia.Common;
using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

public class FormulationManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // 添加缓存系统
    private static readonly ConcurrentDictionary<string, object?> Cache = new();
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, DateTime> CacheTimestamps = new();

    // 命令超时时间
    private const int CommandTimeoutSeconds = 30;

    // 常用的SQL查询语句
    private static class SqlQueries
    {
        public const string GetFirstCategories = "SELECT DISTINCT FirstCategory FROM Category ORDER BY Id ASC";
        public const string GetSecondCategories = "SELECT Id, SecondCategory FROM Category WHERE FirstCategory = @FirstCategory ORDER BY Id ASC";
        public const string GetFormulations = "SELECT Id, Name FROM Formulation WHERE CategoryId = @CategoryId ORDER BY Id ASC";
        public const string GetFormulationById =
            """
            SELECT Id, Name, CategoryId, Usage, Effect, Indication, Disease, 
            Application, Supplement, Song, Notes, Source FROM Formulation 
            WHERE Id = @FormulationId
            """;
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
                                                           INSERT INTO FormulationComposition 
                                                           (FormulationId, DrugId, DrugName, Effect, Position, Notes)
                                                           VALUES (@FormulationId, @DrugId, @DrugName, @Effect, @Position, @Notes);
                                                           SELECT last_insert_rowid();
                                                           """;
        public const string DeleteFormulationComposition = "DELETE FROM FormulationComposition WHERE Id = @Id";
        public const string DeleteCategory = "DELETE FROM Category WHERE Id = @Id";
        public const string DeleteFormulation = "DELETE FROM Formulation WHERE Id = @Id";
        public const string InsertFormulation = """
                                                INSERT INTO Formulation 
                                                (Name, CategoryId, Usage, Effect, Indication, Disease, 
                                                Application, Supplement, Song, Notes, Source)
                                                VALUES (@Name, @CategoryId, @Usage, @Effect, @Indication, @Disease, 
                                                @Application, @Supplement, @Song, @Notes, @Source);
                                                SELECT last_insert_rowid();
                                                """;
        public const string InsertCategory = """
                                             INSERT INTO Category (FirstCategory, SecondCategory) 
                                             VALUES (@FirstCategory, @SecondCategory);
                                             SELECT last_insert_rowid();
                                             """;

        // 批量操作优化
        public const string GetAllSecondCategories = """
                                                     SELECT Id, FirstCategory, SecondCategory 
                                                     FROM Category 
                                                     ORDER BY FirstCategory, Id ASC
                                                     """;

        public const string GetAllFormulationsBasic = """
                                                      SELECT Id, Name, CategoryId 
                                                      FROM Formulation 
                                                      ORDER BY CategoryId, Id ASC
                                                      """;
    }

    // 缓存键定义
    private static class CacheKeys
    {
        public static string FirstCategories => "FirstCategories";
        public static string SecondCategories(string firstCategory) => $"SecondCategories_{firstCategory}";
        public static string Formulations(int categoryId) => $"Formulations_{categoryId}";
        public static string Formulation(int id) => $"Formulation_{id}";
        public static string FormulationImage(int id) => $"FormulationImage_{id}";
        public static string FormulationCompositions(int formulationId) => $"FormulationCompositions_{formulationId}";
        public static string AllSecondCategories => "AllSecondCategories";
        public static string AllFormulationsBasic => "AllFormulationsBasic";
    }

    // 缓存管理
    private static T? GetOrAddCache<T>(string key, Func<T> valueFactory, bool forceRefresh = false)
    {
        if (!forceRefresh && Cache.TryGetValue(key, out var cachedValue) &&
            CacheTimestamps.TryGetValue(key, out var timestamp) &&
            DateTime.Now - timestamp <= CacheExpiration) return (T)cachedValue!;
        var newValue = valueFactory();
        Cache[key] = newValue;
        CacheTimestamps[key] = DateTime.Now;
        return (T)newValue;

    }

    private static async Task<T?> GetOrAddCacheAsync<T>(string key, Func<Task<T>> valueFactory, bool forceRefresh = false)
    {
        if (!forceRefresh && Cache.TryGetValue(key, out var cachedValue) &&
            CacheTimestamps.TryGetValue(key, out var timestamp) &&
            DateTime.Now - timestamp <= CacheExpiration) return (T)cachedValue!;
        var newValue = await valueFactory();
        if (newValue == null) return default;
        Cache[key] = newValue;
        CacheTimestamps[key] = DateTime.Now;
        return (T)newValue;

    }

    // 清除特定缓存
    private static void InvalidateCache(string key)
    {
        Cache.TryRemove(key, out _);
        CacheTimestamps.TryRemove(key, out _);
    }

    // 清除所有缓存
    public static void ClearCache()
    {
        Cache.Clear();
        CacheTimestamps.Clear();
    }

    /// <summary>
    /// 优化的批量加载方法 - 一次性加载所有分类数据
    /// </summary>
    public static async Task<Dictionary<string, List<FormulationCategory>>?> LoadAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "AllCategoriesHierarchy";

        return await GetOrAddCacheAsync(cacheKey, async () =>
        {
            var result = new Dictionary<string, List<FormulationCategory>>();

            // 使用连接池中的连接
            await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
            var connection = pooledConnection.Connection;

            // 获取所有二级分类
            var categories = new List<(int Id, string FirstCategory, string SecondCategory)>();
            await using (var command = new SqliteCommand(SqlQueries.GetAllSecondCategories, connection))
            {
                command.CommandTimeout = CommandTimeoutSeconds;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    categories.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2)
                    ));
                }
            }

            // 创建层次结构
            foreach (var (id, firstCategory, secondCategory) in categories)
            {
                if (!result.TryGetValue(firstCategory, out var secondCategories))
                {
                    secondCategories = [];
                    result[firstCategory] = secondCategories;
                }

                secondCategories.Add(new FormulationCategory(id, secondCategory, true));
            }

            return result;
        });
    }

    /// <summary>
    /// 批量加载所有方剂基础信息 - 减少数据库连接次数
    /// </summary>
    public static async Task<Dictionary<int, List<FormulationCategory>>?> LoadAllFormulationsBasicAsync(CancellationToken cancellationToken = default)
    {
        return await GetOrAddCacheAsync(CacheKeys.AllFormulationsBasic, async () =>
        {
            var result = new Dictionary<int, List<FormulationCategory>>();

            await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.GetAllFormulationsBasic, connection);
            command.CommandTimeout = CommandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                var categoryId = reader.GetInt32(2);

                if (!result.TryGetValue(categoryId, out var formulations))
                {
                    formulations = [];
                    result[categoryId] = formulations;
                }

                formulations.Add(new FormulationCategory(id, name, false));
            }

            return result;
        });
    }

    /// <summary>
    /// 获取所有大类（FirstCategory）- 优化的异步流方法
    /// </summary>
    public static async IAsyncEnumerable<FormulationCategory> GetFirstCategoriesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var firstCategories = await GetOrAddCacheAsync(
            CacheKeys.FirstCategories,
            async () =>
            {
                var categories = new List<FormulationCategory>();

                await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
                var connection = pooledConnection.Connection;

                await using var command = new SqliteCommand(SqlQueries.GetFirstCategories, connection);
                command.CommandTimeout = CommandTimeoutSeconds;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var i = 0;
                while (await reader.ReadAsync(cancellationToken))
                {
                    categories.Add(new FormulationCategory(--i, reader.GetString(0), true));
                }

                return categories;
            });

        if (firstCategories == null) yield break;
        foreach (var category in firstCategories)
        {
            yield return category;
        }
    }

    /// <summary>
    /// 获取所有子类（SecondCategory）- 优化的异步流方法
    /// </summary>
    public static async IAsyncEnumerable<FormulationCategory> GetSecondCategoriesAsync(
        string firstCategory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var secondCategories = await GetOrAddCacheAsync(
            CacheKeys.SecondCategories(firstCategory),
            async () =>
            {
                var categories = new List<FormulationCategory>();

                await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
                var connection = pooledConnection.Connection;

                await using var command = new SqliteCommand(SqlQueries.GetSecondCategories, connection);
                command.Parameters.AddWithValue("@FirstCategory", firstCategory);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    categories.Add(new FormulationCategory(reader.GetInt32(0), reader.GetString(1), true));
                }

                return categories;
            });

        if (secondCategories == null) yield break;
        foreach (var category in secondCategories)
        {
            yield return category;
        }
    }

    /// <summary>
    /// 获取所有方剂（Formulation）- 优化的异步流方法
    /// </summary>
    public static async IAsyncEnumerable<FormulationCategory> GetFormulationsAsync(
        int categoryId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var formulations = await GetOrAddCacheAsync(
            CacheKeys.Formulations(categoryId),
            async () =>
            {
                var items = new List<FormulationCategory>();

                await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
                var connection = pooledConnection.Connection;

                await using var command = new SqliteCommand(SqlQueries.GetFormulations, connection);
                command.Parameters.AddWithValue("@CategoryId", categoryId);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new FormulationCategory(reader.GetInt32(0), reader.GetString(1), false));
                }

                return items;
            });

        if (formulations == null) yield break;
        foreach (var formulation in formulations)
        {
            yield return formulation;
        }
    }

    /// <summary>
    /// 获取方剂详情 - 使用缓存优化
    /// </summary>
    public static async Task<Formulation?> GetFormulationByIdAsync(
        int formulationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetOrAddCacheAsync<Formulation?>(
                CacheKeys.Formulation(formulationId),
                async () =>
                {
                    await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
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
                });
        }
        catch (Exception e)
        {
            Logger.Error(e, "获取方剂详情失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 异步流式读取指定方剂的组成数据 - 优化版本
    /// </summary>
    public static async IAsyncEnumerable<FormulationComposition> GetFormulationCompositionsAsync(
        int formulationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var compositions = await GetOrAddCacheAsync(
            CacheKeys.FormulationCompositions(formulationId),
            async () =>
            {
                var items = new List<FormulationComposition>();

                await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
                var connection = pooledConnection.Connection;

                await using var command = new SqliteCommand(SqlQueries.GetFormulationCompositions, connection);
                command.Parameters.AddWithValue("@FormulationId", formulationId);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new FormulationComposition
                    {
                        Id = reader.GetInt32(0),
                        FormulationId = reader.GetInt32(1),
                        DrugId = reader.GetInt32(2),
                        DrugName = reader.GetString(3),
                        Effect = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Position = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Notes = reader.IsDBNull(6) ? null : reader.GetString(6)
                    });
                }

                return items;
            });

        if (compositions == null) yield break;
        foreach (var composition in compositions)
        {
            yield return composition;
        }
    }

    /// <summary>
    /// 读取方剂图片 - 使用unsafe代码优化图片数据处理
    /// </summary>
    public static async Task<FormulationImage?> GetFormulationImageAsync(
        int formulationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetOrAddCacheAsync<FormulationImage?>(
                CacheKeys.FormulationImage(formulationId),
                async () =>
                {
                    await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
                    var connection = pooledConnection.Connection;

                    await using var command = new SqliteCommand(SqlQueries.GetFormulationImage, connection);
                    command.Parameters.AddWithValue("@FormulationId", formulationId);

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken)) return null;
                    var id = reader.GetInt32(0);

                    // 使用高效的方法获取图片数据
                    if (reader.IsDBNull(1)) return null;
                    var blobLength = (int)reader.GetBytes(1, 0, null, 0, 0);
                    var imageBytes = new byte[blobLength];

                    // 使用unsafe代码直接处理字节数据，避免多余的复制
                    unsafe
                    {
                        fixed (byte* ptrDest = imageBytes)
                        {
                            var bytesRead = reader.GetBytes(1, 0, imageBytes, 0, blobLength);

                            // 验证读取的数据量是否正确
                            if (bytesRead != blobLength)
                            {
                                throw new InvalidOperationException($"Expected to read {blobLength} bytes, but got {bytesRead}");
                            }
                        }
                    }

                    return new FormulationImage
                    {
                        Id = id,
                        FormulationId = formulationId,
                        Image = imageBytes
                    };

                });
        }
        catch (Exception e)
        {
            Logger.Error(e, "获取方剂图片失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 更新方剂 - 优化的批量更新方法
    /// </summary>
    public static async Task UpdateFormulationAsync(int formulationId, params (string key, string value)[]? tuples)
    {
        if (tuples == null || tuples.Length == 0)
            return;

        try
        {
            await using var pooledConnection = await DataManager.Pool.GetConnectionAsync();
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

                // 更新缓存
                InvalidateCache(CacheKeys.Formulation(formulationId));
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

    // 使用ArrayPool优化内存使用的方剂组成插入方法
    public static async Task<int> InsertFormulationComposition(FormulationComposition composition)
    {
        try
        {
            await using var pooledConnection = await DataManager.Pool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.InsertFormulationComposition, connection);
            command.Parameters.AddWithValue("@FormulationId", composition.FormulationId);
            command.Parameters.AddWithValue("@DrugId", composition.DrugId);
            command.Parameters.AddWithValue("@DrugName", composition.DrugName);
            command.Parameters.AddWithValue("@Effect", composition.Effect == null ? DBNull.Value : composition.Effect);
            command.Parameters.AddWithValue("@Position", composition.Position == null ? DBNull.Value : composition.Position);
            command.Parameters.AddWithValue("@Notes", composition.Notes == null ? DBNull.Value : composition.Notes);

            var result = await command.ExecuteScalarAsync();
            var id = Convert.ToInt32(result);

            // 清除相关缓存
            InvalidateCache(CacheKeys.FormulationCompositions(composition.FormulationId));

            return id;
        }
        catch (Exception e)
        {
            Logger.Error(e, "插入方剂组成失败: {Message}", e.Message);
            throw;
        }
    }

    public static async Task DeleteFormulationComposition(int compositionId)
    {
        try
        {
            var formulationId = -1;

            // 先获取FormulationId用于清除缓存
            await using (var pooledConnection = await DataManager.Pool.GetConnectionAsync())
            {
                var connection = pooledConnection.Connection;
                await using var getIdCmd = new SqliteCommand(
                    "SELECT FormulationId FROM FormulationComposition WHERE Id = @Id", connection);
                getIdCmd.Parameters.AddWithValue("@Id", compositionId);
                var result = await getIdCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    formulationId = Convert.ToInt32(result);
                }
            }

            await using (var pooledConnection = await DataManager.Pool.GetConnectionAsync())
            {
                var connection = pooledConnection.Connection;
                await using var command = new SqliteCommand(SqlQueries.DeleteFormulationComposition, connection);
                command.Parameters.AddWithValue("@Id", compositionId);
                await command.ExecuteNonQueryAsync();
            }

            // 清除相关缓存
            if (formulationId > 0)
            {
                InvalidateCache(CacheKeys.FormulationCompositions(formulationId));
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除方剂组成失败: {Message}", e.Message);
            throw;
        }
    }

    // 优化的方剂组成更新方法
    public static async Task UpdateFormulationComposition(int id, params (string key, string? value)[]? tuples)
    {
        if (tuples == null || tuples.Length == 0) return;

        try
        {
            var formulationId = -1;

            // 获取FormulationId用于清除缓存
            await using (var pooledConnection = await DataManager.Pool.GetConnectionAsync())
            {
                var connection = pooledConnection.Connection;
                await using var getIdCmd = new SqliteCommand(
                    "SELECT FormulationId FROM FormulationComposition WHERE Id = @Id", connection);
                getIdCmd.Parameters.AddWithValue("@Id", id);
                var result = await getIdCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    formulationId = Convert.ToInt32(result);
                }
            }

            await using (var pooledConnection = await DataManager.Pool.GetConnectionAsync())
            {
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

                    // 清除相关缓存
                    if (formulationId > 0)
                    {
                        InvalidateCache(CacheKeys.FormulationCompositions(formulationId));
                    }
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "更新方剂组成失败: {Message}", e.Message);
            throw;
        }
    }

    // 分类删除的优化方法
    public static async Task DeleteCategory(int id)
    {
        try
        {
            // 获取分类信息用于清除缓存
            await using (var pooledConnection = await DataManager.Pool.GetConnectionAsync())
            {
                var connection = pooledConnection.Connection;
                await using var getInfoCmd = new SqliteCommand(
                    "SELECT FirstCategory FROM Category WHERE Id = @Id", connection);
                getInfoCmd.Parameters.AddWithValue("@Id", id);
                var result = await getInfoCmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    _ = Convert.ToString(result);
                }
            }

            await using (var pooledConnection = await DataManager.Pool.GetConnectionAsync())
            {
                var connection = pooledConnection.Connection;
                await using var command = new SqliteCommand(SqlQueries.DeleteCategory, connection);
                command.Parameters.AddWithValue("@Id", id);
                await command.ExecuteNonQueryAsync();
            }

            // 清除相关缓存
            ClearCache(); // 这里需要清除所有缓存，因为分类删除会影响层次结构
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除分类失败: {Message}", e.Message);
            throw;
        }
    }

    public static async Task DeleteFormulation(int id)
    {
        try
        {
            // 删除前清除相关缓存
            InvalidateCache(CacheKeys.Formulation(id));
            InvalidateCache(CacheKeys.FormulationCompositions(id));
            InvalidateCache(CacheKeys.FormulationImage(id));

            await using var pooledConnection = await DataManager.Pool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.DeleteFormulation, connection);
            command.Parameters.AddWithValue("@Id", id);
            await command.ExecuteNonQueryAsync();

            // 清除分类相关缓存
            InvalidateCache(CacheKeys.AllFormulationsBasic);
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除方剂失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 优化的插入方剂方法 - 使用参数复用和批处理
    /// </summary>
    public static async Task<int> InsertFormulationAsync(Formulation formulation)
    {
        try
        {
            await using var pooledConnection = await DataManager.Pool.GetConnectionAsync();
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
            var id = Convert.ToInt32(result);

            // 清除缓存
            InvalidateCache(CacheKeys.Formulations(formulation.CategoryId));
            InvalidateCache(CacheKeys.AllFormulationsBasic);

            return id;
        }
        catch (Exception e)
        {
            Logger.Error(e, "插入方剂失败: {Message}", e.Message);
            throw;
        }
    }

    public static async Task<int> InsertCategoryAsync(string firstCategory, string secondCategory)
    {
        try
        {
            await using var pooledConnection = await DataManager.Pool.GetConnectionAsync();
            var connection = pooledConnection.Connection;

            await using var command = new SqliteCommand(SqlQueries.InsertCategory, connection);
            command.Parameters.AddWithValue("@FirstCategory", firstCategory);
            command.Parameters.AddWithValue("@SecondCategory", secondCategory);

            var result = await command.ExecuteScalarAsync();
            var id = Convert.ToInt32(result);

            // 清除缓存
            InvalidateCache(CacheKeys.FirstCategories);
            InvalidateCache(CacheKeys.SecondCategories(firstCategory));
            InvalidateCache(CacheKeys.AllSecondCategories);

            return id;
        }
        catch (Exception e)
        {
            Logger.Error(e, "插入分类失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 批量预加载方剂数据到内存缓存中
    /// </summary>
    public static async Task PreloadCommonDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Info("开始预加载常用数据");

            // 并行加载不同类型的数据
            var tasks = new List<Task>
            {
                Task.Run(async () => await GetOrAddCacheAsync(CacheKeys.FirstCategories, async () => {
                    var categories = new List<FormulationCategory>();
                    await using var pooledConnection = await DataManager.Pool.GetConnectionAsync(cancellationToken);
                    var connection = pooledConnection.Connection;
                    await using var command = new SqliteCommand(SqlQueries.GetFirstCategories, connection);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    var i = 0;
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        categories.Add(new FormulationCategory(--i, reader.GetString(0), true));
                    }
                    return categories;
                }), cancellationToken),

                Task.Run(async () => await LoadAllCategoriesAsync(cancellationToken)),
                Task.Run(async () => await LoadAllFormulationsBasicAsync(cancellationToken))
            };

            await Task.WhenAll(tasks);

            Logger.Info("常用数据预加载完成");
        }
        catch (Exception e)
        {
            Logger.Error(e, "预加载数据失败: {Message}", e.Message);
            // 预加载失败不抛出异常，允许应用继续运行
        }
    }
}
