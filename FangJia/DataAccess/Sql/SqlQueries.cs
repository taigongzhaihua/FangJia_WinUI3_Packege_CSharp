//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace FangJia.DataAccess.Sql;

/// <summary>
/// SQL 语句集中管理类 - 包含应用程序所有 SQL 查询
/// </summary>
public static class SqlQueries
{
    #region 数据库管理查询

    /// <summary>
    /// 数据库管理相关查询
    /// </summary>
    public static class Database
    {
        /// <summary>
        /// 创建数据库表结构 SQL
        /// </summary>
        public const string CreateTables = """
            CREATE TABLE IF NOT EXISTS Category(
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstCategory TEXT NOT NULL,
                SecondCategory TEXT NOT NULL
            );
            
            CREATE TABLE IF NOT EXISTS Drug (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL COLLATE NOCASE,
                EnglishName TEXT COLLATE NOCASE,
                LatinName   TEXT COLLATE NOCASE,
                Category    TEXT,
                Origin      TEXT,
                Properties  TEXT,
                Quality     TEXT,
                Taste       TEXT,
                Meridian    TEXT,
                Effect      TEXT,
                Notes       TEXT,
                Processed   TEXT,
                Source      TEXT
            );
            
            CREATE INDEX IF NOT EXISTS idx_drug_name ON Drug(Name);
            CREATE INDEX IF NOT EXISTS idx_drug_englishname ON Drug(EnglishName);
            
            CREATE TABLE IF NOT EXISTS DrugImage (
                Id     INTEGER PRIMARY KEY AUTOINCREMENT,
                DrugId INTEGER NOT NULL,
                Image  BLOB,
                FOREIGN KEY (DrugId) REFERENCES Drug(Id) ON DELETE CASCADE
            );
            
            CREATE TABLE IF NOT EXISTS Formulation (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL COLLATE NOCASE,
                CategoryId  INTEGER,
                Usage       TEXT,
                Effect      TEXT,
                Indication  TEXT,
                Disease     TEXT,
                Application TEXT,
                Supplement  TEXT,
                Song        TEXT,
                Notes       TEXT,
                Source      TEXT,
                FOREIGN KEY (CategoryId) REFERENCES Category(Id) ON DELETE CASCADE
            );
            
            CREATE INDEX IF NOT EXISTS idx_formulation_name ON Formulation(Name);
            CREATE INDEX IF NOT EXISTS idx_formulation_category ON Formulation(CategoryId);
            
            CREATE TABLE IF NOT EXISTS FormulationComposition (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                FormulationId INTEGER NOT NULL,
                DrugID        INTEGER REFERENCES Drug(Id),
                DrugName      TEXT NOT NULL,
                Effect        TEXT,
                Position      TEXT,
                Notes         TEXT,
                FOREIGN KEY (FormulationId) REFERENCES Formulation(Id) ON DELETE CASCADE
            );
            
            CREATE INDEX IF NOT EXISTS idx_composition_formulation ON FormulationComposition(FormulationId);
            CREATE INDEX IF NOT EXISTS idx_composition_drug ON FormulationComposition(DrugID);
            
            CREATE TABLE IF NOT EXISTS FormulationImage (
                Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                FormulationId INTEGER NOT NULL,
                Image         BLOB,
                FOREIGN KEY (FormulationId) REFERENCES Formulation(Id) ON DELETE CASCADE
            );
            
            CREATE INDEX IF NOT EXISTS idx_formimage_formulation ON FormulationImage(FormulationId);
            """;

        /// <summary>
        /// 数据库性能优化设置
        /// </summary>
        public const string OptimizationPragmas = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA cache_size = 10000;
            PRAGMA temp_store = MEMORY;
            PRAGMA mmap_size = 30000000;
            PRAGMA foreign_keys = ON;
            PRAGMA auto_vacuum = INCREMENTAL;
            PRAGMA optimize;
            """;

        /// <summary>
        /// 数据库维护查询
        /// </summary>
        public const string DatabaseMaintenance = """
            PRAGMA optimize;
            PRAGMA vacuum;
            PRAGMA incremental_vacuum;
            PRAGMA wal_checkpoint(FULL);
            PRAGMA analysis_limit=1000;
            PRAGMA automatic_index=ON;
            ANALYZE;
            """;

        /// <summary>
        /// 检查表存在查询模板
        /// </summary>
        public const string TableExists = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@TableName;";
    }

    #endregion


    #region 药物管理查询

    /// <summary>
    /// 药物相关查询
    /// </summary>
    public static class Drug
    {
        /// <summary>
        /// 获取药物摘要列表
        /// </summary>
        public const string GetDrugSummaryList = "SELECT Id, Name, Category FROM Drug";

        /// <summary>
        /// 获取所有药物详细信息
        /// </summary>
        public const string GetDrugList = "SELECT * FROM Drug";

        /// <summary>
        /// 通过ID获取药物
        /// </summary>
        public const string GetDrug = "SELECT * FROM Drug WHERE Id = @Id";

        /// <summary>
        /// 通过ID获取药物图片
        /// </summary>
        public const string GetDrugImage = "SELECT * FROM DrugImage WHERE DrugId = @Id";

        /// <summary>
        /// 插入药物
        /// </summary>
        public const string InsertDrug = """
            INSERT INTO Drug (
                Name, EnglishName, LatinName, Category, Origin, 
                Properties, Quality, Taste, Meridian, Effect, 
                Notes, Processed, Source
            ) VALUES (
                @Name, @EnglishName, @LatinName, @Category, @Origin, 
                @Properties, @Quality, @Taste, @Meridian, @Effect, 
                @Notes, @Processed, @Source
            );
            SELECT last_insert_rowid();
            """;

        /// <summary>
        /// 插入药物图片
        /// </summary>
        public const string InsertDrugImage = """
            INSERT INTO DrugImage (DrugId, Image) 
            VALUES (@DrugId, @Image);
            SELECT last_insert_rowid();
            """;

        /// <summary>
        /// 更新药物图片
        /// </summary>
        public const string UpdateDrugImage = "UPDATE DrugImage SET Image = @Image WHERE Id = @Id";

        /// <summary>
        /// 删除药物
        /// </summary>
        public const string DeleteDrug = "DELETE FROM Drug WHERE Id = @Id";

        /// <summary>
        /// 删除药物图片
        /// </summary>
        public const string DeleteDrugImage = "DELETE FROM DrugImage WHERE DrugId = @Id";

        /// <summary>
        /// 搜索药物
        /// </summary>
        public const string SearchDrugs = "SELECT Id, Name, Category FROM Drug WHERE Name LIKE @SearchTerm OR EnglishName LIKE @SearchTerm OR LatinName LIKE @SearchTerm";

        /// <summary>
        /// 完整更新药物
        /// </summary>
        public const string UpdateDrugFull = """
            UPDATE Drug SET 
                Name = @Name, 
                EnglishName = @EnglishName, 
                LatinName = @LatinName,
                Category = @Category, 
                Origin = @Origin, 
                Properties = @Properties,
                Quality = @Quality, 
                Taste = @Taste, 
                Meridian = @Meridian,
                Effect = @Effect, 
                Notes = @Notes, 
                Processed = @Processed,
                Source = @Source
            WHERE Id = @Id
            """;

        /// <summary>
        /// 生成动态更新SQL
        /// </summary>
        /// <param name="id">药物ID</param>
        /// <param name="keys">要更新的字段</param>
        /// <returns>更新SQL语句</returns>
        public static string BuildUpdateSql(int id, string[] keys)
        {
            var set = string.Join(", ", keys.Select(k => $"{k} = @{k}"));
            return $"UPDATE Drug SET {set} WHERE Id = {id}";
        }
    }

    #endregion

    #region 方剂管理查询

    /// <summary>
    /// 方剂分类相关查询
    /// </summary>
    public static class Category
    {
        /// <summary>
        /// 获取一级分类
        /// </summary>
        public const string GetFirstCategories = "SELECT DISTINCT FirstCategory FROM Category ORDER BY Id ASC";

        /// <summary>
        /// 获取二级分类
        /// </summary>
        public const string GetSecondCategories =
            "SELECT Id, SecondCategory FROM Category WHERE FirstCategory = @FirstCategory ORDER BY Id ASC";

        /// <summary>
        /// 获取所有二级分类
        /// </summary>
        public const string GetAllSecondCategories =
            """
            SELECT Id, FirstCategory, SecondCategory 
                FROM Category 
                ORDER BY FirstCategory, Id ASC
            """;

        /// <summary>
        /// 插入分类
        /// </summary>
        public const string InsertCategory =
            """
            INSERT INTO Category (FirstCategory, SecondCategory) 
                VALUES (@FirstCategory, @SecondCategory);
            SELECT last_insert_rowid();
            """;

        /// <summary>
        /// 删除分类
        /// </summary>
        public const string DeleteCategory = "DELETE FROM Category WHERE Id = @Id";

        /// <summary>
        /// 获取分类信息
        /// </summary>
        public const string GetCategoryById = "SELECT FirstCategory FROM Category WHERE Id = @Id";
    }

    /// <summary>
    /// 方剂相关查询
    /// </summary>
    public static class Formulation
    {
        /// <summary>
        /// 获取方剂列表
        /// </summary>
        public const string GetFormulations =
            "SELECT Id, Name FROM Formulation WHERE CategoryId = @CategoryId ORDER BY Id ASC";

        /// <summary>
        /// 获取方剂详情
        /// </summary>
        public const string GetFormulationById =
            """
            SELECT Id, Name, CategoryId, Usage, Effect, Indication, Disease, 
                   Application, Supplement, Song, Notes, Source FROM Formulation 
                   WHERE Id = @FormulationId
            """;

        /// <summary>
        /// 获取所有方剂基础信息
        /// </summary>
        public const string GetAllFormulationsBasic =
            """
            SELECT Id, Name, CategoryId 
                FROM Formulation 
                ORDER BY CategoryId, Id ASC
            """;

        /// <summary>
        /// 插入方剂
        /// </summary>
        public const string InsertFormulation =
            """
            INSERT INTO Formulation 
                (Name, CategoryId, Usage, Effect, Indication, Disease, 
                Application, Supplement, Song, Notes, Source)
            VALUES (@Name, @CategoryId, @Usage, @Effect, @Indication, @Disease, 
                @Application, @Supplement, @Song, @Notes, @Source);
            SELECT last_insert_rowid();
            """;

        /// <summary>
        /// 删除方剂
        /// </summary>
        public const string DeleteFormulation = "DELETE FROM Formulation WHERE Id = @Id";

        /// <summary>
        /// 搜索方剂
        /// </summary>
        public const string SearchFormulations =
            """
            SELECT f.Id, f.Name 
            FROM Formulation f 
            WHERE f.Name LIKE @SearchTerm 
                OR f.Effect LIKE @SearchTerm 
                OR f.Indication LIKE @SearchTerm 
            ORDER BY f.Name
            """;

        /// <summary>
        /// 生成更新方剂SQL
        /// </summary>
        /// <param name="fields">要更新的字段</param>
        /// <returns>更新SQL</returns>
        public static string BuildUpdateSql(IEnumerable<string> fields)
        {
            var setClauses = string.Join(", ", fields.Select(f => $"{f} = @{f}"));
            return $"UPDATE Formulation SET {setClauses} WHERE Id = @Id";
        }
    }

    /// <summary>
    /// 方剂组成相关查询
    /// </summary>
    public static class Composition
    {
        /// <summary>
        /// 获取方剂组成
        /// </summary>
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

        /// <summary>
        /// 插入方剂组成
        /// </summary>
        public const string InsertFormulationComposition =
            """
            INSERT INTO FormulationComposition 
                (FormulationId, DrugId, DrugName, Effect, Position, Notes)
            VALUES (@FormulationId, @DrugId, @DrugName, @Effect, @Position, @Notes);
            SELECT last_insert_rowid();
            """;

        /// <summary>
        /// 删除方剂组成
        /// </summary>
        public const string DeleteFormulationComposition = "DELETE FROM FormulationComposition WHERE Id = @Id";

        /// <summary>
        /// 获取组成所属方剂ID
        /// </summary>
        public const string GetCompositionFormulationId =
            "SELECT FormulationId FROM FormulationComposition WHERE Id = @Id";

        /// <summary>
        /// 生成更新组成SQL
        /// </summary>
        /// <param name="fields">要更新的字段</param>
        /// <returns>更新SQL</returns>
        public static string BuildUpdateSql(IEnumerable<string> fields)
        {
            var setClauses = string.Join(", ", fields.Select(f => $"{f} = @{f}"));
            return $"UPDATE FormulationComposition SET {setClauses} WHERE Id = @Id";
        }
    }

    /// <summary>
    /// 图片相关查询
    /// </summary>
    public static class Image
    {
        /// <summary>
        /// 获取方剂图片
        /// </summary>
        public const string GetFormulationImage =
            "SELECT Id, Image FROM FormulationImage WHERE FormulationId = @FormulationId";

        /// <summary>
        /// 插入方剂图片
        /// </summary>
        public const string InsertFormulationImage =
            """
            INSERT INTO FormulationImage (FormulationId, Image) 
            VALUES (@FormulationId, @Image);
            SELECT last_insert_rowid();
            """;

        /// <summary>
        /// 更新方剂图片
        /// </summary>
        public const string UpdateFormulationImage =
            "UPDATE FormulationImage SET Image = @Image WHERE FormulationId = @FormulationId";

        /// <summary>
        /// 删除方剂图片
        /// </summary>
        public const string DeleteFormulationImage =
            "DELETE FROM FormulationImage WHERE FormulationId = @FormulationId";

        /// <summary>
        /// 检查图片是否存在
        /// </summary>
        public const string CheckImageExists =
            "SELECT Id FROM FormulationImage WHERE FormulationId = @FormulationId";
    }

    /// <summary>
    /// 统计信息查询
    /// </summary>
    public static class Statistics
    {
        /// <summary>
        /// 获取方剂统计数据
        /// </summary>
        public const string GetFormulationStats =
            """
            SELECT COUNT(*) FROM Formulation;
            SELECT COUNT(*) FROM Category;
            SELECT COUNT(*) FROM FormulationComposition;
            SELECT COUNT(DISTINCT DrugId) FROM FormulationComposition;
            """;
    }

    #endregion

    /// <summary>
    /// 参数转换助手方法 - 将对象字典转换为参数列表
    /// </summary>
    public static List<(string name, object? value)> CreateParameters(Dictionary<string, object?> parameters)
    {
        return [.. parameters.Select(p => (p.Key, p.Value))];
    }
}