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
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT DISTINCT FirstCategory FROM Category ORDER BY Id ASC";
        await using var command = new SqliteCommand(sql, connection);
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
        [EnumeratorCancellation]
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
        [EnumeratorCancellation]
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

    public async Task UpdateFormulationAsync(int formulationId, params (string key, string value)[]? tuples)
    {
        // 如果没有传入更新项，则直接返回
        if (tuples == null || tuples.Length == 0)
            return;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // 构造 UPDATE 语句的 SET 部分
        var setClauses = new List<string>();
        var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@Id", formulationId);
        for (var i = 0; i < tuples.Length; i++)
        {
            var (key, value) = tuples[i];

            // 注意：直接将 key 插入 SQL 可能存在风险，
            // 在实际使用中应对 key 做校验或限制为允许更新的列名
            setClauses.Add($"{key} = @p{i}");
            command.Parameters.AddWithValue($"@p{i}", value);
        }

        // 此处假设表名为 Formulation，并且要更新的记录只有一行（没有 WHERE 条件）
        command.CommandText = $"UPDATE Formulation SET {string.Join(", ", setClauses)} WHERE Id = @Id";

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> InsertFormulationComposition(FormulationComposition composition)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            const string sql =
                "INSERT INTO FormulationComposition (FormulationId, DrugId, DrugName, Effect, Position, Notes) " +
                "VALUES (@FormulationId, @DrugId, @DrugName, @Effect, @Position, @Notes); " +
                "SELECT last_insert_rowid();";
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@FormulationId", composition.FormulationId);
            command.Parameters.AddWithValue("@DrugId", composition.DrugId);
            command.Parameters.AddWithValue("@DrugName", composition.DrugName);
            command.Parameters.AddWithValue("@Effect", composition.Effect);
            command.Parameters.AddWithValue("@Position", composition.Position);
            command.Parameters.AddWithValue("@Notes", composition.Notes);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            throw;
        }
    }


    public async Task DeleteFormulationComposition(int compositionId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        const string sql = "DELETE FROM FormulationComposition WHERE Id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", compositionId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateFormulationComposition(int id, params (string key, string? value)[]? tuples)
    {
        if (tuples == null) return;
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var setClauses = new List<string>();
        var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@Id", id);
        for (var i = 0; i < tuples.Length; i++)
        {
            var (key, value) = tuples[i];
            setClauses.Add($"{key} = @p{i}");
            command.Parameters.AddWithValue($"@p{i}", value);
        }

        command.CommandText = $"UPDATE FormulationComposition SET {string.Join(", ", setClauses)} WHERE Id = @Id";
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteCategory(int id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        const string sql = "DELETE FROM Category WHERE Id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteFormulation(int id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        const string sql = "DELETE FROM Formulation WHERE Id = @Id";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> InsertFormulationAsync(Formulation formulation)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        const string sql =
            "INSERT INTO Formulation (Name, CategoryId, Usage, Effect, Indication, Disease, Application, Supplement, Song, Notes, Source) " +
            "VALUES (@Name, @CategoryId, @Usage, @Effect, @Indication, @Disease, @Application, @Supplement, @Song, @Notes, @Source);" +
            "SELECT last_insert_rowid();";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", formulation.Name);
        command.Parameters.AddWithValue("@CategoryId", formulation.CategoryId);
        command.Parameters.AddWithValue("@Usage", formulation.Usage);
        command.Parameters.AddWithValue("@Effect", formulation.Effect);
        command.Parameters.AddWithValue("@Indication", formulation.Indication);
        command.Parameters.AddWithValue("@Disease", formulation.Disease);
        command.Parameters.AddWithValue("@Application", formulation.Application);
        command.Parameters.AddWithValue("@Supplement", formulation.Supplement);
        command.Parameters.AddWithValue("@Song", formulation.Song);
        command.Parameters.AddWithValue("@Notes", formulation.Notes);
        command.Parameters.AddWithValue("@Source", formulation.Source);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<int> InsertCategoryAsync(string firstCategory, string secondCategory)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        const string sql = "INSERT INTO Category (FirstCategory, SecondCategory) VALUES (@FirstCategory, @SecondCategory); " +
                           "SELECT last_insert_rowid();";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@FirstCategory", firstCategory);
        command.Parameters.AddWithValue("@SecondCategory", secondCategory);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}