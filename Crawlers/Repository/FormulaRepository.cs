using Crawlers.Models;
using LiteDB;
using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace Crawlers.Repository;

// 定义JSON序列化源生成器上下文
[JsonSerializable(typeof(ChineseFormula))]
[JsonSerializable(typeof(List<ChineseFormula>))]
[JsonSerializable(typeof(FormulaDataSource))]
[JsonSerializable(typeof(FormulaItem))]
internal partial class FormulaJsonContext : JsonSerializerContext
{
}

public class FormulaRepository : IDisposable
{
    // LiteDB配置
    private LiteDatabase _liteDb;
    private ILiteCollection<ChineseFormula> _formulasCollection;
    private readonly string _liteDbPath;

    // SQLite配置
    private readonly SqliteConnection _sqlConnection;
    private readonly string _sqliteDbPath;

    // JSON文件配置
    private readonly string _jsonBasePath;

    // 序列化配置
    private readonly JsonSerializerOptions _jsonOptions;

    public FormulaRepository(string liteDbPath = "ChineseFormula.litedb",
        string sqliteDbPath = "ChineseFormula.db",
        string jsonBasePath = "ChineseFormulaData")
    {
        _liteDbPath = liteDbPath;
        _sqliteDbPath = sqliteDbPath;

        // 尝试连接LiteDB
        InitializeLiteDb();

        // 初始化SQLite
        _sqlConnection = new SqliteConnection($"Data Source={sqliteDbPath}");
        InitializeSqliteDatabase();

        // 初始化JSON文件存储路径
        _jsonBasePath = jsonBasePath;
        Directory.CreateDirectory(_jsonBasePath);

        // 初始化JSON序列化选项，使用源生成器
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver = FormulaJsonContext.Default
        };
    }

    private void InitializeLiteDb()
    {
        try
        {
            // 尝试打开数据库，共享读取
            var connectionString = new ConnectionString
            {
                Filename = _liteDbPath,
                Connection = ConnectionType.Shared
            };

            _liteDb = new LiteDatabase(connectionString);
            _formulasCollection = _liteDb.GetCollection<ChineseFormula>("formulas");

            // 创建索引
            _formulasCollection.EnsureIndex(x => x.Name, true);  // unique
            _formulasCollection.EnsureIndex(x => x.FirstLetter);
            _formulasCollection.EnsureIndex(x => x.PinYin);

            Console.WriteLine("LiteDB 初始化成功");
        }
        catch (IOException ex) when (ex.Message.Contains("because it is being used by another process"))
        {
            Console.WriteLine($"警告: LiteDB数据库文件被锁定，创建临时数据库。原因: {ex.Message}");

            // 创建带时间戳的临时文件名
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var tempPath = Path.Combine(
                Path.GetDirectoryName(_liteDbPath) ?? ".",
                $"{Path.GetFileNameWithoutExtension(_liteDbPath)}_{timestamp}{Path.GetExtension(_liteDbPath)}");

            Console.WriteLine($"使用临时数据库文件: {tempPath}");

            _liteDb = new LiteDatabase(tempPath);
            _formulasCollection = _liteDb.GetCollection<ChineseFormula>("formulas");

            // 创建索引
            _formulasCollection.EnsureIndex(x => x.Name, true);  // unique
            _formulasCollection.EnsureIndex(x => x.FirstLetter);
            _formulasCollection.EnsureIndex(x => x.PinYin);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LiteDB 初始化失败: {ex.Message}");
            throw;
        }
    }

    private void InitializeSqliteDatabase()
    {
        try
        {
            _sqlConnection.Open();

            // 创建表结构
            var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS Formulas (
                        FormulaID INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        PinYin TEXT,
                        FirstLetter TEXT,
                        Aliases TEXT,
                        Composition TEXT,
                        Indications TEXT,
                        CreatedAt TEXT,
                        UpdatedAt TEXT
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_formula_name ON Formulas(Name);
                    CREATE INDEX IF NOT EXISTS idx_formula_pinyin ON Formulas(PinYin);
                    CREATE INDEX IF NOT EXISTS idx_formula_letter ON Formulas(FirstLetter);
                ";

            using (var command = _sqlConnection.CreateCommand())
            {
                command.CommandText = createTableSql;
                command.ExecuteNonQuery();
            }

            _sqlConnection.Close();
            Console.WriteLine("SQLite 初始化成功");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQLite 初始化失败: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> SaveFormulaAsync(ChineseFormula formula)
    {
        try
        {
            // 确保拼音和首字母已设置
            if (string.IsNullOrEmpty(formula.PinYin))
            {
                formula.PinYin = GetPinyin(formula.Name);
                formula.FirstLetter = !string.IsNullOrEmpty(formula.PinYin) ?
                    formula.PinYin[..1].ToUpper() : "?";
            }

            // 更新索引属性
            formula.UpdateIndexProperties();

            // 设置更新时间
            formula.UpdatedAt = DateTime.UtcNow;

            // 1. 保存到LiteDB
            var dbId = SaveToLiteDb(formula);

            // 2. 更新SQLite索引
            await UpdateSqliteIndexAsync(formula);

            // 3. 保存JSON文件
            SaveToJsonFile(formula);

            return true;
        }
        catch (Exception ex)
        {
            // 记录错误，但不中断程序
            Console.WriteLine($"保存方剂 {formula.Name} 时出错: {ex.Message}");
            return false;
        }
    }

    private int SaveToLiteDb(ChineseFormula formula)
    {
        try
        {
            // 查找是否存在同名记录
            var existingFormula = _formulasCollection.FindOne(x => x.Name == formula.Name);

            if (existingFormula != null)
            {
                // 如果存在，更新记录，保留ID
                formula.Id = existingFormula.Id;
                _formulasCollection.Update(formula);
                Console.WriteLine($"LiteDB: 更新方剂 {formula.Name} (ID: {formula.Id})");
                return formula.Id;
            }
            else
            {
                // 如果不存在，插入新记录
                var id = _formulasCollection.Insert(formula);
                Console.WriteLine($"LiteDB: 新增方剂 {formula.Name} (ID: {id})");
                return (int)id;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LiteDB操作失败: {ex.Message}");
            // 返回一个临时ID，不影响程序继续运行
            return -1;
        }
    }

    private async Task UpdateSqliteIndexAsync(ChineseFormula formula)
    {
        try
        {
            await _sqlConnection.OpenAsync();

            using (var command = _sqlConnection.CreateCommand())
            {
                command.CommandText = @"
                        INSERT INTO Formulas (FormulaID, Name, PinYin, FirstLetter, Aliases, Composition, Indications, CreatedAt, UpdatedAt)
                        VALUES (@Id, @Name, @PinYin, @FirstLetter, @AllAliases, @AllComposition, @AllIndications, @CreatedAt, @UpdatedAt)
                        ON CONFLICT(FormulaID) DO UPDATE SET
                        Name = @Name,
                        PinYin = @PinYin,
                        FirstLetter = @FirstLetter,
                        Aliases = @AllAliases,
                        Composition = @AllComposition,
                        Indications = @AllIndications,
                        UpdatedAt = @UpdatedAt
                    ";

                command.Parameters.AddWithValue("@Id", formula.Id);
                command.Parameters.AddWithValue("@Name", formula.Name);
                command.Parameters.AddWithValue("@PinYin", formula.PinYin ?? string.Empty);
                command.Parameters.AddWithValue("@FirstLetter", formula.FirstLetter ?? string.Empty);
                command.Parameters.AddWithValue("@AllAliases", formula.AllAliases ?? string.Empty);
                command.Parameters.AddWithValue("@AllComposition", formula.AllComposition ?? string.Empty);
                command.Parameters.AddWithValue("@AllIndications", formula.AllIndications ?? string.Empty);
                command.Parameters.AddWithValue("@CreatedAt", formula.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("@UpdatedAt", formula.UpdatedAt.ToString("O"));

                await command.ExecuteNonQueryAsync();
            }

            _sqlConnection.Close();
            Console.WriteLine($"SQLite: 索引更新 {formula.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQLite操作失败: {ex.Message}");
            // 忽略错误，允许程序继续
        }
    }

    private void SaveToJsonFile(ChineseFormula formula)
    {
        try
        {
            // 按首字母创建子目录
            var letter = !string.IsNullOrEmpty(formula.FirstLetter) ? formula.FirstLetter : "Other";
            var categoryPath = Path.Combine(_jsonBasePath, letter);
            Directory.CreateDirectory(categoryPath);

            // 创建安全的文件名
            var safeFileName = GetSafeFileName(formula.Name);
            var filePath = Path.Combine(categoryPath, $"{safeFileName}.json");

            // 使用源生成的序列化器而不是反射
            var jsonString = System.Text.Json.JsonSerializer.Serialize(
                formula,
                FormulaJsonContext.Default.ChineseFormula
            );

            File.WriteAllText(filePath, jsonString, Encoding.UTF8);

            Console.WriteLine($"JSON: 保存文件 {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存JSON文件时出错: {ex.Message}");
        }
    }

    // 根据方剂名获取拼音
    private string GetPinyin(string chineseName)
    {
        try
        {
            // 使用NPinyin库获取拼音
            return NPinyin.Pinyin.GetPinyin(chineseName).Replace(" ", "");
        }
        catch
        {
            // 如果转换失败，返回原名
            return chineseName;
        }
    }

    // 生成文件系统安全的文件名
    private string GetSafeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }

    // 查询方法：根据名称查找方剂
    public ChineseFormula GetFormulaByName(string name)
    {
        try
        {
            return _formulasCollection.FindOne(x => x.Name == name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询方剂 {name} 失败: {ex.Message}");
            return null;
        }
    }

    // 异步包装方法
    public Task<ChineseFormula> GetFormulaByNameAsync(string name)
    {
        return Task.FromResult(GetFormulaByName(name));
    }

    // 查询方法：根据拼音首字母查找方剂
    public List<ChineseFormula> GetFormulasByFirstLetter(string letter)
    {
        try
        {
            return _formulasCollection.Find(x => x.FirstLetter.Equals(letter, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询首字母 {letter} 的方剂失败: {ex.Message}");
            return [];
        }
    }

    // 异步包装方法
    public Task<List<ChineseFormula>> GetFormulasByFirstLetterAsync(string letter)
    {
        return Task.FromResult(GetFormulasByFirstLetter(letter));
    }

    // 获取所有方剂统计信息
    public Dictionary<string, int> GetFormulaStatistics()
    {
        var stats = new Dictionary<string, int>();

        try
        {
            // 总数
            stats["Total"] = _formulasCollection.Count();

            // 按首字母分组统计
            var letters = _formulasCollection
                .FindAll()
                .Select(x => x.FirstLetter)
                .GroupBy(x => x)
                .Select(g => new { Letter = g.Key, Count = g.Count() });

            foreach (var group in letters)
            {
                stats[group.Letter] = group.Count;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取统计信息失败: {ex.Message}");
            stats["Total"] = 0;
        }

        return stats;
    }

    // 释放资源
    public void Dispose()
    {
        try
        {
            _sqlConnection?.Dispose();
            _liteDb?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"关闭数据库连接时出错: {ex.Message}");
        }
    }
}

// 用于SQLite查询结果的简单DTO类
public class FormulaBrief
{
    public string Name { get; set; } = string.Empty;
    public string PinYin { get; set; } = string.Empty;
    public string FirstLetter { get; set; } = string.Empty;
    public string Aliases { get; set; } = string.Empty;
    public string Composition { get; set; } = string.Empty;
    public string Indications { get; set; } = string.Empty;
}