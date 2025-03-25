using Crawlers.Models;
using LiteDB;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System.Text;

namespace Crawlers.Repository
{
    public class ArticleRepository : IDisposable
    {
        private readonly string _liteDbPath;
        private readonly string _sqliteDbPath;
        private readonly string _jsonBasePath;
        private readonly LiteDatabase _db;
        private readonly SqliteConnection _sqliteConnection;

        public ArticleRepository(
            string liteDbPath = "TCMArticles.litedb",
            string sqliteDbPath = "TCMArticles.db",
            string jsonBasePath = "TCMArticlesData")
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
            Directory.CreateDirectory(Path.Combine(_jsonBasePath, "Articles"));
        }

        public string GetJsonBasePath()
        {
            return _jsonBasePath;
        }

        private void CreateTables()
        {
            try
            {
                using (var command = _sqliteConnection.CreateCommand())
                {
                    // 创建文章表
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Articles (
                            Id TEXT PRIMARY KEY,
                            Title TEXT NOT NULL,
                            Author TEXT,
                            PublishDate TEXT,
                            Summary TEXT,
                            Content TEXT,
                            Url TEXT,
                            CreatedTime TEXT
                        );
                    ";
                    command.ExecuteNonQuery();

                    // 创建标签表
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ArticleTags (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            ArticleId TEXT,
                            Tag TEXT,
                            FOREIGN KEY(ArticleId) REFERENCES Articles(Id)
                        );
                    ";
                    command.ExecuteNonQuery();

                    // 创建索引
                    command.CommandText = "CREATE INDEX IF NOT EXISTS idx_article_title ON Articles (Title);";
                    command.ExecuteNonQuery();

                    command.CommandText = "CREATE INDEX IF NOT EXISTS idx_article_tags ON ArticleTags (Tag);";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建表时出错: {ex.Message}");
            }
        }

        public async Task<bool> SaveArticleAsync(Article article)
        {
            if (article == null || string.IsNullOrEmpty(article.Title))
                return false;

            try
            {
                // 检查是否已存在相同标题的文章
                if (await ExistsByTitleAsync(article.Title))
                {
                    // 更新现有文章
                    return await UpdateArticleAsync(article);
                }

                // 保存到LiteDB
                var collection = _db.GetCollection<Article>("articles");
                collection.Insert(article);

                // 保存到SQLite
                using (var command = _sqliteConnection.CreateCommand())
                {
                    // 插入文章记录
                    command.CommandText = @"
                        INSERT INTO Articles (Id, Title, Author, PublishDate, Summary, Content, Url, CreatedTime)
                        VALUES (@Id, @Title, @Author, @PublishDate, @Summary, @Content, @Url, @CreatedTime);
                    ";
                    command.Parameters.AddWithValue("@Id", article.Id);
                    command.Parameters.AddWithValue("@Title", article.Title);
                    command.Parameters.AddWithValue("@Author", article.Author ?? "");
                    command.Parameters.AddWithValue("@PublishDate", article.PublishDate ?? "");
                    command.Parameters.AddWithValue("@Summary", article.Summary ?? "");
                    command.Parameters.AddWithValue("@Content", article.Content ?? "");
                    command.Parameters.AddWithValue("@Url", article.Url ?? "");
                    command.Parameters.AddWithValue("@CreatedTime", article.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.ExecuteNonQuery();

                    // 插入标签
                    if (article.Tags != null && article.Tags.Count > 0)
                    {
                        foreach (var tag in article.Tags)
                        {
                            if (string.IsNullOrEmpty(tag))
                                continue;

                            command.CommandText = @"
                                INSERT INTO ArticleTags (ArticleId, Tag)
                                VALUES (@ArticleId, @Tag);
                            ";
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("@ArticleId", article.Id);
                            command.Parameters.AddWithValue("@Tag", tag);
                            command.ExecuteNonQuery();
                        }
                    }
                }

                // 保存到JSON文件
                await SaveArticleToJsonAsync(article);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存文章时出错: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExistsByTitleAsync(string title)
        {
            try
            {
                using (var command = _sqliteConnection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM Articles WHERE Title = @Title;";
                    command.Parameters.AddWithValue("@Title", title);

                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查文章是否存在时出错: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> UpdateArticleAsync(Article article)
        {
            try
            {
                // 获取现有文章的ID
                string existingId;

                using (var command = _sqliteConnection.CreateCommand())
                {
                    command.CommandText = "SELECT Id FROM Articles WHERE Title = @Title;";
                    command.Parameters.AddWithValue("@Title", article.Title);

                    var result = await command.ExecuteScalarAsync();
                    existingId = result?.ToString();

                    if (string.IsNullOrEmpty(existingId))
                        return false;

                    // 保持原始ID
                    article.Id = existingId;

                    // 更新文章
                    command.CommandText = @"
                        UPDATE Articles
                        SET Author = @Author,
                            PublishDate = @PublishDate,
                            Summary = @Summary,
                            Content = @Content,
                            Url = @Url
                        WHERE Id = @Id;
                    ";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@Id", existingId);
                    command.Parameters.AddWithValue("@Author", article.Author ?? "");
                    command.Parameters.AddWithValue("@PublishDate", article.PublishDate ?? "");
                    command.Parameters.AddWithValue("@Summary", article.Summary ?? "");
                    command.Parameters.AddWithValue("@Content", article.Content ?? "");
                    command.Parameters.AddWithValue("@Url", article.Url ?? "");
                    command.ExecuteNonQuery();

                    // 删除现有标签
                    command.CommandText = "DELETE FROM ArticleTags WHERE ArticleId = @ArticleId;";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@ArticleId", existingId);
                    command.ExecuteNonQuery();

                    // 插入新标签
                    if (article.Tags != null && article.Tags.Count > 0)
                    {
                        foreach (var tag in article.Tags)
                        {
                            if (string.IsNullOrEmpty(tag))
                                continue;

                            command.CommandText = @"
                                INSERT INTO ArticleTags (ArticleId, Tag)
                                VALUES (@ArticleId, @Tag);
                            ";
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("@ArticleId", existingId);
                            command.Parameters.AddWithValue("@Tag", tag);
                            command.ExecuteNonQuery();
                        }
                    }
                }

                // 更新LiteDB
                var collection = _db.GetCollection<Article>("articles");
                collection.Update(article);

                // 更新JSON文件
                await SaveArticleToJsonAsync(article);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新文章时出错: {ex.Message}");
                return false;
            }
        }

        private async Task SaveArticleToJsonAsync(Article article)
        {
            try
            {
                // 文件名使用ID而不是标题，避免特殊字符问题
                string jsonFilePath = Path.Combine(_jsonBasePath, "Articles", $"{article.Id}.json");
                string json = JsonConvert.SerializeObject(article, Formatting.Indented);
                await File.WriteAllTextAsync(jsonFilePath, json, Encoding.UTF8);

                // 如果文章内容不为空，创建纯文本版本
                if (!string.IsNullOrEmpty(article.Content))
                {
                    string textFolderPath = Path.Combine(_jsonBasePath, "TextArticles");
                    Directory.CreateDirectory(textFolderPath);

                    string textFileName = SanitizeFileName(article.Title);
                    string textFilePath = Path.Combine(textFolderPath, $"{textFileName}.txt");

                    using (StreamWriter writer = new StreamWriter(textFilePath, false, Encoding.UTF8))
                    {
                        await writer.WriteLineAsync(article.Title);
                        await writer.WriteLineAsync(new string('=', article.Title.Length));

                        if (!string.IsNullOrEmpty(article.Author))
                            await writer.WriteLineAsync($"作者：{article.Author}");

                        if (!string.IsNullOrEmpty(article.PublishDate))
                            await writer.WriteLineAsync($"发布日期：{article.PublishDate}");

                        if (article.Tags != null && article.Tags.Count > 0)
                            await writer.WriteLineAsync($"标签：{string.Join(", ", article.Tags)}");

                        await writer.WriteLineAsync(new string('-', 40));

                        if (!string.IsNullOrEmpty(article.Summary))
                        {
                            await writer.WriteLineAsync("摘要：");
                            await writer.WriteLineAsync(article.Summary);
                            await writer.WriteLineAsync();
                        }

                        await writer.WriteLineAsync(article.Content);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存文章到JSON时出错: {ex.Message}");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            // 限制文件名长度
            if (fileName.Length > 100)
            {
                fileName = fileName.Substring(0, 100);
            }

            return fileName;
        }

        public async Task<List<Article>> GetArticlesByTagAsync(string tag)
        {
            try
            {
                var articles = new List<Article>();

                using (var command = _sqliteConnection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT a.* 
                        FROM Articles a
                        JOIN ArticleTags t ON a.Id = t.ArticleId
                        WHERE t.Tag = @Tag
                        ORDER BY a.PublishDate DESC;
                    ";
                    command.Parameters.AddWithValue("@Tag", tag);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var article = new Article
                            {
                                Id = reader["Id"].ToString(),
                                Title = reader["Title"].ToString(),
                                Author = reader["Author"].ToString(),
                                PublishDate = reader["PublishDate"].ToString(),
                                Summary = reader["Summary"].ToString(),
                                Content = reader["Content"].ToString(),
                                Url = reader["Url"].ToString(),
                                Tags = await GetArticleTagsAsync(reader["Id"].ToString())
                            };

                            articles.Add(article);
                        }
                    }
                }

                return articles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取标签文章时出错: {ex.Message}");
                return new List<Article>();
            }
        }

        public async Task<ArticleStatistics> GetStatisticsAsync()
        {
            var stats = new ArticleStatistics();

            try
            {
                using (var command = _sqliteConnection.CreateCommand())
                {
                    // 总文章数
                    command.CommandText = "SELECT COUNT(*) FROM Articles;";
                    stats.TotalArticles = Convert.ToInt32(await command.ExecuteScalarAsync());

                    // 有内容的文章数
                    command.CommandText = "SELECT COUNT(*) FROM Articles WHERE Content <> '';";
                    stats.ArticlesWithContent = Convert.ToInt32(await command.ExecuteScalarAsync());

                    // 有作者的文章数
                    command.CommandText = "SELECT COUNT(*) FROM Articles WHERE Author <> '';";
                    stats.ArticlesWithAuthor = Convert.ToInt32(await command.ExecuteScalarAsync());

                    // 有日期的文章数
                    command.CommandText = "SELECT COUNT(*) FROM Articles WHERE PublishDate <> '';";
                    stats.ArticlesWithDate = Convert.ToInt32(await command.ExecuteScalarAsync());

                    // 有标签的文章数
                    command.CommandText = "SELECT COUNT(DISTINCT ArticleId) FROM ArticleTags;";
                    stats.ArticlesWithTags = Convert.ToInt32(await command.ExecuteScalarAsync());

                    // 热门标签
                    command.CommandText = @"
                        SELECT Tag, COUNT(*) as TagCount
                        FROM ArticleTags
                        GROUP BY Tag
                        ORDER BY TagCount DESC
                        LIMIT 20;
                    ";

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            stats.TopTags.Add(
                                reader["Tag"].ToString(),
                                Convert.ToInt32(reader["TagCount"])
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取统计信息时出错: {ex.Message}");
            }

            return stats;
        }

        private async Task<List<string>> GetArticleTagsAsync(string articleId)
        {
            var tags = new List<string>();

            try
            {
                using (var command = _sqliteConnection.CreateCommand())
                {
                    command.CommandText = "SELECT Tag FROM ArticleTags WHERE ArticleId = @ArticleId;";
                    command.Parameters.AddWithValue("@ArticleId", articleId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tags.Add(reader["Tag"].ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取文章标签时出错: {ex.Message}");
            }

            return tags;
        }

        public void Dispose()
        {
            _db?.Dispose();
            _sqliteConnection?.Dispose();
        }
    }
}