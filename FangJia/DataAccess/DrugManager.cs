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

using FangJia.Common;
using FangJia.DataAccess.Sql;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

/// <summary>
/// 药物管理类，提供药物相关的数据库操作
/// </summary>
public static class DrugManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 获取药物摘要列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>药物摘要异步枚举</returns>
    public static async IAsyncEnumerable<DrugSummary> GetDrugSummaryListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 使用统一的SQL语句管理
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.Drug.GetDrugSummaryList, cancellationToken);
        await using (pooledConnection)
        await using (command)
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return new DrugSummary
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Category = !reader.IsDBNull(2) ? reader.GetString(2) : string.Empty
                };
            }
        }
    }

    /// <summary>
    /// 搜索药物
    /// </summary>
    /// <param name="searchTerm">搜索关键词</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匹配的药物摘要列表</returns>
    public static async Task<List<DrugSummary>> SearchDrugsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            var results = new List<DrugSummary>();
            var searchPattern = $"%{searchTerm}%";

            var parameters = new List<(string name, object? value)>
            {
                ("@SearchTerm", searchPattern)
            };

            await DataManager.Instance.ExecuteReaderAsync(
                SqlQueries.Drug.SearchDrugs,
                reader =>
                {
                    results.Add(new DrugSummary
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Category = !reader.IsDBNull(2) ? reader.GetString(2) : string.Empty
                    });
                    return Task.CompletedTask;
                },
                parameters,
                cancellationToken);

            return results;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"搜索药物失败: {searchTerm}");
            throw new DataAccessException($"搜索药物'{searchTerm}'时出错", ex);
        }
    }

    /// <summary>
    /// 获取完整药物列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>药物完整信息异步枚举</returns>
    [UsedImplicitly]
    public static async IAsyncEnumerable<Drug> GetDrugListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.Drug.GetDrugList, cancellationToken);
        await using (pooledConnection)
        await using (command)
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return MapDrugFromReader(reader);
            }
        }
    }

    /// <summary>
    /// 根据ID获取药物信息
    /// </summary>
    /// <param name="id">药物ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>药物信息，不存在则返回null</returns>
    public static async Task<Drug?> GetDrugAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            Drug? drug = null;

            var parameters = new List<(string name, object? value)>
            {
                ("@Id", id)
            };

            await DataManager.Instance.ExecuteReaderAsync(
                SqlQueries.Drug.GetDrug,
                reader =>
                {
                    drug = MapDrugFromReader(reader);
                    return Task.CompletedTask;
                },
                parameters,
                cancellationToken);

            return drug;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"获取药物ID={id}时失败");
            throw new DataAccessException($"无法检索ID为{id}的药物信息", ex);
        }
    }

    /// <summary>
    /// 获取药物图片
    /// </summary>
    /// <param name="drugId">药物ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>药物图片信息</returns>
    public static async Task<DrugImage> GetDrugImageAsync(int drugId, CancellationToken cancellationToken = default)
    {
        try
        {
            DrugImage? image = null;

            var parameters = new List<(string name, object? value)>
            {
                ("@Id", drugId)
            };

            await DataManager.Instance.ExecuteReaderAsync(
                SqlQueries.Drug.GetDrugImage,
                reader =>
                {
                    image = new DrugImage
                    {
                        Id = reader.GetInt32(0),
                        DrugId = reader.GetInt32(1),
                        Image = reader.IsDBNull(2) ? null : GetImageDataSafely(reader, 2)
                    };
                    return Task.CompletedTask;
                },
                parameters,
                cancellationToken);

            return image ?? new DrugImage { DrugId = drugId };
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"获取药物图片失败，药物ID={drugId}");
            return new DrugImage { DrugId = drugId };
        }
    }

    /// <summary>
    /// 使用unsafe代码高效读取图片数据
    /// </summary>
    private static byte[]? GetImageDataSafely(SqliteDataReader reader, int columnIndex)
    {
        if (reader.IsDBNull(columnIndex))
            return null;

        try
        {
            var blobLength = (int)reader.GetBytes(columnIndex, 0, null, 0, 0);
            if (blobLength == 0)
                return null;

            var imageBytes = new byte[blobLength];

            // 使用unsafe代码直接处理字节数据，避免多余的复制
            unsafe
            {
                var bytesRead = reader.GetBytes(columnIndex, 0, imageBytes, 0, blobLength);

                // 验证读取的数据量是否正确
                if (bytesRead != blobLength)
                {
                    throw new InvalidOperationException($"预期读取 {blobLength} 字节，但实际读取 {bytesRead} 字节");
                }
            }

            return imageBytes;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "读取图片数据时出错");
            return null;
        }
    }

    /// <summary>
    /// 插入新药物
    /// </summary>
    /// <param name="drug">药物信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新插入药物的ID</returns>
    [UsedImplicitly]
    public static async Task<int> InsertDrugAsync(Drug drug, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@Name", drug.Name),
                ("@EnglishName", drug.EnglishName as object ?? DBNull.Value),
                ("@LatinName", drug.LatinName as object ?? DBNull.Value),
                ("@Category", drug.Category as object ?? DBNull.Value),
                ("@Origin", drug.Origin as object ?? DBNull.Value),
                ("@Properties", drug.Properties as object ?? DBNull.Value),
                ("@Quality", drug.Quality as object ?? DBNull.Value),
                ("@Taste", drug.Taste as object ?? DBNull.Value),
                ("@Meridian", drug.Meridian as object ?? DBNull.Value),
                ("@Effect", drug.Effect as object ?? DBNull.Value),
                ("@Notes", drug.Notes as object ?? DBNull.Value),
                ("@Processed", drug.Processed as object ?? DBNull.Value),
                ("@Source", drug.Source as object ?? DBNull.Value)
            };

            var result = await DataManager.Instance.ExecuteScalarAsync<long>(
                SqlQueries.Drug.InsertDrug,
                parameters,
                cancellationToken);

            return (int)result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"插入药物失败: {drug.Name}");
            throw new DataAccessException($"插入药物'{drug.Name}'时出错", ex);
        }
    }

    /// <summary>
    /// 插入药物图片
    /// </summary>
    /// <param name="drugImage">药物图片信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新插入图片的ID</returns>
    [UsedImplicitly]
    public static async Task<int> InsertDrugImageAsync(DrugImage drugImage, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@DrugId", drugImage.DrugId),
                ("@Image", drugImage.Image as object ?? DBNull.Value)
            };

            var result = await DataManager.Instance.ExecuteScalarAsync<long>(
                SqlQueries.Drug.InsertDrugImage,
                parameters,
                cancellationToken);

            return (int)result;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"插入药物图片失败，药物ID={drugImage.DrugId}");
            throw new DataAccessException($"无法为药物ID={drugImage.DrugId}插入图片", ex);
        }
    }

    /// <summary>
    /// 更新药物信息
    /// </summary>
    /// <param name="id">药物ID</param>
    /// <param name="fields">要更新的字段及其值</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否更新成功</returns>
    public static async Task<bool> UpdateDrugAsync(
        int id, CancellationToken cancellationToken = default,
        params (string key, string? value)[] fields
        )
    {
        if (fields.Length == 0)
        {
            return false;
        }

        try
        {
            var keys = fields.Select(f => f.key).ToArray();
            var sql = SqlQueries.Drug.BuildUpdateSql(id, keys);

            var parameters = new List<(string name, object? value)>
            {
                ("@Id", id)
            };

            parameters.AddRange(fields.Select(f => ($"@{f.key}", f.value as object ?? DBNull.Value))!);

            var rowsAffected = await DataManager.Instance.ExecuteNonQueryAsync(
                                sql,
                parameters,
                cancellationToken);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"更新药物ID={id}时失败");
            throw new DataAccessException($"更新药物ID={id}时出错", ex);
        }
    }

    /// <summary>
    /// 批量更新药物信息
    /// </summary>
    /// <param name="drug">完整药物信息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否更新成功</returns>
    public static async Task<bool> UpdateDrugFullAsync(Drug drug, CancellationToken cancellationToken = default)
    {
        try
        {
            var success = false;

            await DataManager.Instance.ExecuteInTransactionAsync(connection =>
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = SqlQueries.Drug.UpdateDrugFull;

                cmd.Parameters.AddWithValue("@Id", drug.Id);
                cmd.Parameters.AddWithValue("@Name", drug.Name);
                cmd.Parameters.AddWithValue("@EnglishName", drug.EnglishName as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LatinName", drug.LatinName as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Category", drug.Category as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Origin", drug.Origin as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Properties", drug.Properties as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Quality", drug.Quality as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Taste", drug.Taste as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Meridian", drug.Meridian as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Effect", drug.Effect as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notes", drug.Notes as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Processed", drug.Processed as object ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Source", drug.Source as object ?? DBNull.Value);

                var rowsAffected = cmd.ExecuteNonQuery();
                success = rowsAffected > 0;

                return Task.CompletedTask;
            }, cancellationToken);

            return success;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"更新药物完整信息失败，ID={drug.Id}");
            throw new DataAccessException($"更新药物ID={drug.Id}时出错", ex);
        }
    }

    /// <summary>
    /// 更新药物图片
    /// </summary>
    /// <param name="id">图片ID</param>
    /// <param name="image">图片数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否更新成功</returns>
    [UsedImplicitly]
    public static async Task<bool> UpdateDrugImageAsync(int id, byte[]? image, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@Id", id),
                ("@Image", image as object ?? DBNull.Value)
            };

            var rowsAffected = await DataManager.Instance.ExecuteNonQueryAsync(
                SqlQueries.Drug.UpdateDrugImage,
                parameters,
                cancellationToken);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"更新药物图片失败，ID={id}");
            throw new DataAccessException($"更新药物图片ID={id}时出错", ex);
        }
    }

    /// <summary>
    /// 删除药物
    /// </summary>
    /// <param name="id">药物ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    public static async Task<bool> DeleteDrugAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@Id", id)
            };

            // 由于设置了外键级联删除，只需删除药物表中的记录
            var rowsAffected = await DataManager.Instance.ExecuteNonQueryAsync(
                SqlQueries.Drug.DeleteDrug,
                parameters,
                cancellationToken);

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"删除药物失败，ID={id}");
            throw new DataAccessException($"删除药物ID={id}时出错", ex);
        }
    }

    /// <summary>
    /// 从SQL读取器映射药物对象
    /// </summary>
    private static Drug MapDrugFromReader(SqliteDataReader reader)
    {
        return new Drug
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            EnglishName = reader.IsDBNull(2) ? null : reader.GetString(2),
            LatinName = reader.IsDBNull(3) ? null : reader.GetString(3),
            Category = reader.IsDBNull(4) ? null : reader.GetString(4),
            Origin = reader.IsDBNull(5) ? null : reader.GetString(5),
            Properties = reader.IsDBNull(6) ? null : reader.GetString(6),
            Quality = reader.IsDBNull(7) ? null : reader.GetString(7),
            Taste = reader.IsDBNull(8) ? null : reader.GetString(8),
            Meridian = reader.IsDBNull(9) ? null : reader.GetString(9),
            Effect = reader.IsDBNull(10) ? null : reader.GetString(10),
            Notes = reader.IsDBNull(11) ? null : reader.GetString(11),
            Processed = reader.IsDBNull(12) ? null : reader.GetString(12),
            Source = reader.IsDBNull(13) ? null : reader.GetString(13)
        };
    }

    /// <summary>
    /// 检查药物是否存在
    /// </summary>
    /// <param name="id">药物ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>药物是否存在</returns>
    public static async Task<bool> DrugExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new List<(string name, object? value)>
            {
                ("@Id", id)
            };

            var result = await DataManager.Instance.ExecuteScalarAsync<int?>(
                "SELECT 1 FROM Drug WHERE Id = @Id",
                parameters,
                cancellationToken);

            return result.HasValue;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"检查药物存在失败，ID={id}");
            throw new DataAccessException($"检查药物ID={id}是否存在时出错", ex);
        }
    }

    /// <summary>
    /// 获取药物统计信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>药物统计信息</returns>
    public static async Task<DrugStatistics> GetDrugStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = new DrugStatistics();

            // 药物总数
            var totalCount = await DataManager.Instance.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Drug",
                cancellationToken: cancellationToken);
            stats.TotalCount = totalCount;

            // 分类统计
            var categories = new Dictionary<string, int>();
            await DataManager.Instance.ExecuteReaderAsync(
                "SELECT Category, COUNT(*) FROM Drug WHERE Category IS NOT NULL GROUP BY Category",
                reader =>
                {
                    var category = reader.GetString(0);
                    var count = reader.GetInt32(1);
                    categories[category] = count;
                    return Task.CompletedTask;
                },
                cancellationToken: cancellationToken);
            stats.CategoryCounts = categories;

            // 图片统计
            var imageCount = await DataManager.Instance.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM DrugImage",
                cancellationToken: cancellationToken);
            stats.ImageCount = imageCount;

            return stats;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "获取药物统计信息失败");
            throw new DataAccessException("获取药物统计信息时出错", ex);
        }
    }
}

/// <summary>
/// 数据访问异常
/// </summary>
public class DataAccessException : Exception
{
    public DataAccessException(string message) : base(message) { }
    public DataAccessException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// 药物统计信息
/// </summary>
public class DrugStatistics
{
    /// <summary>
    /// 药物总数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 各分类药物数量
    /// </summary>
    public Dictionary<string, int> CategoryCounts { get; set; } = [];

    /// <summary>
    /// 药物图片数量
    /// </summary>
    public int ImageCount { get; set; }
}