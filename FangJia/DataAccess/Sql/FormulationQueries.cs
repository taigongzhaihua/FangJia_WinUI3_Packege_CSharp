using System;

namespace FangJia.DataAccess.Sql;

/// <summary>
/// 方剂管理器 SQL 语句集中管理类
/// </summary>
public static class FormulationQueries
{
    // 分类相关查询
    public static class Category
    {
        public const string GetFirstCategories = "SELECT DISTINCT FirstCategory FROM Category ORDER BY Id ASC";

        public const string GetSecondCategories =
            "SELECT Id, SecondCategory FROM Category WHERE FirstCategory = @FirstCategory ORDER BY Id ASC";

        public const string GetAllSecondCategories =
            """
            SELECT Id, FirstCategory, SecondCategory 
                FROM Category 
                ORDER BY FirstCategory, Id ASC
            """;

        public const string InsertCategory =
            """
            INSERT INTO Category (FirstCategory, SecondCategory) 
                VALUES (@FirstCategory, @SecondCategory);
            SELECT last_insert_rowid();
            """;

        public const string DeleteCategory = "DELETE FROM Category WHERE Id = @Id";

        public const string GetCategoryById = "SELECT FirstCategory FROM Category WHERE Id = @Id";
    }

    // 方剂相关查询
    public static class Formulation
    {
        public const string GetFormulations =
            "SELECT Id, Name FROM Formulation WHERE CategoryId = @CategoryId ORDER BY Id ASC";

        public const string GetFormulationById =
            """
            SELECT Id, Name, CategoryId, Usage, Effect, Indication, Disease, 
                   Application, Supplement, Song, Notes, Source FROM Formulation 
                   WHERE Id = @FormulationId
            """;

        public const string GetAllFormulationsBasic =
            """
            SELECT Id, Name, CategoryId 
                FROM Formulation 
                ORDER BY CategoryId, Id ASC
            """;

        public const string InsertFormulation =
            """
            INSERT INTO Formulation 
                (Name, CategoryId, Usage, Effect, Indication, Disease, 
                Application, Supplement, Song, Notes, Source)
            VALUES (@Name, @CategoryId, @Usage, @Effect, @Indication, @Disease, 
                @Application, @Supplement, @Song, @Notes, @Source);
            SELECT last_insert_rowid();
            """;

        public const string DeleteFormulation = "DELETE FROM Formulation WHERE Id = @Id";

        public const string SearchFormulations =
            """
            SELECT f.Id, f.Name 
            FROM Formulation f 
            WHERE f.Name LIKE @SearchTerm 
                OR f.Effect LIKE @SearchTerm 
                OR f.Indication LIKE @SearchTerm 
            ORDER BY f.Name
            """;
    }

    // 方剂组成相关查询
    public static class Composition
    {
        public const string GetFormulationCompositions =
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
            WHERE fc.FormulationId = @FormulationId
            """;

        public const string InsertFormulationComposition =
            """
            INSERT INTO FormulationComposition 
                (FormulationId, DrugId, DrugName, Effect, Position, Notes)
            VALUES (@FormulationId, @DrugId, @DrugName, @Effect, @Position, @Notes);
            SELECT last_insert_rowid();
            """;

        public const string DeleteFormulationComposition = "DELETE FROM FormulationComposition WHERE Id = @Id";

        public const string GetCompositionFormulationId =
            "SELECT FormulationId FROM FormulationComposition WHERE Id = @Id";
    }

    // 图片相关查询
    public static class Image
    {
        public const string GetFormulationImage =
            "SELECT Id, Image FROM FormulationImage WHERE FormulationId = @FormulationId";

        public const string InsertFormulationImage =
            """
            INSERT INTO FormulationImage (FormulationId, Image) 
            VALUES (@FormulationId, @Image);
            SELECT last_insert_rowid();
            """;

        public const string UpdateFormulationImage =
            "UPDATE FormulationImage SET Image = @Image WHERE FormulationId = @FormulationId";

        public const string DeleteFormulationImage =
            "DELETE FROM FormulationImage WHERE FormulationId = @FormulationId";

        public const string CheckImageExists =
            "SELECT Id FROM FormulationImage WHERE FormulationId = @FormulationId";
    }

    // 统计信息查询
    public static class Statistics
    {
        public const string GetFormulationStats =
            """
            SELECT COUNT(*) FROM Formulation;
            SELECT COUNT(*) FROM Category;
            SELECT COUNT(*) FROM FormulationComposition;
            SELECT COUNT(DISTINCT DrugId) FROM FormulationComposition;
            """;
    }

    /// <summary>
    /// 生成更新SQL语句（通用方法）
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="idColumnName">主键列名</param>
    /// <param name="id">主键值</param>
    /// <param name="keys">要更新的字段</param>
    /// <returns>更新SQL语句</returns>
    public static string BuildUpdateSql(string tableName, string idColumnName, object id, string[] keys)
    {
        var setClauses = string.Join(", ", Array.ConvertAll(keys, k => $"{k} = @{k}"));
        return $"UPDATE {tableName} SET {setClauses} WHERE {idColumnName} = @{idColumnName}";
    }
}