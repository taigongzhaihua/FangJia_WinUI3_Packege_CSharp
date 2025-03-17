using FangJia.Common;
using FangJia.DataAccess.Sql;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

/// <summary>
/// 方剂管理器 - 优化版
/// 提供方剂数据的访问与管理功能
/// </summary>
public static class FormulationManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // 添加缓存系统
    private static readonly ConcurrentDictionary<string, object?> Cache = new();
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, DateTime> CacheTimestamps = new();

    // 命令超时时间
    private const int CommandTimeoutSeconds = 30;

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

    #region 缓存管理

    // 缓存管理
    private static T? GetOrAddCache<T>(string key, Func<T> valueFactory, bool forceRefresh = false)
    {
        if (!forceRefresh && Cache.TryGetValue(key, out var cachedValue) &&
            CacheTimestamps.TryGetValue(key, out var timestamp) &&
            DateTime.Now - timestamp <= CacheExpiration) return (T)cachedValue!;
        var newValue = valueFactory();
        Cache[key] = newValue;
        CacheTimestamps[key] = DateTime.Now;
        return newValue;
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

    #endregion

    #region 批量加载方法

    /// <summary>
    /// 优化的批量加载方法 - 一次性加载所有分类数据
    /// </summary>
    public static async Task<Dictionary<string, List<FormulationCategory>>?> LoadAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "AllCategoriesHierarchy";

        return await GetOrAddCacheAsync(cacheKey, async () =>
        {
            var result = new Dictionary<string, List<FormulationCategory>>();
            var categories = new List<(int Id, string FirstCategory, string SecondCategory)>();

            // 使用优化的 DataManager 执行查询
            await DataManager.Instance.ExecuteReaderAsync(
                FormulationQueries.Category.GetAllSecondCategories,
                reader =>
                {
                    categories.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2)
                    ));
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationToken
            );

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

            await DataManager.Instance.ExecuteReaderAsync(
                FormulationQueries.Formulation.GetAllFormulationsBasic,
                reader =>
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
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationToken
            );

            return result;
        });
    }

    #endregion

    #region 分类管理

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

                // 使用优化后的 DataManager 接口
                await DataManager.Instance.ExecuteReaderAsync(
                    FormulationQueries.Category.GetFirstCategories,
                    reader =>
                    {
                        var i = categories.Count;
                        categories.Add(new FormulationCategory(--i, reader.GetString(0), true));
                        return Task.CompletedTask;
                    },
                    cancellationToken: cancellationToken
                );

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

                var parameters = new List<(string name, object? value)>
                {
                    ("@FirstCategory", firstCategory)
                };

                await DataManager.Instance.ExecuteReaderAsync(
                    FormulationQueries.Category.GetSecondCategories,
                    reader =>
                    {
                        categories.Add(new FormulationCategory(
                            reader.GetInt32(0),
                            reader.GetString(1),
                            true));
                        return Task.CompletedTask;
                    },
                    parameters,
                    cancellationToken
                );

                return categories;
            });

        if (secondCategories == null) yield break;
        foreach (var category in secondCategories)
        {
            yield return category;
        }
    }

    /// <summary>
    /// 插入分类
    /// </summary>
    public static async Task<int> InsertCategoryAsync(string firstCategory, string secondCategory)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@FirstCategory", firstCategory),
                ("@SecondCategory", secondCategory)
            };

            var result = await DataManager.Instance.ExecuteScalarAsync<long>(
                FormulationQueries.Category.InsertCategory,
                parameters
            );

            var id = (int)result;

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
    /// 删除分类
    /// </summary>
    public static async Task DeleteCategory(int id)
    {
        try
        {
            // 获取分类信息用于清除缓存
            var getInfoParam = new List<(string name, object? value)> { ("@Id", id) };
            await DataManager.Instance.ExecuteScalarAsync<string>(
                FormulationQueries.Category.GetCategoryById,
                getInfoParam
            );

            // 执行删除
            var deleteParam = new List<(string name, object? value)> { ("@Id", id) };
            await DataManager.Instance.ExecuteNonQueryAsync(
                FormulationQueries.Category.DeleteCategory,
                deleteParam
            );

            // 清除相关缓存
            ClearCache(); // 分类删除会影响层次结构，清除所有缓存
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除分类失败: {Message}", e.Message);
            throw;
        }
    }

    #endregion

    #region 方剂管理

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

                var parameters = new List<(string name, object? value)>
                {
                    ("@CategoryId", categoryId)
                };

                await DataManager.Instance.ExecuteReaderAsync(
                    FormulationQueries.Formulation.GetFormulations,
                    reader =>
                    {
                        items.Add(new FormulationCategory(
                            reader.GetInt32(0),
                            reader.GetString(1),
                            false));
                        return Task.CompletedTask;
                    },
                    parameters,
                    cancellationToken
                );

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
            return await GetOrAddCacheAsync(
                CacheKeys.Formulation(formulationId),
                async () =>
                {
                    Formulation? formulation = null;

                    var parameters = new List<(string name, object? value)>
                    {
                        ("@FormulationId", formulationId)
                    };

                    await DataManager.Instance.ExecuteReaderAsync(
                        FormulationQueries.Formulation.GetFormulationById,
                        reader =>
                        {
                            formulation = new Formulation
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
                            return Task.CompletedTask;
                        },
                        parameters,
                        cancellationToken
                    );

                    return formulation;
                });
        }
        catch (Exception e)
        {
            Logger.Error(e, "获取方剂详情失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 插入方剂
    /// </summary>
    public static async Task<int> InsertFormulationAsync(Formulation formulation)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@Name", formulation.Name),
                ("@CategoryId", formulation.CategoryId),
                ("@Usage", formulation.Usage as object ?? string.Empty),
                ("@Effect", formulation.Effect as object ?? string.Empty),
                ("@Indication", formulation.Indication as object ?? string.Empty),
                ("@Disease", formulation.Disease as object ?? string.Empty),
                ("@Application", formulation.Application as object ?? string.Empty),
                ("@Supplement", formulation.Supplement as object ?? string.Empty),
                ("@Song", formulation.Song as object ?? string.Empty),
                ("@Notes", formulation.Notes as object ?? string.Empty),
                ("@Source", formulation.Source as object ?? string.Empty)
            };

            var result = await DataManager.Instance.ExecuteScalarAsync<long>(
                FormulationQueries.Formulation.InsertFormulation,
                parameters
            );

            var id = (int)result;

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

    /// <summary>
    /// 更新方剂 - 优化的批量更新方法
    /// </summary>
    public static async Task UpdateFormulationAsync(int formulationId, params (string key, string value)[]? tuples)
    {
        if (tuples == null || tuples.Length == 0)
            return;

        try
        {
            // 使用事务执行批量更新
            await DataManager.Instance.ExecuteInTransactionAsync(async connection =>
            {
                await using var command = connection.CreateCommand();

                // 构造 UPDATE 语句的 SET 部分
                var setClauses = new List<string>(tuples.Length);
                command.Parameters.AddWithValue("@Id", formulationId);

                for (var i = 0; i < tuples.Length; i++)
                {
                    var (key, value) = tuples[i];
                    setClauses.Add($"{key} = @p{i}");
                    command.Parameters.AddWithValue($"@p{i}", value);
                }

                command.CommandText = $"UPDATE Formulation SET {string.Join(", ", setClauses)} WHERE Id = @Id";
                await command.ExecuteNonQueryAsync();
            });

            // 更新缓存
            InvalidateCache(CacheKeys.Formulation(formulationId));
        }
        catch (Exception e)
        {
            Logger.Error(e, "更新方剂失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 删除方剂
    /// </summary>
    public static async Task DeleteFormulation(int id)
    {
        try
        {
            // 删除前清除相关缓存
            InvalidateCache(CacheKeys.Formulation(id));
            InvalidateCache(CacheKeys.FormulationCompositions(id));
            InvalidateCache(CacheKeys.FormulationImage(id));

            // 执行删除
            var deleteParam = new List<(string name, object? value)> { ("@Id", id) };
            await DataManager.Instance.ExecuteNonQueryAsync(
                FormulationQueries.Formulation.DeleteFormulation,
                deleteParam
            );

            // 清除分类相关缓存
            InvalidateCache(CacheKeys.AllFormulationsBasic);
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除方剂失败: {Message}", e.Message);
            throw;
        }
    }

    #endregion

    #region 方剂组成管理

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

                var parameters = new List<(string name, object? value)>
                {
                    ("@FormulationId", formulationId)
                };

                await DataManager.Instance.ExecuteReaderAsync(
                    FormulationQueries.Composition.GetFormulationCompositions,
                    reader =>
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
                        return Task.CompletedTask;
                    },
                    parameters,
                    cancellationToken
                );

                return items;
            });

        if (compositions == null) yield break;
        foreach (var composition in compositions)
        {
            yield return composition;
        }
    }

    /// <summary>
    /// 方剂组成插入方法
    /// </summary>
    public static async Task<int> InsertFormulationComposition(FormulationComposition composition)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@FormulationId", composition.FormulationId),
                ("@DrugId", composition.DrugId),
                ("@DrugName", composition.DrugName),
                ("@Effect", composition.Effect as object ?? DBNull.Value),
                ("@Position", composition.Position as object ?? DBNull.Value),
                ("@Notes", composition.Notes as object ?? DBNull.Value)
            };

            var result = await DataManager.Instance.ExecuteScalarAsync<long>(
                FormulationQueries.Composition.InsertFormulationComposition,
                parameters
            );

            var id = (int)result;

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

    /// <summary>
    /// 删除方剂组成
    /// </summary>
    public static async Task DeleteFormulationComposition(int compositionId)
    {
        try
        {
            // 先获取FormulationId用于清除缓存
            var getIdParam = new List<(string name, object? value)> { ("@Id", compositionId) };
            var formulationId = await DataManager.Instance.ExecuteScalarAsync<int>(
                FormulationQueries.Composition.GetCompositionFormulationId,
                getIdParam
            );

            // 执行删除
            var deleteParam = new List<(string name, object? value)> { ("@Id", compositionId) };
            await DataManager.Instance.ExecuteNonQueryAsync(
                FormulationQueries.Composition.DeleteFormulationComposition,
                deleteParam
            );

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

    /// <summary>
    /// 优化的方剂组成更新方法
    /// </summary>
    public static async Task UpdateFormulationComposition(int id, params (string key, string? value)[]? tuples)
    {
        if (tuples == null || tuples.Length == 0) return;

        try
        {
            // 获取FormulationId用于清除缓存
            var getIdParam = new List<(string name, object? value)> { ("@Id", id) };
            var formulationId = await DataManager.Instance.ExecuteScalarAsync<int>(
                FormulationQueries.Composition.GetCompositionFormulationId,
                getIdParam
            );

            // 使用事务执行批量更新
            await DataManager.Instance.ExecuteInTransactionAsync(async connection =>
            {
                await using var command = connection.CreateCommand();

                var setClauses = new List<string>(tuples.Length);
                command.Parameters.AddWithValue("@Id", id);

                for (var i = 0; i < tuples.Length; i++)
                {
                    var (key, value) = tuples[i];
                    setClauses.Add($"{key} = @p{i}");
                    command.Parameters.AddWithValue($"@p{i}", value as object ?? DBNull.Value);
                }

                command.CommandText = $"UPDATE FormulationComposition SET {string.Join(", ", setClauses)} WHERE Id = @Id";
                await command.ExecuteNonQueryAsync();
            });

            // 清除相关缓存
            if (formulationId > 0)
            {
                InvalidateCache(CacheKeys.FormulationCompositions(formulationId));
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "更新方剂组成失败: {Message}", e.Message);
            throw;
        }
    }

    #endregion

    #region 方剂图片管理

    /// <summary>
    /// 读取方剂图片 - 使用unsafe代码优化性能
    /// </summary>
    public static async Task<FormulationImage?> GetFormulationImageAsync(
        int formulationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetOrAddCacheAsync(
                CacheKeys.FormulationImage(formulationId),
                async () =>
                {
                    FormulationImage? formulationImage = null;

                    var parameters = new List<(string name, object? value)>
                    {
                        ("@FormulationId", formulationId)
                    };

                    await DataManager.Instance.ExecuteReaderAsync(
                        FormulationQueries.Image.GetFormulationImage,
                        reader =>
                        {
                            var id = reader.GetInt32(0);
                            byte[]? imageBytes = null;

                            // 使用高效的方法获取图片数据
                            if (!reader.IsDBNull(1))
                            {
                                var blobLength = (int)reader.GetBytes(1, 0, null, 0, 0);
                                imageBytes = new byte[blobLength];

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
                            }

                            formulationImage = new FormulationImage
                            {
                                Id = id,
                                FormulationId = formulationId,
                                Image = imageBytes
                            };

                            return Task.CompletedTask;
                        },
                        parameters,
                        cancellationToken
                    );

                    return formulationImage;
                });
        }
        catch (Exception e)
        {
            Logger.Error(e, "获取方剂图片失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 插入方剂图片 - 高性能版本
    /// </summary>
    public static async Task<int> InsertFormulationImageAsync(int formulationId, byte[]? imageData)
    {
        try
        {
            // 先检查是否已存在图片
            var existingImageId = await DataManager.Instance.ExecuteScalarAsync<int?>(
                FormulationQueries.Image.CheckImageExists,
                [("@FormulationId", formulationId)]
            );

            if (existingImageId.HasValue)
            {
                // 如果已存在，则更新
                await DataManager.Instance.ExecuteNonQueryAsync(
                    FormulationQueries.Image.UpdateFormulationImage,
                    [
                        ("@FormulationId", formulationId),
                        ("@Image", imageData == null ? DBNull.Value : imageData)
                    ]
                );

                // 清除缓存
                InvalidateCache(CacheKeys.FormulationImage(formulationId));

                return existingImageId.Value;
            }

            // 如果不存在，则插入新记录
            var result = await DataManager.Instance.ExecuteScalarAsync<long>(
                FormulationQueries.Image.InsertFormulationImage,
                [
                    ("@FormulationId", formulationId),
                    ("@Image", imageData == null ? DBNull.Value : imageData)
                ]
            );

            var id = (int)result;

            // 清除缓存
            InvalidateCache(CacheKeys.FormulationImage(formulationId));

            return id;
        }
        catch (Exception e)
        {
            Logger.Error(e, "插入或更新方剂图片失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 删除方剂图片
    /// </summary>
    public static async Task<bool> DeleteFormulationImageAsync(int formulationId)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@FormulationId", formulationId)
            };

            var rowsAffected = await DataManager.Instance.ExecuteNonQueryAsync(
                FormulationQueries.Image.DeleteFormulationImage,
                parameters
            );

            // 清除缓存
            InvalidateCache(CacheKeys.FormulationImage(formulationId));

            return rowsAffected > 0;
        }
        catch (Exception e)
        {
            Logger.Error(e, "删除方剂图片失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 高性能批量加载图片方法
    /// </summary>
    public static async Task<Dictionary<int, byte[]?>> LoadImagesAsync(IEnumerable<int> formulationIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, byte[]?>();

        foreach (var id in formulationIds)
        {
            // 先检查缓存
            var cacheKey = CacheKeys.FormulationImage(id);
            if (Cache.TryGetValue(cacheKey, out var cached) &&
                cached is FormulationImage cachedImage &&
                CacheTimestamps.TryGetValue(cacheKey, out var timestamp) &&
                DateTime.Now - timestamp <= CacheExpiration)
            {
                result[id] = cachedImage.Image;
                continue;
            }

            // 缓存未命中，加载图片
            try
            {
                var image = await GetFormulationImageAsync(id, cancellationToken);
                result[id] = image?.Image;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"加载方剂图片失败 ID={id}");
                result[id] = null;
            }
        }

        return result;
    }

    #endregion

    #region 搜索和统计功能

    /// <summary>
    /// 搜索方剂 - 支持按名称或相关字段搜索
    /// </summary>
    public static async Task<List<FormulationCategory>> SearchFormulationsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return [];

            var results = new List<FormulationCategory>();
            var searchPattern = $"%{searchTerm}%";

            var parameters = new List<(string name, object? value)>
            {
                ("@SearchTerm", searchPattern)
            };

            // 使用优化后的数据访问方法
            await DataManager.Instance.ExecuteReaderAsync(
                FormulationQueries.Formulation.SearchFormulations,
                reader =>
                {
                    results.Add(new FormulationCategory(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        false));
                    return Task.CompletedTask;
                },
                parameters,
                cancellationToken
            );

            return results;
        }
        catch (Exception e)
        {
            Logger.Error(e, "搜索方剂失败: {Message}", e.Message);
            throw;
        }
    }

    /// <summary>
    /// 获取方剂统计信息
    /// </summary>
    public static async Task<FormulationStats> GetFormulationStatsAsync()
    {
        try
        {
            var stats = new FormulationStats();

            // 使用单个批处理查询统计各种数据
            await DataManager.Instance.ExecuteInTransactionAsync(async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = FormulationQueries.Statistics.GetFormulationStats;

                await using var reader = await command.ExecuteReaderAsync();

                // 读取方剂总数
                await reader.ReadAsync();
                stats.TotalFormulations = reader.GetInt32(0);

                // 读取分类总数
                await reader.NextResultAsync();
                await reader.ReadAsync();
                stats.TotalCategories = reader.GetInt32(0);

                // 读取组成总数
                await reader.NextResultAsync();
                await reader.ReadAsync();
                stats.TotalComponents = reader.GetInt32(0);

                // 读取不同药物总数
                await reader.NextResultAsync();
                await reader.ReadAsync();
                stats.UniqueDrugs = reader.GetInt32(0);
            });

            return stats;
        }
        catch (Exception e)
        {
            Logger.Error(e, "获取方剂统计数据失败: {Message}", e.Message);
            return new FormulationStats();
        }
    }

    #endregion

    #region 预加载和性能优化

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

                    await DataManager.Instance.ExecuteReaderAsync(
                        FormulationQueries.Category.GetFirstCategories,
                        reader =>
                        {
                            var i = categories.Count;
                            categories.Add(new FormulationCategory(--i, reader.GetString(0), true));
                            return Task.CompletedTask;
                        },
                        cancellationToken: cancellationToken
                    );

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

    /// <summary>
    /// 优化缓存性能 - 整理和压缩缓存
    /// </summary>
    public static void CompactCache()
    {
        try
        {
            var removedItems = 0;
            var currentTime = DateTime.Now;
            var expiredKeys = (from entry in CacheTimestamps where currentTime - entry.Value > CacheExpiration select entry.Key).ToList();

            // 找出过期的缓存项

            // 批量移除过期缓存
            foreach (var key in expiredKeys.Where(key => Cache.TryRemove(key, out _)))
            {
                CacheTimestamps.TryRemove(key, out _);
                removedItems++;
            }

            if (removedItems > 0)
            {
                Logger.Debug($"缓存整理完成，移除了 {removedItems} 个过期项");
            }

            // 请求垃圾回收
            GC.Collect(1, GCCollectionMode.Optimized, false);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "缓存整理失败");
        }
    }

    #endregion
}

/// <summary>
/// 方剂统计信息类
/// </summary>
public class FormulationStats
{
    public int TotalFormulations { get; set; }
    public int TotalCategories { get; set; }
    public int TotalComponents { get; set; }
    public int UniqueDrugs { get; set; }
}