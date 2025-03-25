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
[JsonSerializable(typeof(DictionaryEntry))]
[JsonSerializable(typeof(List<DictionaryEntry>))]
[JsonSerializable(typeof(DictionaryContent))]
internal partial class DictionaryJsonContext : JsonSerializerContext
{
}

public class DictionaryRepository : IDisposable
{
    // LiteDB配置
    private LiteDatabase _liteDb;
    private ILiteCollection<DictionaryEntry> _entriesCollection;
    private readonly string _liteDbPath;

    // SQLite配置
    private readonly SqliteConnection _sqlConnection;
    private readonly string _sqliteDbPath;

    // JSON文件配置
    private readonly string _jsonBasePath;

    // 序列化配置
    private readonly JsonSerializerOptions _jsonOptions;

    public DictionaryRepository(string liteDbPath = "TCMDictionary.litedb",
        string sqliteDbPath = "TCMDictionary.db",
        string jsonBasePath = "TCMDictionaryData")
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
            TypeInfoResolver = DictionaryJsonContext.Default
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
            _entriesCollection = _liteDb.GetCollection<DictionaryEntry>("entries");

            // 创建索引
            _entriesCollection.EnsureIndex(x => x.Term, true);  // 词条名称唯一索引
            _entriesCollection.EnsureIndex(x => x.FirstLetter);
            _entriesCollection.EnsureIndex(x => x.PinYin);
            _entriesCollection.EnsureIndex(x => x.Category);
            _entriesCollection.EnsureIndex("$.SearchText", false);  // 全文搜索索引

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
            _entriesCollection = _liteDb.GetCollection<DictionaryEntry>("entries");

            // 创建索引
            _entriesCollection.EnsureIndex(x => x.Term, true);
            _entriesCollection.EnsureIndex(x => x.FirstLetter);
            _entriesCollection.EnsureIndex(x => x.PinYin);
            _entriesCollection.EnsureIndex(x => x.Category);
            _entriesCollection.EnsureIndex("$.SearchText", false);
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
                    CREATE TABLE IF NOT EXISTS DictionaryEntries (
                        EntryID INTEGER PRIMARY KEY,
                        Term TEXT NOT NULL,
                        PinYin TEXT,
                        FirstLetter TEXT,
                        Category TEXT,
                        DefinitionBrief TEXT,
                        CreatedAt TEXT,
                        UpdatedAt TEXT
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_entry_term ON DictionaryEntries(Term);
                    CREATE INDEX IF NOT EXISTS idx_entry_pinyin ON DictionaryEntries(PinYin);
                    CREATE INDEX IF NOT EXISTS idx_entry_letter ON DictionaryEntries(FirstLetter);
                    CREATE INDEX IF NOT EXISTS idx_entry_category ON DictionaryEntries(Category);
                    CREATE INDEX IF NOT EXISTS idx_entry_definition ON DictionaryEntries(DefinitionBrief);
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

    public async Task<bool> SaveEntryAsync(DictionaryEntry entry)
    {
        try
        {
            // 确保拼音和首字母已设置
            if (string.IsNullOrEmpty(entry.PinYin))
            {
                entry.PinYin = GetPinyin(entry.Term);
                entry.FirstLetter = !string.IsNullOrEmpty(entry.PinYin) ?
                    entry.PinYin.Substring(0, 1).ToUpper() : "?";
            }

            // 更新搜索文本
            entry.UpdateSearchText();

            // 设置更新时间
            entry.UpdatedAt = DateTime.UtcNow;

            // 1. 保存到LiteDB
            var dbId = SaveToLiteDb(entry);

            // 2. 更新SQLite索引
            await UpdateSqliteIndexAsync(entry);

            // 3. 保存JSON文件
            SaveToJsonFile(entry);

            return true;
        }
        catch (Exception ex)
        {
            // 记录错误，但不中断程序
            Console.WriteLine($"保存词条 {entry.Term} 时出错: {ex.Message}");
            return false;
        }
    }

    private int SaveToLiteDb(DictionaryEntry entry)
    {
        try
        {
            // 查找是否存在同名记录
            var existingEntry = _entriesCollection.FindOne(x => x.Term == entry.Term);

            if (existingEntry != null)
            {
                // 如果存在，更新记录，保留ID
                entry.Id = existingEntry.Id;
                _entriesCollection.Update(entry);
                Console.WriteLine($"LiteDB: 更新词条 {entry.Term} (ID: {entry.Id})");
                return entry.Id;
            }
            else
            {
                // 如果不存在，插入新记录
                var id = _entriesCollection.Insert(entry);
                Console.WriteLine($"LiteDB: 新增词条 {entry.Term} (ID: {id})");
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

    private async Task UpdateSqliteIndexAsync(DictionaryEntry entry)
    {
        try
        {
            await _sqlConnection.OpenAsync();

            // 获取简要定义（仅取第一条内容的前100个字符）
            var briefDefinition = entry.Contents.Count > 0
                ? TruncateText(entry.Contents[0].Definition, 100)
                : string.Empty;

            using (var command = _sqlConnection.CreateCommand())
            {
                command.CommandText = @"
                        INSERT INTO DictionaryEntries 
                        (EntryID, Term, PinYin, FirstLetter, Category, DefinitionBrief, CreatedAt, UpdatedAt)
                        VALUES 
                        (@Id, @Term, @PinYin, @FirstLetter, @Category, @DefinitionBrief, @CreatedAt, @UpdatedAt)
                        ON CONFLICT(EntryID) DO UPDATE SET
                        Term = @Term,
                        PinYin = @PinYin,
                        FirstLetter = @FirstLetter,
                        Category = @Category,
                        DefinitionBrief = @DefinitionBrief,
                        UpdatedAt = @UpdatedAt
                    ";

                command.Parameters.AddWithValue("@Id", entry.Id);
                command.Parameters.AddWithValue("@Term", entry.Term);
                command.Parameters.AddWithValue("@PinYin", entry.PinYin ?? string.Empty);
                command.Parameters.AddWithValue("@FirstLetter", entry.FirstLetter ?? string.Empty);
                command.Parameters.AddWithValue("@Category", entry.Category ?? string.Empty);
                command.Parameters.AddWithValue("@DefinitionBrief", briefDefinition);
                command.Parameters.AddWithValue("@CreatedAt", entry.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("@UpdatedAt", entry.UpdatedAt.ToString("O"));

                await command.ExecuteNonQueryAsync();
            }

            _sqlConnection.Close();
            Console.WriteLine($"SQLite: 索引更新 {entry.Term}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQLite操作失败: {ex.Message}");
        }
    }

    private void SaveToJsonFile(DictionaryEntry entry)
    {
        try
        {
            // 按首字母创建子目录
            var letter = !string.IsNullOrEmpty(entry.FirstLetter) ? entry.FirstLetter : "Other";
            var categoryPath = Path.Combine(_jsonBasePath, letter);
            Directory.CreateDirectory(categoryPath);

            // 创建安全的文件名
            var safeFileName = GetSafeFileName(entry.Term);
            var filePath = Path.Combine(categoryPath, $"{safeFileName}.json");

            // 使用源生成的序列化器而不是反射
            var jsonString = System.Text.Json.JsonSerializer.Serialize(
                entry,
                DictionaryJsonContext.Default.DictionaryEntry
            );

            File.WriteAllText(filePath, jsonString, Encoding.UTF8);

            Console.WriteLine($"JSON: 保存文件 {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存JSON文件时出错: {ex.Message}");
        }
    }

    // 获取拼音
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

    // 截取文本，避免过长
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
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

    // 查询方法：根据词条名称查找
    public DictionaryEntry GetEntryByTerm(string term)
    {
        try
        {
            return _entriesCollection.FindOne(x => x.Term == term);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询词条 {term} 失败: {ex.Message}");
            return null;
        }
    }

    // 异步包装方法
    public Task<DictionaryEntry> GetEntryByTermAsync(string term)
    {
        return Task.FromResult(GetEntryByTerm(term));
    }

    // 查询方法：根据拼音首字母查找
    public List<DictionaryEntry> GetEntriesByFirstLetter(string letter)
    {
        try
        {
            return _entriesCollection.Find(x => x.FirstLetter.Equals(letter, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(x => x.PinYin)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询首字母 {letter} 的词条失败: {ex.Message}");
            return new List<DictionaryEntry>();
        }
    }

    // 异步包装方法
    public Task<List<DictionaryEntry>> GetEntriesByFirstLetterAsync(string letter)
    {
        return Task.FromResult(GetEntriesByFirstLetter(letter));
    }

    // 查询方法：搜索词条
    public List<DictionaryEntry> SearchEntries(string keyword, int limit = 100)
    {
        try
        {
            // 使用全文搜索索引
            var results = _entriesCollection
                .Find(Query.Contains("SearchText", keyword));

            // 使用LINQ的Take方法而不是Limit
            return results
                .OrderBy(x => x.Term)
                .Take(limit)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"搜索词条失败: {ex.Message}");
            return new List<DictionaryEntry>();
        }
    }


    // 异步包装方法
    public Task<List<DictionaryEntry>> SearchEntriesAsync(string keyword, int limit = 100)
    {
        return Task.FromResult(SearchEntries(keyword, limit));
    }

    // 获取词典统计信息
    public Dictionary<string, int> GetDictionaryStatistics()
    {
        var stats = new Dictionary<string, int>();

        try
        {
            // 总数
            stats["Total"] = _entriesCollection.Count();

            // 按首字母分组统计
            var letters = _entriesCollection
                .FindAll()
                .Select(x => x.FirstLetter)
                .GroupBy(x => x)
                .Select(g => new { Letter = g.Key, Count = g.Count() });

            foreach (var group in letters)
            {
                stats[group.Letter] = group.Count;
            }

            // 按分类统计
            var categories = _entriesCollection
                .FindAll()
                .Select(x => x.Category)
                .Where(x => !string.IsNullOrEmpty(x))
                .GroupBy(x => x)
                .Select(g => new { Category = g.Key, Count = g.Count() });

            foreach (var group in categories)
            {
                stats["Cat_" + group.Category] = group.Count;
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
public class EntryBrief
{
    public string Term { get; set; } = string.Empty;
    public string PinYin { get; set; } = string.Empty;
    public string FirstLetter { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefinitionBrief { get; set; } = string.Empty;
}