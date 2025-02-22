using FangJia.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FangJia.DataAccess;

public class FormulationManager
{
    private static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data.db");
    private readonly string _connectionString = $"Data Source={DatabasePath};";

    /// <summary>
    /// 获取所有大类（FirstCategory）
    /// </summary>
    public async IAsyncEnumerable<FormulationCategory> GetFirstCategoriesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT DISTINCT FirstCategory FROM Category ORDER BY Id ASC";
        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return new FormulationCategory(-1, reader.GetString(0), true); // ID 设为 -1，表示大类
        }
    }

    /// <summary>
    /// 获取所有子类（SecondCategory）
    /// </summary>
    public async IAsyncEnumerable<FormulationCategory> GetSecondCategoriesAsync(string firstCategory,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql =
            "SELECT Id, SecondCategory FROM Category WHERE FirstCategory = @FirstCategory ORDER BY Id ASC";
        await using var command = new SqliteCommand(sql, connection);
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
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT Id, Name FROM Formulation WHERE CategoryId = @CategoryId ORDER BY Id ASC";
        await using var command = new SqliteCommand(sql, connection);
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
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT * FROM Formulation WHERE Id = @FormulationId";
        await using var command = new SqliteCommand(sql, connection);
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

    /// <summary>
    /// 异步流式读取指定方剂的组成数据
    /// </summary>
    /// <param name="formulationId">方剂ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步迭代器，流式返回 FormulationComposition 对象</returns>
    public async IAsyncEnumerable<FormulationComposition> GetFormulationCompositionsAsync(
        int formulationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) // 允许在迭代过程中取消
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql =
            """
               SELECT
                   fc.Id,
                   fc.FormulationId,
                   fc.DrugID,
                   fc.DrugName,
                   fc.Effect,
                   fc.Position,
                   fc.Notes
               FROM FormulationComposition fc
               WHERE fc.FormulationId = @FormulationId;
            """;

        await using var command = new SqliteCommand(sql, connection);
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
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT Id, Image FROM FormulationImage WHERE FormulationId = @FormulationId";
        await using var command = new SqliteCommand(sql, connection);
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


}