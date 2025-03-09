using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TGZH.Pinyin;

/// <summary>
/// 拼音数据库
/// </summary>
/// <remarks>
/// 创建拼音数据库实例
/// </remarks>
/// <param name="dbPath">数据库路径，为空时使用默认路径</param>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal partial class PinyinDatabase(string dbPath = null) : IDisposable
{
    private readonly string _defaultDbName = Path.Combine(AppContext.BaseDirectory, "PinyinData.db");
    private SqliteConnection _connection;
    private bool _isInitialized;
    private string _dbPath = dbPath;
    // 内存缓存，提高频繁查询的性能
    private readonly ConcurrentDictionary<char, Dictionary<PinyinFormat, string[]>> _charCache = new();

    private readonly ConcurrentDictionary<string, Dictionary<PinyinFormat, string>> _wordCache = new();

    // 缓存状态和控制
    private int _maxCacheSize = 10000; // 可配置的最大缓存项数
    private bool _enableCache = true;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>
    /// 配置缓存设置
    /// </summary>
    /// <param name="enableCache">是否启用缓存</param>
    /// <param name="maxCacheSize">最大缓存项数</param>
    public void ConfigureCache(bool enableCache, int maxCacheSize = 10000)
    {
        _enableCache = enableCache;
        _maxCacheSize = maxCacheSize;

        if (!enableCache)
        {
            ClearCache();
        }
    }

    /// <summary>
    /// 清除拼音缓存
    /// </summary>
    public void ClearCache()
    {
        _charCache.Clear();
        _wordCache.Clear();
    }
    /// <summary>
    /// 初始化数据库
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            // 确保数据库文件存在
            await EnsureDatabaseFileExistsAsync();

            // 打开数据库连接
            OpenConnection();

            // 确保数据库表结构
            await EnsureSchemaCreatedAsync();

            // 检查数据库是否需要初始数据
            if (await IsDatabaseEmptyAsync())
            {
                // 导入内置的基础汉字数据
                await ImportEmbeddedBasicDataAsync();
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化拼音数据库失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 确保数据库文件存在
    /// </summary>
    private Task EnsureDatabaseFileExistsAsync()
    {
        if (string.IsNullOrEmpty(_dbPath))
        {
            // 使用默认路径
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pinyinDir = Path.Combine(appDataPath, "PinyinLibrary");

            // 确保目录存在
            if (!Directory.Exists(pinyinDir))
            {
                Directory.CreateDirectory(pinyinDir);
            }

            _dbPath = Path.Combine(pinyinDir, _defaultDbName);
        }

        // 如果文件不存在，创建一个空的数据库文件
        if (File.Exists(_dbPath)) return Task.CompletedTask;
        // SQLite会在连接时自动创建文件
        // 我们这里可以先创建一个空文件
        using (File.Create(_dbPath)) { }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 打开数据库连接
    /// </summary>
    private void OpenConnection()
    {
        try
        {
            var connectionString = $"Data Source={_dbPath}";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"打开数据库连接失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 确保数据库结构已创建
    /// </summary>
    private async Task EnsureSchemaCreatedAsync()
    {
        await using var transaction = _connection.BeginTransaction();
        try
        {
            // 字符拼音表 - 修改Character列为TEXT类型
            await using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS Characters (
                                      Character TEXT PRIMARY KEY,
                                      CodePoint INTEGER NOT NULL,
                                      WithToneMark TEXT NOT NULL,
                                      WithoutTone TEXT NOT NULL,
                                      WithToneNumber TEXT NOT NULL,
                                      FirstLetter TEXT NOT NULL,
                                      Frequency INTEGER DEFAULT 0
                                  );
                                  CREATE INDEX IF NOT EXISTS idx_character_frequency ON Characters(Frequency DESC);
                                  CREATE INDEX IF NOT EXISTS idx_character_codepoint ON Characters(CodePoint);
                                  """;
                await cmd.ExecuteNonQueryAsync();
            }

            // 词语拼音表（保持不变）
            await using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                                  CREATE TABLE IF NOT EXISTS Words (
                                      Word TEXT PRIMARY KEY,
                                      WithToneMark TEXT NOT NULL,
                                      WithoutTone TEXT NOT NULL,
                                      WithToneNumber TEXT NOT NULL,
                                      FirstLetter TEXT NOT NULL,
                                      Frequency INTEGER DEFAULT 0
                                  );
                                  CREATE INDEX IF NOT EXISTS idx_word_frequency ON Words(Frequency DESC);
                                  CREATE INDEX IF NOT EXISTS idx_word_length ON Words(LENGTH(Word) DESC);
                                  """;
                await cmd.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Debug.WriteLine($"创建数据库结构失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 检查数据库是否为空
    /// </summary>
    private async Task<bool> IsDatabaseEmptyAsync()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Characters";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 0;
    }
    /// <summary>
    /// 从嵌入资源导入基础拼音数据
    /// </summary>
    private async Task ImportEmbeddedBasicDataAsync()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            // 导入汉字拼音数据
            const string characterResourceName = "TGZH.Pinyin.Resources.BasicCharacterPinyin.txt";
            await using (var stream = assembly.GetManifestResourceStream(characterResourceName))
            {
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var content = await reader.ReadToEndAsync();
                    await ImportCharacterDataFromStringAsync(content);
                }
            }

            // 导入词语拼音数据
            const string wordResourceName = "TGZH.Pinyin.Resources.BasicWordPinyin.txt";
            await using (var stream = assembly.GetManifestResourceStream(wordResourceName))
            {
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var content = await reader.ReadToEndAsync();
                    await ImportWordDataFromStringAsync(content);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导入基础拼音数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从字符串导入汉字数据
    /// </summary>
    private async Task ImportCharacterDataFromStringAsync(string content)
    {
        await using var transaction = _connection.BeginTransaction();
        try
        {
            using (var reader = new StringReader(content))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    // 忽略注释和空行
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                        continue;

                    // 解析行数据
                    if (!ParseCharacterLine(line, out var character, out var withTone,
                            out var withoutTone, out var withNumber, out var firstLetter)) continue;

                    // 计算字符的码点（Unicode代码点）
                    int codePoint;
                    switch (character.Length)
                    {
                        case 1:
                            codePoint = character[0];
                            break;
                        case 2 when char.IsHighSurrogate(character[0]) && char.IsLowSurrogate(character[1]):
                            codePoint = char.ConvertToUtf32(character[0], character[1]);
                            break;
                        default:
                            Debug.WriteLine($"无法处理的字符: '{character}'");
                            continue;
                    }

                    await using var cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = """
                                      INSERT OR REPLACE INTO Characters 
                                      (Character, CodePoint, WithToneMark, WithoutTone, WithToneNumber, FirstLetter, Frequency) 
                                      VALUES ($char, $codePoint, $withTone, $withoutTone, $withNumber, $firstLetter, 500)
                                      """;

                    cmd.Parameters.AddWithValue("$char", character);
                    cmd.Parameters.AddWithValue("$codePoint", codePoint);
                    cmd.Parameters.AddWithValue("$withTone", withTone);
                    cmd.Parameters.AddWithValue("$withoutTone", withoutTone);
                    cmd.Parameters.AddWithValue("$withNumber", withNumber);
                    cmd.Parameters.AddWithValue("$firstLetter", firstLetter);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Debug.WriteLine($"导入汉字数据失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 解析汉字数据行
    /// </summary>
    private static bool ParseCharacterLine(string line, out string character, out string withTone,
        out string withoutTone, out string withNumber, out string firstLetter)
    {
        character = null;
        withTone = null;
        withoutTone = null;
        withNumber = null;
        firstLetter = null;

        try
        {
            // 首先移除注释部分
            var processedLine = line;
            var commentIndex = processedLine.IndexOf('#');
            var commentText = string.Empty;
            if (commentIndex > 0)
            {
                commentText = line[(commentIndex + 1)..].Trim();
                processedLine = processedLine[..commentIndex].Trim();
            }

            // 支持多种格式
            string[] parts;
            if (processedLine.Contains('='))
            {
                parts = processedLine.Split('=', 2);
            }
            else if (processedLine.Contains(':'))
            {
                parts = processedLine.Split(':', 2);
            }
            else
            {
                return false;
            }

            if (parts.Length != 2)
                return false;

            var charPart = parts[0].Trim();
            var pinyinPart = parts[1].Trim();

            // 处理Unicode编码格式 (U+XXXX)
            if (charPart.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
            {
                var unicodeStr = charPart[2..];

                // 从注释中提取字符，如果存在的话
                string commentChar = null;
                if (!string.IsNullOrEmpty(commentText) && commentText.Length > 0)
                {
                    // 获取第一个字符（可能是代理对）
                    var charLen = char.IsHighSurrogate(commentText[0]) && commentText.Length > 1 ? 2 : 1;
                    commentChar = commentText[..charLen];
                }

                // 解析十六进制数字
                if (!int.TryParse(unicodeStr, System.Globalization.NumberStyles.HexNumber,
                        null, out var unicode))
                {
                    Debug.WriteLine($"警告: 无法解析Unicode值 {charPart}");
                    return false;
                }

                // 检查值是否在有效Unicode范围内
                if (unicode > 0x10FFFF)
                {
                    Debug.WriteLine($"警告: Unicode值 {charPart} 超出有效范围 (最大 U+10FFFF)");

                    // 如果有注释字符，使用它
                    if (!string.IsNullOrEmpty(commentChar))
                    {
                        character = commentChar;
                        Debug.WriteLine($"使用注释中的字符: '{commentChar}'");
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // 正确处理所有Unicode字符，包括扩展区字符
                    try
                    {
                        character = char.ConvertFromUtf32(unicode);

                        // 验证解析的字符与注释中的字符是否匹配
                        if (!string.IsNullOrEmpty(commentChar) && character != commentChar)
                        {
                            Debug.WriteLine($"警告: Unicode {charPart} 解析为字符 '{character}', 但注释中显示为 '{commentChar}'");
                            // 可以选择使用注释中的字符
                            // character = commentChar;
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        Debug.WriteLine($"警告: Unicode {charPart} 不是有效的代码点");

                        // 如果有注释字符，使用它
                        if (!string.IsNullOrEmpty(commentChar))
                        {
                            character = commentChar;
                            Debug.WriteLine($"使用注释中的字符: '{commentChar}'");
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            // 直接字符格式
            else if (charPart.Length >= 1)
            {
                // 获取第一个字符（可能是代理对）
                var charLen = char.IsHighSurrogate(charPart[0]) && charPart.Length > 1 ? 2 : 1;
                character = charPart[..charLen];
            }
            else
            {
                return false;
            }

            // 拼音可能有多个，用逗号分隔
            var pinyins = pinyinPart.Split(',');
            if (pinyins.Length == 0 || string.IsNullOrWhiteSpace(pinyins[0]))
                return false;

            // 使用所有拼音，用逗号连接
            withTone = string.Join(",", pinyins.Select(p => p.Trim()));

            // 转换为不带声调和带数字声调的格式
            withoutTone = string.Join(",", pinyins.Select(p => PinyinConverter.RemoveToneMarks(p.Trim())));
            withNumber = string.Join(",", pinyins.Select(p => PinyinConverter.ConvertToToneNumber(p.Trim())));

            // 获取首字母，使用第一个拼音的首字母
            firstLetter = PinyinConverter.RemoveToneMarks(pinyins[0].Trim())[0].ToString();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"解析拼音行失败: {line}, 错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取汉字的拼音
    /// </summary>
    public async Task<string[]> GetCharPinyinAsync(char c, PinyinFormat format)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("数据库未初始化。请先调用 InitializeAsync 方法。");

        try
        {
            var columnName = format switch
            {
                PinyinFormat.WithoutTone => "WithoutTone",
                PinyinFormat.WithToneNumber => "WithToneNumber",
                PinyinFormat.FirstLetter => "FirstLetter",
                _ => "WithToneMark",
            };
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"SELECT {columnName} FROM Characters WHERE Character = $char";
            cmd.Parameters.AddWithValue("$char", (int)c);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return [c.ToString()];
            var pinyinStr = result.ToString();
            return pinyinStr?.Split(',');

            // 如果找不到，返回原字符
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"查询汉字拼音失败: {ex.Message}");
            return [c.ToString()];
        }
    }

    /// <summary>
    /// 批量获取多个汉字的拼音，使用异步迭代器实现，性能优化版本
    /// </summary>
    /// <param name="chars">要查询的汉字数组</param>
    /// <param name="format">拼音格式</param>
    /// <returns>拼音结果的异步枚举，键为汉字，值为拼音数组</returns>
    public async IAsyncEnumerable<KeyValuePair<char, string[]>> GetCharsPinyinBatchAsyncEnumerable(
        char[] chars, PinyinFormat format)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("数据库未初始化。请先调用 InitializeAsync 方法。");

        if (chars == null || chars.Length == 0)
            yield break;

        // 分离需要查询的字符和可以从缓存获取的字符
        var charsToQuery = new List<char>();

        // 首先从缓存获取并返回结果
        foreach (var c in chars)
        {
            if (_enableCache && TryGetFromCache(c, format, out var cachedPinyin))
            {
                yield return new KeyValuePair<char, string[]>(c, cachedPinyin);
            }
            else
            {
                charsToQuery.Add(c);
            }
        }

        // 如果所有字符都在缓存中找到，直接返回
        if (charsToQuery.Count == 0)
            yield break;

        // 构建参数化查询，避免多次数据库往返
        var columnName = format switch
        {
            PinyinFormat.WithoutTone => "WithoutTone",
            PinyinFormat.WithToneNumber => "WithToneNumber",
            PinyinFormat.FirstLetter => "FirstLetter",
            _ => "WithToneMark",
        };

        // 分批处理，每次最多500个参数
        const int batchSize = 500;
        for (var i = 0; i < charsToQuery.Count; i += batchSize)
        {
            var batch = charsToQuery.Skip(i).Take(batchSize).ToList();
            var processedChars = new HashSet<char>();
            var sqlBuilder = new StringBuilder();
            var parameters = new Dictionary<string, object>();

            sqlBuilder.Append($"SELECT Character, {columnName} FROM Characters WHERE Character IN (");

            for (var j = 0; j < batch.Count; j++)
            {
                var paramName = $"$p{j}";
                sqlBuilder.Append(j > 0 ? ", " : "");
                sqlBuilder.Append(paramName);
                parameters[paramName] = batch[j].ToString();
            }

            sqlBuilder.Append(')');

            // 执行批量查询
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = sqlBuilder.ToString();

            // 添加参数
            foreach (var param in parameters)
            {
                cmd.Parameters.AddWithValue(param.Key, param.Value);
            }

            // 执行查询并处理结果
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var charStr = reader.GetString(0);
                var pinyin = reader.GetString(1).Split(',');

                if (charStr.Length != 1) continue;
                var c = charStr[0];
                processedChars.Add(c);

                // 添加到缓存
                if (_enableCache)
                {
                    AddToCache(c, format, pinyin);
                }

                yield return new KeyValuePair<char, string[]>(c, pinyin);
            }

            // 处理未找到拼音的字符
            foreach (var c in batch.Where(c => !processedChars.Contains(c)))
            {
                var defaultPinyin = new[] { c.ToString() };

                // 也缓存未找到的结果
                if (_enableCache)
                {
                    AddToCache(c, format, defaultPinyin);
                }

                yield return new KeyValuePair<char, string[]>(c, defaultPinyin);
            }
        }
    }

    /// <summary>
    /// 批量获取多个汉字的拼音，性能优化版本
    /// </summary>
    /// <param name="chars">要查询的汉字数组</param>
    /// <param name="format">拼音格式</param>
    /// <returns>拼音结果字典，键为汉字，值为拼音数组</returns>
    public async Task<Dictionary<char, string[]>> GetCharsPinyinBatchAsync(
        char[] chars, PinyinFormat format)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("数据库未初始化。请先调用 InitializeAsync 方法。");

        if (chars == null || chars.Length == 0)
            return new Dictionary<char, string[]>();

        // 使用异步迭代器收集结果
        var result = new Dictionary<char, string[]>();

        await foreach (var pair in GetCharsPinyinBatchAsyncEnumerable(chars, format))
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    /// <summary>
    /// 批量获取文本中全部汉字的拼音
    /// </summary>
    /// <param name="text">要查询的文本</param>
    /// <param name="format">拼音格式</param>
    /// <returns>每个字的拼音数组</returns>
    public async Task<string[][]> GetTextCharPinyinBatchAsync(string text, PinyinFormat format)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        // 提取文本中唯一的字符
        var uniqueChars = text.Distinct().ToArray();

        // 批量获取拼音
        var pinyinDict = await GetCharsPinyinBatchAsync(uniqueChars, format);

        // 按原文顺序构建结果
        var result = new string[text.Length][];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (pinyinDict.TryGetValue(c, out var pinyin))
            {
                result[i] = pinyin;
            }
            else
            {
                // 不应该发生，但以防万一
                result[i] = [c.ToString()];
            }
        }

        return result;
    }

    /// <summary>
    /// 获取词语的拼音
    /// </summary>
    public async Task<string> GetWordPinyinAsync(string word, PinyinFormat format)
    {
        if (string.IsNullOrEmpty(word) || word.Length <= 1)
            return null;

        if (!_isInitialized)
            throw new InvalidOperationException("数据库未初始化。请先调用 InitializeAsync 方法。");

        try
        {
            var columnName = format switch
            {
                PinyinFormat.WithoutTone => "WithoutTone",
                PinyinFormat.WithToneNumber => "WithToneNumber",
                PinyinFormat.FirstLetter => "FirstLetter",
                _ => "WithToneMark",
            };
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"SELECT {columnName} FROM Words WHERE Word = $word";
            cmd.Parameters.AddWithValue("$word", word);

            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                return result.ToString();
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"查询词语拼音失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 批量获取多个词语的拼音
    /// </summary>
    /// <param name="words">要查询的词语数组</param>
    /// <param name="format">拼音格式</param>
    /// <returns>拼音结果字典，键为词语，值为拼音</returns>
    public async Task<Dictionary<string, string>> GetWordsPinyinBatchAsync(
        string[] words, PinyinFormat format)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("数据库未初始化。请先调用 InitializeAsync 方法。");

        if (words == null || words.Length == 0)
            return new Dictionary<string, string>();

        // 结果字典
        var result = new Dictionary<string, string>();

        // 分离需要查询的词语和可以从缓存获取的词语
        var wordsToQuery = new List<string>();

        // 从缓存获取已有结果
        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word) || word.Length <= 1)
                continue;

            if (_enableCache && TryGetWordFromCache(word, format, out var cachedPinyin))
            {
                result[word] = cachedPinyin;
            }
            else
            {
                wordsToQuery.Add(word);
            }
        }

        // 如果所有词语都在缓存中找到，直接返回
        if (wordsToQuery.Count == 0)
            return result;

        try
        {
            // 构建参数化查询，避免多次数据库往返
            var columnName = format switch
            {
                PinyinFormat.WithoutTone => "WithoutTone",
                PinyinFormat.WithToneNumber => "WithToneNumber",
                PinyinFormat.FirstLetter => "FirstLetter",
                _ => "WithToneMark",
            };

            // 分批处理，每次最多500个参数
            const int batchSize = 500;
            for (var i = 0; i < wordsToQuery.Count; i += batchSize)
            {
                var batch = wordsToQuery.Skip(i).Take(batchSize).ToList();
                var parameters = new Dictionary<string, object>();

                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append($"SELECT Word, {columnName} FROM Words WHERE Word IN (");

                for (var j = 0; j < batch.Count; j++)
                {
                    var paramName = $"$p{j}";
                    sqlBuilder.Append(j > 0 ? ", " : "");
                    sqlBuilder.Append(paramName);
                    parameters[paramName] = batch[j];
                }

                sqlBuilder.Append(')');

                // 执行批量查询
                await using var cmd = _connection.CreateCommand();
                cmd.CommandText = sqlBuilder.ToString();

                // 添加参数
                foreach (var param in parameters)
                {
                    cmd.Parameters.AddWithValue(param.Key, param.Value);
                }

                // 执行查询并处理结果
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var word = reader.GetString(0);
                    var pinyin = reader.GetString(1);

                    result[word] = pinyin;

                    // 添加到缓存
                    if (_enableCache)
                    {
                        AddWordToCache(word, format, pinyin);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"批量查询词语拼音失败: {ex.Message}");
        }

        return result;
    }
    /// <summary>
    /// 添加或更新汉字拼音
    /// </summary>
    public async Task AddOrUpdateCharacterAsync(char c, string withTone, string withoutTone = null,
        string withNumber = null, string firstLetter = null, int frequency = 500)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("数据库未初始化");

        // 如果只提供了带音调的拼音，自动计算其他格式
        withoutTone ??= PinyinConverter.RemoveToneMarks(withTone);

        withNumber ??= PinyinConverter.ConvertToToneNumber(withTone);

        firstLetter ??= withoutTone[..1];

        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                              
                                                      INSERT OR REPLACE INTO Characters 
                                                      (Character, WithToneMark, WithoutTone, WithToneNumber, FirstLetter, Frequency) 
                                                      VALUES ($char, $withTone, $withoutTone, $withNumber, $firstLetter, $frequency)
                              """;

            cmd.Parameters.AddWithValue("$char", (int)c);
            cmd.Parameters.AddWithValue("$withTone", withTone);
            cmd.Parameters.AddWithValue("$withoutTone", withoutTone);
            cmd.Parameters.AddWithValue("$withNumber", withNumber);
            cmd.Parameters.AddWithValue("$firstLetter", firstLetter);
            cmd.Parameters.AddWithValue("$frequency", frequency);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"添加或更新汉字拼音失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 添加或更新词语拼音
    /// </summary>
    public async Task AddOrUpdateWordAsync(string word, string withTone, string withoutTone = null,
        string withNumber = null, string firstLetter = null, int frequency = 500)
    {
        if (string.IsNullOrEmpty(word) || word.Length <= 1)
            throw new ArgumentException("词语长度必须大于1", nameof(word));

        if (!_isInitialized)
            throw new InvalidOperationException("数据库未初始化");

        // 如果只提供了带音调的拼音，自动计算其他格式
        withoutTone ??= PinyinConverter.RemoveToneMarks(withTone);

        withNumber ??= PinyinConverter.ConvertToToneNumber(withTone);

        if (firstLetter == null)
        {
            // 计算首字母，假设拼音之间用空格分隔
            var sb = new StringBuilder();
            foreach (var part in withoutTone.Split(' '))
            {
                if (!string.IsNullOrEmpty(part))
                    sb.Append(part[0]);
                sb.Append(' ');
            }
            firstLetter = sb.ToString().Trim();
        }

        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                              
                                                      INSERT OR REPLACE INTO Words 
                                                      (Word, WithToneMark, WithoutTone, WithToneNumber, FirstLetter, Frequency) 
                                                      VALUES ($word, $withTone, $withoutTone, $withNumber, $firstLetter, $frequency)
                              """;

            cmd.Parameters.AddWithValue("$word", word);
            cmd.Parameters.AddWithValue("$withTone", withTone);
            cmd.Parameters.AddWithValue("$withoutTone", withoutTone);
            cmd.Parameters.AddWithValue("$withNumber", withNumber);
            cmd.Parameters.AddWithValue("$firstLetter", firstLetter);
            cmd.Parameters.AddWithValue("$frequency", frequency);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"添加或更新词语拼音失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 批量导入拼音数据
    /// </summary>
    public async Task ImportPinyinDataAsync(string filePath, bool isWordData = false)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("文件不存在", filePath);

        try
        {
            var content = await File.ReadAllTextAsync(filePath);

            if (isWordData)
            {
                await ImportWordDataFromStringAsync(content);
            }
            else
            {
                await ImportCharacterDataFromStringAsync(content);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导入拼音数据失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 从字符串导入词语数据
    /// </summary>
    private async Task ImportWordDataFromStringAsync(string content)
    {
        await using var transaction = _connection.BeginTransaction();
        try
        {
            using (var reader = new StringReader(content))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    // 忽略注释和空行
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(value: '#'))
                        continue;

                    // 解析行数据
                    if (!ParseWordLine(line, out var word, out var withTone,
                            out var withoutTone, out var withNumber, out var firstLetter)) continue;
                    await using var cmd = _connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = """
                                      
                                                                              INSERT OR REPLACE INTO Words 
                                                                              (Word, WithToneMark, WithoutTone, WithToneNumber, FirstLetter, Frequency) 
                                                                              VALUES ($word, $withTone, $withoutTone, $withNumber, $firstLetter, 500)
                                      """;

                    cmd.Parameters.AddWithValue("$word", word);
                    cmd.Parameters.AddWithValue("$withTone", withTone);
                    cmd.Parameters.AddWithValue("$withoutTone", withoutTone);
                    cmd.Parameters.AddWithValue("$withNumber", withNumber);
                    cmd.Parameters.AddWithValue("$firstLetter", firstLetter);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Debug.WriteLine($"导入词语数据失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 解析词语数据行
    /// </summary>
    private bool ParseWordLine(string line, out string word, out string withTone,
        out string withoutTone, out string withNumber, out string firstLetter)
    {
        word = null;
        withTone = null;
        withoutTone = null;
        withNumber = null;
        firstLetter = null;

        try
        {
            // 首先处理行尾注释
            var commentIndex = line.IndexOf('#');
            if (commentIndex > 0)
            {
                line = line[..commentIndex].Trim();
            }

            // 支持格式: 词语=拼音 或 词语:拼音
            string[] parts;
            if (line.Contains('='))
            {
                parts = line.Split('=', 2);
            }
            else if (line.Contains(':'))
            {
                parts = line.Split(':', 2);
            }
            else
            {
                return false;
            }

            if (parts.Length != 2)
                return false;

            word = parts[0].Trim();
            var pinyinPart = parts[1].Trim();

            // 词语必须大于1个字符
            if (string.IsNullOrEmpty(word) || word.Length <= 1)
                return false;

            withTone = pinyinPart;

            // 转换为不带声调和带数字声调的格式
            withoutTone = PinyinConverter.RemoveToneMarks(withTone);
            withNumber = PinyinConverter.ConvertToToneNumber(withTone);

            // 计算首字母
            var sb = new StringBuilder();
            foreach (var part in withoutTone.Split(' '))
            {
                if (!string.IsNullOrEmpty(part))
                    sb.Append(part[0]);
                sb.Append(' ');
            }
            firstLetter = sb.ToString().Trim();

            return true;
        }
        catch
        {
            return false;
        }
    }

    #region 缓存管理

    private bool TryGetFromCache(char c, PinyinFormat format, out string[] pinyin)
    {
        pinyin = null;

        if (_charCache.TryGetValue(c, out var formatDict))
        {
            return formatDict.TryGetValue(format, out pinyin);
        }

        return false;
    }

    private void AddToCache(char c, PinyinFormat format, string[] pinyin)
    {
        // 简单的LRU缓存管理 - 如果缓存满了，不再添加新项
        if (_charCache.Count >= _maxCacheSize)
            return;

        var formatDict = _charCache.GetOrAdd(c, _ => new Dictionary<PinyinFormat, string[]>());
        formatDict[format] = pinyin;
    }

    private bool TryGetWordFromCache(string word, PinyinFormat format, out string pinyin)
    {
        pinyin = null;

        return _wordCache.TryGetValue(word, out var formatDict) && formatDict.TryGetValue(format, out pinyin);
    }

    private void AddWordToCache(string word, PinyinFormat format, string pinyin)
    {
        // 简单的LRU缓存管理
        if (_wordCache.Count >= _maxCacheSize)
            return;

        var formatDict = _wordCache.GetOrAdd(word, _ => new Dictionary<PinyinFormat, string>());
        formatDict[format] = pinyin;
    }

    #endregion

    /// <summary>
    /// 预热缓存，提前加载常用字符到内存
    /// </summary>
    /// <param name="topFrequency">频率最高的N个字符</param>
    public async Task WarmupCacheAsync(int topFrequency = 3000)
    {
        if (!_enableCache || !_isInitialized)
            return;

        try
        {
            // 获取最常用的字符
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = $@"
                    SELECT Character, WithToneMark, WithoutTone, WithToneNumber, FirstLetter
                    FROM Characters 
                    ORDER BY Frequency DESC 
                    LIMIT {topFrequency}";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var charStr = reader.GetString(0);
                if (charStr.Length != 1) continue;

                var c = charStr[0];
                var withTone = reader.GetString(1).Split(',');
                var withoutTone = reader.GetString(2).Split(',');
                var withNumber = reader.GetString(3).Split(',');
                var firstLetter = reader.GetString(4).Split(',');

                // 添加到各种格式的缓存
                AddToCache(c, PinyinFormat.WithToneMark, withTone);
                AddToCache(c, PinyinFormat.WithoutTone, withoutTone);
                AddToCache(c, PinyinFormat.WithToneNumber, withNumber);
                AddToCache(c, PinyinFormat.FirstLetter, firstLetter);
            }

            Debug.WriteLine($"拼音缓存预热完成，已加载 {topFrequency} 个常用汉字");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"拼音缓存预热失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
        _isInitialized = false;
    }
}