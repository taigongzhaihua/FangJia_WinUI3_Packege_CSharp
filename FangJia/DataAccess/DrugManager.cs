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
using JetBrains.Annotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

public class DrugManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static class SqlQueries
    {
        public const string GetDrugSummaryList = "SELECT Id, Name, Category FROM Drug";
        public const string GetDrugList = "SELECT * FROM Drug";
        public const string GetDrug = "SELECT * FROM Drug WHERE Id = @Id";
        public const string GetDrugImage = "SELECT * FROM DrugImage WHERE DrugId = @Id";
        public const string InsertDrug = """
                                         INSERT INTO Drug (Name, EnglishName, LatinName, Category, Origin, Properties, Quality, Taste, Meridian, Effect, Notes, Processed, Source)
                                         VALUES (@Name, @EnglishName, @LatinName, @Category, @Origin, @Properties, @Quality, @Taste, @Meridian, @Effect, @Notes, @Processed, @Source);
                                         SELECT last_insert_rowid();
                                         """;
        public const string InsertDrugImage = """
                                              INSERT INTO DrugImage (DrugId, Image) 
                                              VALUES (@DrugId, @Image);
                                              SELECT last_insert_rowid();
                                              """;

        public static string UpdateDrug(int id, params string[] keys)
        {
            var set = string.Join(", ", keys.Select(k => $"{k} = @{k}"));
            return $"UPDATE Drug SET {set} WHERE Id = {id}";
        }

        public const string UpdateDrugImage = $"UPDATE DrugImage SET Image = @Image WHERE Id = @Id";
        public const string DeleteDrug = "DELETE FROM Drug WHERE Id = @Id";
        public const string DeleteDrugImage = "DELETE FROM DrugImage WHERE Id = @Id";
    }
    public static async IAsyncEnumerable<DrugSummary> GetDrugSummaryListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.GetDrugSummaryList, cancellationToken);
        await using (pooledConnection) await using (command)
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return new DrugSummary
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2)
                };
            }
        }
    }

    [UsedImplicitly]
    public static async IAsyncEnumerable<Drug> GetDrugListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.GetDrugList, cancellationToken);
        await using (pooledConnection) await using (command)
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return new Drug
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(2),
                    EnglishName = reader.GetString(3),
                    LatinName = reader.GetString(4),
                    Origin = reader.GetString(5),
                    Properties = reader.GetString(6),
                    Quality = reader.GetString(7),
                    Taste = reader.GetString(8),
                    Meridian = reader.GetString(9),
                    Effect = reader.GetString(10),
                    Notes = reader.GetString(11),
                    Processed = reader.GetString(12),
                    Source = reader.GetString(13)
                };
            }
        }
    }

    public static async Task<Drug?> GetDrugAsync(int id, CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.GetDrug, cancellationToken);
        await using (pooledConnection) await using (command)
        {
            command.Parameters.AddWithValue("@Id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new Drug
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Category = reader.GetString(4),
                    EnglishName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    LatinName = reader.IsDBNull(3) ? null : reader.GetString(3),
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

            return null;
        }
    }

    public static async Task<DrugImage> GetDrugImageAsync(int id, CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.GetDrugImage, cancellationToken);
        await using (pooledConnection) await using (command)
        {
            command.Parameters.AddWithValue("@Id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new DrugImage
                {
                    Id = reader.GetInt32(0),
                    DrugId = reader.GetInt32(1),
                    Image = reader.IsDBNull(2) ? null : reader.GetFieldValue<byte[]>(2)
                };
            }

            return new DrugImage();
        }
    }

    [UsedImplicitly]
    public static async Task<int> InsertDrugAsync(Drug drug, CancellationToken cancellationToken = default)
    {
        try
        {
            var (command, pooledConnection) =
                await DataManager.CreateCommandAsync(SqlQueries.InsertDrug, cancellationToken);
            await using (pooledConnection)
            await using (command)
            {
                command.Parameters.AddWithValue("@Name", drug.Name);
                command.Parameters.AddWithValue("@EnglishName", drug.EnglishName);
                command.Parameters.AddWithValue("@LatinName", drug.LatinName);
                command.Parameters.AddWithValue("@Category", drug.Category);
                command.Parameters.AddWithValue("@Origin", drug.Origin);
                command.Parameters.AddWithValue("@Properties", drug.Properties);
                command.Parameters.AddWithValue("@Quality", drug.Quality);
                command.Parameters.AddWithValue("@Taste", drug.Taste);
                command.Parameters.AddWithValue("@Meridian", drug.Meridian);
                command.Parameters.AddWithValue("@Effect", drug.Effect);
                command.Parameters.AddWithValue("@Notes", drug.Notes);
                command.Parameters.AddWithValue("@Processed", drug.Processed);
                command.Parameters.AddWithValue("@Source", drug.Source);
                return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            }
        }
        catch (Exception e)
        {
            Logger.Error($"插入药物错误: {e.Message}", e);
            throw;
        }
    }

    [UsedImplicitly]
    public static async Task<int> InsertDrugImageAsync(DrugImage drugImage, CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.InsertDrugImage, cancellationToken);
        await using (pooledConnection) await using (command)
        {
            command.Parameters.AddWithValue("@DrugId", drugImage.DrugId);
            command.Parameters.AddWithValue("@Image", drugImage.Image);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
    }

    public static async Task<bool> UpdateDrugAsync(int id, CancellationToken cancellationToken = default, params (string k, string?)[] tuple)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.UpdateDrug(id, [.. tuple.Select(t => t.k)]), cancellationToken);
        await using (pooledConnection) await using (command)
        {
            command.Parameters.AddWithValue("@Id", id);
            foreach (var (key, value) in tuple)
            {
                command.Parameters.AddWithValue($"@{key}", value);
            }
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
    }

    [UsedImplicitly]
    public static async Task<bool> UpdateDrugImageAsync(int id, byte[] image, CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.UpdateDrugImage, cancellationToken);
        await using (pooledConnection) await using (command)
        {
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Image", image);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
    }

    public static async Task<bool> DeleteDrugAsync(int id, CancellationToken cancellationToken = default)
    {
        var (command, pooledConnection) = await DataManager.CreateCommandAsync(SqlQueries.DeleteDrug, cancellationToken);
        await using (pooledConnection) await using (command)
        {
            command.Parameters.AddWithValue("@Id", id);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
    }
}