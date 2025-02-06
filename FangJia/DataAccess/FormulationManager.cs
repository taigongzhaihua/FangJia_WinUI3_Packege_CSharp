using FangJia.Common;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace FangJia.DataAccess;

public class FormulationManager
{
    private static readonly string DatabasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data.db");
    private readonly string _connectionString = $"Data Source={DatabasePath};";

    /// <summary>
    /// 获取所有大类（FirstCategory）
    /// </summary>
    public async IAsyncEnumerable<FormulationCategory> GetFirstCategoriesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    public async IAsyncEnumerable<FormulationCategory> GetSecondCategoriesAsync(string firstCategory, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT Id, SecondCategory FROM Category WHERE FirstCategory = @FirstCategory ORDER BY Id ASC";
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
    public async IAsyncEnumerable<FormulationCategory> GetFormulationsAsync(int categoryId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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
}