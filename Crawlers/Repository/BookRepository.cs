using Crawlers.Models;
using LiteDB;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.Text;

namespace Crawlers.Repository;

public class BookRepository : IDisposable
{
    private readonly string _liteDbPath;
    private readonly string _sqliteDbPath;
    private readonly string _jsonBasePath;
    private readonly LiteDatabase _db;
    private readonly SqliteConnection _sqliteConnection;

    public BookRepository(
        string liteDbPath = "TCMBooks.litedb",
        string sqliteDbPath = "TCMBooks.db",
        string jsonBasePath = "TCMBooksData")
    {
        _liteDbPath = liteDbPath;
        _sqliteDbPath = sqliteDbPath;
        _jsonBasePath = jsonBasePath;

        // 初始化 LiteDB
        _db = new LiteDatabase(_liteDbPath);

        // 初始化 SQLite
        _sqliteConnection = new SqliteConnection($"Data Source={_sqliteDbPath}");
        _sqliteConnection.Open();

        // 创建必要的表
        CreateTables();

        // 确保JSON目录存在
        Directory.CreateDirectory(_jsonBasePath);
        Directory.CreateDirectory(Path.Combine(_jsonBasePath, "Books"));
    }

    private void CreateTables()
    {
        try
        {
            using (var command = _sqliteConnection.CreateCommand())
            {
                // 创建书籍表
                command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Books (
                            Id TEXT PRIMARY KEY,
                            Title TEXT NOT NULL,
                            Author TEXT,
                            Dynasty TEXT,
                            Year TEXT,
                            Category TEXT,
                            Url TEXT,
                            ChapterCount INTEGER,
                            CreatedTime TEXT
                        );
                    ";
                command.ExecuteNonQuery();

                // 创建章节表
                command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Chapters (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            BookId TEXT,
                            Title TEXT,
                            ChapterIndex INTEGER,
                            SectionCount INTEGER,
                            ContentLength INTEGER,
                            FOREIGN KEY(BookId) REFERENCES Books(Id)
                        );
                    ";
                command.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"创建表时出错: {ex.Message}");
        }
    }

    public async Task<bool> SaveBookAsync(Book book)
    {
        try
        {
            // 保存到LiteDB
            var collection = _db.GetCollection<Book>("books");
            collection.Insert(book);

            // 保存到SQLite
            using (var command = _sqliteConnection.CreateCommand())
            {
                // 插入书籍记录
                command.CommandText = @"
                        INSERT OR REPLACE INTO Books (Id, Title, Author, Dynasty, Year, Category, Url, ChapterCount, CreatedTime)
                        VALUES (@Id, @Title, @Author, @Dynasty, @Year, @Category, @Url, @ChapterCount, @CreatedTime);
                    ";
                command.Parameters.AddWithValue("@Id", book.Id);
                command.Parameters.AddWithValue("@Title", book.Title);
                command.Parameters.AddWithValue("@Author", book.Author ?? "未知");
                command.Parameters.AddWithValue("@Dynasty", book.Dynasty ?? "未知");
                command.Parameters.AddWithValue("@Year", book.Year ?? "未知");
                command.Parameters.AddWithValue("@Category", book.Category);
                command.Parameters.AddWithValue("@Url", book.Url);
                command.Parameters.AddWithValue("@ChapterCount", book.Chapters.Count);
                command.Parameters.AddWithValue("@CreatedTime", book.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();

                // 插入章节记录
                for (var i = 0; i < book.Chapters.Count; i++)
                {
                    var chapter = book.Chapters[i];

                    command.CommandText = @"
                            INSERT INTO Chapters (BookId, Title, ChapterIndex, SectionCount, ContentLength)
                            VALUES (@BookId, @Title, @ChapterIndex, @SectionCount, @ContentLength);
                        ";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@BookId", book.Id);
                    command.Parameters.AddWithValue("@Title", chapter.Title);
                    command.Parameters.AddWithValue("@ChapterIndex", i);
                    command.Parameters.AddWithValue("@SectionCount", chapter.Sections.Count);
                    command.Parameters.AddWithValue("@ContentLength", chapter.Content.Length);
                    command.ExecuteNonQuery();
                }
            }

            // 保存到JSON文件
            var jsonFilePath = Path.Combine(_jsonBasePath, "Books", $"{SanitizeFileName(book.Title)}.json");
            var json = JsonConvert.SerializeObject(book, Formatting.Indented);
            await File.WriteAllTextAsync(jsonFilePath, json, Encoding.UTF8);

            // 创建纯文本版本
            await SaveBookAsTextAsync(book);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存书籍时出错: {ex.Message}");
            return false;
        }
    }

    private async Task SaveBookAsTextAsync(Book book)
    {
        try
        {
            // 创建纯文本文件夹
            var textFolderPath = Path.Combine(_jsonBasePath, "TextBooks");
            Directory.CreateDirectory(textFolderPath);

            var textFilePath = Path.Combine(textFolderPath, $"{SanitizeFileName(book.Title)}.txt");

            using (var writer = new StreamWriter(textFilePath, false, Encoding.UTF8))
            {
                // 写入书籍信息
                await writer.WriteLineAsync($"《{book.Title}》");
                await writer.WriteLineAsync(new string('=', 40));

                if (!string.IsNullOrEmpty(book.Author))
                    await writer.WriteLineAsync($"作者: {book.Author}");

                if (!string.IsNullOrEmpty(book.Dynasty))
                    await writer.WriteLineAsync($"朝代: {book.Dynasty}");

                if (!string.IsNullOrEmpty(book.Year))
                    await writer.WriteLineAsync($"年份: {book.Year}");

                await writer.WriteLineAsync($"分类: {book.Category}");
                await writer.WriteLineAsync(new string('=', 40));
                await writer.WriteLineAsync();

                // 写入章节内容
                foreach (var chapter in book.Chapters)
                {
                    await writer.WriteLineAsync($"## {chapter.Title}");
                    await writer.WriteLineAsync();

                    if (!string.IsNullOrEmpty(chapter.Content))
                    {
                        await writer.WriteLineAsync(chapter.Content);
                        await writer.WriteLineAsync();
                    }

                    foreach (var section in chapter.Sections)
                    {
                        await writer.WriteLineAsync($"### {section.Title}");
                        await writer.WriteLineAsync();
                        await writer.WriteLineAsync(section.Content);
                        await writer.WriteLineAsync();
                    }

                    await writer.WriteLineAsync(new string('-', 30));
                    await writer.WriteLineAsync();
                }
            }

            Console.WriteLine($"已创建纯文本版本：{textFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存文本版本时出错: {ex.Message}");
        }
    }

    private string SanitizeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }

    public async Task<List<BookInfo>> GetAllBooksAsync()
    {
        try
        {
            var books = new List<BookInfo>();

            using (var command = _sqliteConnection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Title, Author, Dynasty, Year, Category, Url FROM Books ORDER BY Title;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var bookInfo = new BookInfo
                        {
                            Title = reader["Title"].ToString(),
                            Author = reader["Author"].ToString(),
                            Dynasty = reader["Dynasty"].ToString(),
                            Year = reader["Year"].ToString(),
                            Category = reader["Category"].ToString(),
                            Url = reader["Url"].ToString()
                        };

                        books.Add(bookInfo);
                    }
                }
            }

            return books;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取书籍列表时出错: {ex.Message}");
            return new List<BookInfo>();
        }
    }

    public async Task<List<BookInfo>> GetBooksByCategoryAsync(string category)
    {
        try
        {
            var books = new List<BookInfo>();

            using (var command = _sqliteConnection.CreateCommand())
            {
                command.CommandText = "SELECT Id, Title, Author, Dynasty, Year, Category, Url FROM Books WHERE Category = @Category ORDER BY Title;";
                command.Parameters.AddWithValue("@Category", category);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var bookInfo = new BookInfo
                        {
                            Title = reader["Title"].ToString(),
                            Author = reader["Author"].ToString(),
                            Dynasty = reader["Dynasty"].ToString(),
                            Year = reader["Year"].ToString(),
                            Category = reader["Category"].ToString(),
                            Url = reader["Url"].ToString()
                        };

                        books.Add(bookInfo);
                    }
                }
            }

            return books;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"按分类获取书籍时出错: {ex.Message}");
            return new List<BookInfo>();
        }
    }

    public async Task<Book> GetBookByTitleAsync(string title)
    {
        try
        {
            var jsonFilePath = Path.Combine(_jsonBasePath, "Books", $"{SanitizeFileName(title)}.json");

            if (File.Exists(jsonFilePath))
            {
                var json = await File.ReadAllTextAsync(jsonFilePath);
                return JsonConvert.DeserializeObject<Book>(json);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取书籍时出错: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _db?.Dispose();
        _sqliteConnection?.Dispose();
    }
}