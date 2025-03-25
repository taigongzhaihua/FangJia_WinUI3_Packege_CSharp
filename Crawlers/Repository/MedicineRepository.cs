using Crawlers.Models;
using Dapper;
using LiteDB;
using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization; // System.Text.Json命名空间
using System.Text.Unicode;

namespace Crawlers.Repository;

// 定义JSON序列化源生成器上下文
[JsonSerializable(typeof(ChineseMedicine))]
[JsonSerializable(typeof(List<ChineseMedicine>))]
[JsonSerializable(typeof(MedicineDataSource))]
[JsonSerializable(typeof(MedicineItem))]
internal partial class MedicineJsonContext : JsonSerializerContext
{
}

public class MedicineRepository : IDisposable
{
    // LiteDB配置
    private LiteDatabase _liteDb;
    private ILiteCollection<ChineseMedicine> _medicinesCollection;
    private readonly string _liteDbPath;

    // SQLite配置
    private readonly SqliteConnection _sqlConnection;
    private readonly string _sqliteDbPath;

    // JSON文件配置
    private readonly string _jsonBasePath;

    // 序列化配置
    private readonly JsonSerializerOptions _jsonOptions;

    public MedicineRepository(string liteDbPath = "ChineseMedicine.litedb",
        string sqliteDbPath = "ChineseMedicine.db",
        string jsonBasePath = "ChineseMedicineData")
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
            TypeInfoResolver = MedicineJsonContext.Default
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
            _medicinesCollection = _liteDb.GetCollection<ChineseMedicine>("medicines");

            // 创建索引
            _medicinesCollection.EnsureIndex(x => x.Name, true);  // unique
            _medicinesCollection.EnsureIndex(x => x.FirstLetter);
            _medicinesCollection.EnsureIndex(x => x.PinYin);

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
            _medicinesCollection = _liteDb.GetCollection<ChineseMedicine>("medicines");

            // 创建索引
            _medicinesCollection.EnsureIndex(x => x.Name, true);  // unique
            _medicinesCollection.EnsureIndex(x => x.FirstLetter);
            _medicinesCollection.EnsureIndex(x => x.PinYin);
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
                    CREATE TABLE IF NOT EXISTS Medicines (
                        MedicineID INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        PinYin TEXT,
                        FirstLetter TEXT,
                        Aliases TEXT,
                        Effects TEXT,
                        Indications TEXT,
                        CreatedAt TEXT,
                        UpdatedAt TEXT
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_medicine_name ON Medicines(Name);
                    CREATE INDEX IF NOT EXISTS idx_medicine_pinyin ON Medicines(PinYin);
                    CREATE INDEX IF NOT EXISTS idx_medicine_letter ON Medicines(FirstLetter);
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

    public async Task<bool> SaveMedicineAsync(ChineseMedicine medicine)
    {
        try
        {
            // 确保拼音和首字母已设置
            if (string.IsNullOrEmpty(medicine.PinYin))
            {
                medicine.PinYin = GetPinyin(medicine.Name);
                medicine.FirstLetter = !string.IsNullOrEmpty(medicine.PinYin) ?
                    medicine.PinYin[..1].ToUpper() : "?";
            }

            // 更新索引属性
            medicine.UpdateIndexProperties();

            // 设置更新时间
            medicine.UpdatedAt = DateTime.UtcNow;

            // 1. 保存到LiteDB
            var dbId = SaveToLiteDb(medicine);

            // 2. 更新SQLite索引
            await UpdateSqliteIndexAsync(medicine);

            // 3. 保存JSON文件
            SaveToJsonFile(medicine);

            return true;
        }
        catch (Exception ex)
        {
            // 记录错误，但不中断程序
            Console.WriteLine($"保存中药 {medicine.Name} 时出错: {ex.Message}");
            return false;
        }
    }

    private int SaveToLiteDb(ChineseMedicine medicine)
    {
        try
        {
            // 查找是否存在同名记录
            var existingMedicine = _medicinesCollection.FindOne(x => x.Name == medicine.Name);

            if (existingMedicine != null)
            {
                // 如果存在，更新记录，保留ID
                medicine.Id = existingMedicine.Id;
                _medicinesCollection.Update(medicine);
                Console.WriteLine($"LiteDB: 更新中药 {medicine.Name} (ID: {medicine.Id})");
                return medicine.Id;
            }
            else
            {
                // 如果不存在，插入新记录
                var id = _medicinesCollection.Insert(medicine);
                Console.WriteLine($"LiteDB: 新增中药 {medicine.Name} (ID: {id})");
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

    private async Task UpdateSqliteIndexAsync(ChineseMedicine medicine)
    {
        try
        {
            await _sqlConnection.OpenAsync();

            // 不使用Dapper，直接使用ADO.NET
            using (var command = _sqlConnection.CreateCommand())
            {
                command.CommandText = @"
                        INSERT INTO Medicines (MedicineID, Name, PinYin, FirstLetter, Aliases, Effects, Indications, CreatedAt, UpdatedAt)
                        VALUES (@Id, @Name, @PinYin, @FirstLetter, @AllAliases, @AllEffects, @AllIndications, @CreatedAt, @UpdatedAt)
                        ON CONFLICT(MedicineID) DO UPDATE SET
                        Name = @Name,
                        PinYin = @PinYin,
                        FirstLetter = @FirstLetter,
                        Aliases = @AllAliases,
                        Effects = @AllEffects,
                        Indications = @AllIndications,
                        UpdatedAt = @UpdatedAt
                    ";

                command.Parameters.AddWithValue("@Id", medicine.Id);
                command.Parameters.AddWithValue("@Name", medicine.Name);
                command.Parameters.AddWithValue("@PinYin", medicine.PinYin ?? string.Empty);
                command.Parameters.AddWithValue("@FirstLetter", medicine.FirstLetter ?? string.Empty);
                command.Parameters.AddWithValue("@AllAliases", medicine.AllAliases ?? string.Empty);
                command.Parameters.AddWithValue("@AllEffects", medicine.AllEffects ?? string.Empty);
                command.Parameters.AddWithValue("@AllIndications", medicine.AllIndications ?? string.Empty);
                command.Parameters.AddWithValue("@CreatedAt", medicine.CreatedAt.ToString("O"));
                command.Parameters.AddWithValue("@UpdatedAt", medicine.UpdatedAt.ToString("O"));

                await command.ExecuteNonQueryAsync();
            }

            _sqlConnection.Close();
            Console.WriteLine($"SQLite: 索引更新 {medicine.Name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SQLite操作失败: {ex.Message}");
            // 忽略错误，允许程序继续
        }
    }

    private void SaveToJsonFile(ChineseMedicine medicine)
    {
        try
        {
            // 按首字母创建子目录
            var letter = !string.IsNullOrEmpty(medicine.FirstLetter) ? medicine.FirstLetter : "Other";
            var categoryPath = Path.Combine(_jsonBasePath, letter);
            Directory.CreateDirectory(categoryPath);

            // 创建安全的文件名
            var safeFileName = GetSafeFileName(medicine.Name);
            var filePath = Path.Combine(categoryPath, $"{safeFileName}.json");

            // 使用源生成的序列化器而不是反射
            var jsonString = System.Text.Json.JsonSerializer.Serialize(
                medicine,
                MedicineJsonContext.Default.ChineseMedicine
            );

            File.WriteAllText(filePath, jsonString, Encoding.UTF8);

            Console.WriteLine($"JSON: 保存文件 {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存JSON文件时出错: {ex.Message}");
        }
    }


    // 根据中药名获取拼音
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

    // 查询方法：根据名称查找中药
    public ChineseMedicine GetMedicineByName(string name)
    {
        try
        {
            return _medicinesCollection.FindOne(x => x.Name == name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询中药 {name} 失败: {ex.Message}");
            return null;
        }
    }

    // 异步包装方法（为了保持API一致）
    public Task<ChineseMedicine> GetMedicineByNameAsync(string name)
    {
        return Task.FromResult(GetMedicineByName(name));
    }

    // 查询方法：根据拼音首字母查找中药
    public List<ChineseMedicine> GetMedicinesByFirstLetter(string letter)
    {
        try
        {
            return _medicinesCollection.Find(x => x.FirstLetter.Equals(letter, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"查询首字母 {letter} 的中药失败: {ex.Message}");
            return [];
        }
    }

    // 异步包装方法（为了保持API一致）
    public Task<List<ChineseMedicine>> GetMedicinesByFirstLetterAsync(string letter)
    {
        return Task.FromResult(GetMedicinesByFirstLetter(letter));
    }

    // 查询方法：根据功效关键词搜索中药
    public List<ChineseMedicine> SearchMedicinesByEffects(string keyword)
    {
        try
        {
            return _medicinesCollection.Find(x => x.AllEffects.Contains(keyword)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"按功效搜索中药失败: {ex.Message}");
            return [];
        }
    }

    // 异步包装方法（为了保持API一致）
    public Task<List<ChineseMedicine>> SearchMedicinesByEffectsAsync(string keyword)
    {
        return Task.FromResult(SearchMedicinesByEffects(keyword));
    }

    // 查询方法：从SQLite获取简要信息列表
    public async Task<IEnumerable<dynamic>> GetMedicineBriefListAsync()
    {
        try
        {
            await _sqlConnection.OpenAsync();

            var sql = @"
                    SELECT Name, PinYin, FirstLetter, Aliases, Effects
                    FROM Medicines
                    ORDER BY FirstLetter, PinYin
                ";

            var result = await _sqlConnection.QueryAsync(sql);

            _sqlConnection.Close();
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取中药简要列表失败: {ex.Message}");
            return [];
        }
    }

    // 获取所有中药材统计信息
    public Dictionary<string, int> GetMedicineStatistics()
    {
        var stats = new Dictionary<string, int>();

        try
        {
            // 总数
            stats["Total"] = _medicinesCollection.Count();

            // 按首字母分组统计
            var letters = _medicinesCollection
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

    // 销毁连接
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