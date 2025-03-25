using Crawlers.Models;
using Crawlers.Repository;
using HtmlAgilityPack;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Crawlers
{
    public partial class BookScraper : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://www.zysj.com.cn";
        private readonly Random _random = new();
        private readonly BookRepository _repository;
        private readonly string _mainPageUrl = "https://www.zysj.com.cn/lilunshuji/index.html";

        // 需要排除的书籍关键词
        private readonly List<string> _excludeKeywords = new()
        {
            "中医词典",
            "中医名词词典",
            "词典"
        };

        // 分类标签映射，用于标记书籍分类
        private readonly Dictionary<string, string> _tagCategories = new()
        {
            { "1", "经论" },
            { "2", "伤寒、金匮" },
            { "3", "诊治" },
            { "4", "本草" },
            { "5", "方言" },
            { "6", "内科" },
            { "7", "妇科" },
            { "8", "儿科" },
            { "9", "外科" },
            { "10", "五官" },
            { "11", "针灸" },
            { "12", "医论" },
            { "13", "医案" },
            { "14", "综合" },
            { "15", "养生" },
            { "16", "其他" }
        };

        public BookScraper()
        {
            // 配置HttpClient
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.zysj.com.cn/");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // 初始化书籍仓库
            _repository = new BookRepository();
        }

        public async Task StartScrapingAsync()
        {
            Console.WriteLine("中医古籍书籍爬虫程序启动...");

            try
            {
                // 首先获取所有的书籍链接
                Console.WriteLine("开始获取书籍列表...");
                var bookLinks = await GetAllBookLinksAsync();

                if (bookLinks.Count > 0)
                {
                    Console.WriteLine($"共找到 {bookLinks.Count} 本书籍");

                    // 按类别获取分类信息
                    await GetCategoriesForBooksAsync(bookLinks);

                    // 爬取每本书籍
                    int currentBook = 0;
                    foreach (var bookInfo in bookLinks)
                    {
                        currentBook++;

                        try
                        {
                            Console.WriteLine($"\n处理书籍 [{currentBook}/{bookLinks.Count}]: {bookInfo.Title}");

                            // 构建全本链接
                            string fullBookUrl = ConvertToFullBookUrl(bookInfo.Url);
                            Console.WriteLine($"全本URL: {fullBookUrl}");

                            // 爬取书籍内容
                            await ScrapeBookAsync(fullBookUrl, bookInfo);

                            // 随机等待3-7秒，避免请求过于频繁
                            int delay = _random.Next(3000, 7000);
                            Console.WriteLine($"等待{delay / 1000.0:F1}秒后继续...");
                            await Task.Delay(delay);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"爬取书籍 {bookInfo.Title} 时出错: {ex.Message}");
                            continue; // 继续爬取下一本书
                        }
                    }

                    Console.WriteLine("\n所有书籍爬取完成！");
                }
                else
                {
                    Console.WriteLine("未找到任何书籍链接");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"爬取过程中出错: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private string ConvertToFullBookUrl(string bookUrl)
        {
            // 将书籍URL转换为全本URL
            // 例如: /lilunshuji/yideji/index.html -> /lilunshuji/yideji/quanben.html
            return MyRegex().Replace(bookUrl, "/quanben.html");
        }

        private async Task<List<BookInfo>> GetAllBookLinksAsync()
        {
            List<BookInfo> bookLinks = new List<BookInfo>();
            Dictionary<string, int> pageBookCounts = new Dictionary<string, int>(); // 用于记录每个分类下的书籍数量

            try
            {
                // 首先获取主页面
                Console.WriteLine($"访问主页: {_mainPageUrl}");
                var pageHtml = await RequestWithRetryAsync(_mainPageUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(pageHtml);

                // 查找list-content容器
                var listContentNode = doc.DocumentNode.SelectSingleNode("//div[@id='list-content']");

                if (listContentNode != null)
                {
                    // 找到书籍链接列表
                    var linkNodes = listContentNode.SelectNodes(".//ul/li/a");

                    if (linkNodes is { Count: > 0 })
                    {
                        Console.WriteLine($"找到{linkNodes.Count}个书籍链接");

                        foreach (var linkNode in linkNodes)
                        {
                            // 提取书籍标题和链接
                            string title = linkNode.GetAttributeValue("title", "").Trim();
                            if (string.IsNullOrEmpty(title))
                            {
                                title = linkNode.InnerText.Trim();
                            }

                            // 移除书名中的《》
                            title = title.Replace("《", "").Replace("》", "").Trim();

                            string link = linkNode.GetAttributeValue("href", "");

                            // 排除词典
                            bool shouldExclude = false;
                            foreach (var keyword in _excludeKeywords)
                            {
                                if (title.Contains(keyword))
                                {
                                    shouldExclude = true;
                                    break;
                                }
                            }

                            if (!shouldExclude && !string.IsNullOrEmpty(link))
                            {
                                // 确保链接是完整URL
                                if (!link.StartsWith("http"))
                                {
                                    link = _baseUrl + (link.StartsWith("/") ? "" : "/") + link;
                                }

                                var bookInfo = new BookInfo
                                {
                                    Title = title,
                                    Url = link,
                                    Category = "未分类" // 先设为未分类，后续会更新
                                };

                                bookLinks.Add(bookInfo);
                                Console.WriteLine($"  - {title} [{link}]");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("未找到书籍链接列表，尝试分析页面结构...");
                        AnalyzePageStructure(doc);
                    }
                }
                else
                {
                    // 尝试查找其他可能的结构
                    Console.WriteLine("未找到list-content容器，尝试其他选择器...");
                    var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/lilunshuji/') and not(contains(@href, 'index___'))]");

                    if (links is { Count: > 0 })
                    {
                        Console.WriteLine($"找到{links.Count}个可能的书籍链接");

                        foreach (var link in links)
                        {
                            string href = link.GetAttributeValue("href", "");

                            if (!string.IsNullOrEmpty(href) && href.Contains("/lilunshuji/") &&
                                !href.Contains("index___") && !href.EndsWith("/lilunshuji/"))
                            {
                                string title = link.InnerText.Trim();
                                if (string.IsNullOrEmpty(title)) continue;

                                // 移除书名中的《》
                                title = title.Replace("《", "").Replace("》", "").Trim();

                                // 排除词典
                                bool shouldExclude = false;
                                foreach (var keyword in _excludeKeywords)
                                {
                                    if (title.Contains(keyword))
                                    {
                                        shouldExclude = true;
                                        break;
                                    }
                                }

                                if (!shouldExclude)
                                {
                                    // 确保链接是完整URL
                                    if (!href.StartsWith("http"))
                                    {
                                        href = _baseUrl + (href.StartsWith("/") ? "" : "/") + href;
                                    }

                                    var bookInfo = new BookInfo
                                    {
                                        Title = title,
                                        Url = href,
                                        Category = "未分类" // 先设为未分类，后续会更新
                                    };

                                    // 检查重复
                                    if (!bookLinks.Any(b => b.Title == title || b.Url == href))
                                    {
                                        bookLinks.Add(bookInfo);
                                        Console.WriteLine($"  - {title} [{href}]");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("未能找到任何书籍链接");
                        AnalyzePageStructure(doc);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取书籍列表时出错: {ex.Message}");
            }

            return bookLinks;
        }
        private async Task GetCategoriesForBooksAsync(List<BookInfo> books)
        {
            // 为每本书获取分类，并添加新发现的书籍
            Console.WriteLine("开始获取书籍分类信息并收集额外书籍...");
            int existingBookCount = books.Count;
            int newBookCount = 0;
            int processedCount = 0;

            // 用于防止添加重复书籍的集合
            HashSet<string> existingBookTitles = new HashSet<string>(books.Select(b => b.Title));

            // 遍历所有标签页面
            foreach (var category in _tagCategories)
            {
                string categoryId = category.Key;
                string categoryName = category.Value;
                string categoryUrl = $"{_baseUrl}/lilunshuji/index___{categoryId}.html";

                Console.WriteLine($"\n检查 [{categoryName}] 分类: {categoryUrl}");

                try
                {
                    var pageHtml = await RequestWithRetryAsync(categoryUrl);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(pageHtml);

                    // 查找list-content容器
                    var listContentNode = doc.DocumentNode.SelectSingleNode("//div[@id='list-content']");

                    if (listContentNode == null)
                    {
                        Console.WriteLine($"在 [{categoryName}] 分类页面未找到list-content容器");
                        continue;
                    }

                    // 找到书籍链接列表
                    var linkNodes = listContentNode.SelectNodes(".//ul/li/a");

                    if (linkNodes == null || linkNodes.Count == 0)
                    {
                        Console.WriteLine($"在 [{categoryName}] 分类页面未找到书籍链接");
                        continue;
                    }

                    Console.WriteLine($"在 [{categoryName}] 分类下找到 {linkNodes.Count} 个书籍链接");
                    int categoryNewBooks = 0;

                    foreach (var linkNode in linkNodes)
                    {
                        string title = linkNode.GetAttributeValue("title", "").Trim();
                        if (string.IsNullOrEmpty(title))
                        {
                            title = linkNode.InnerText.Trim();
                        }

                        // 移除书名中的《》
                        title = title.Replace("《", "").Replace("》", "").Trim();

                        // 排除词典
                        bool shouldExclude = false;
                        foreach (var keyword in _excludeKeywords)
                        {
                            if (title.Contains(keyword))
                            {
                                shouldExclude = true;
                                break;
                            }
                        }

                        if (shouldExclude)
                            continue;

                        string link = linkNode.GetAttributeValue("href", "");
                        if (string.IsNullOrEmpty(link))
                            continue;

                        // 确保链接是完整URL
                        if (!link.StartsWith("http"))
                        {
                            link = _baseUrl + (link.StartsWith("/") ? "" : "/") + link;
                        }

                        // 查找匹配的书籍并更新分类
                        var matchedBook = books.FirstOrDefault(b => b.Title == title);
                        if (matchedBook != null)
                        {
                            matchedBook.Category = categoryName;
                            processedCount++;
                        }
                        else if (!existingBookTitles.Contains(title))
                        {
                            // 如果是新书籍，添加到爬取列表
                            var newBook = new BookInfo
                            {
                                Title = title,
                                Url = link,
                                Category = categoryName
                            };

                            books.Add(newBook);
                            existingBookTitles.Add(title);
                            newBookCount++;
                            categoryNewBooks++;

                            Console.WriteLine($"  + 新增书籍: {title} [{link}]");
                        }
                    }

                    if (categoryNewBooks > 0)
                    {
                        Console.WriteLine($"在 [{categoryName}] 分类下新增了 {categoryNewBooks} 本书籍");
                    }

                    // 在分类之间等待2-4秒
                    int delay = _random.Next(2000, 4000);
                    Console.WriteLine($"等待{delay / 1000.0:F1}秒后继续下一个分类...");
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"获取 [{categoryName}] 分类信息时出错: {ex.Message}");
                }
            }

            Console.WriteLine($"\n已为 {processedCount} 本已存在书籍分配分类");
            Console.WriteLine($"从分类页面新增了 {newBookCount} 本书籍，总计 {books.Count} 本待爬取");

            // 为未分类的书籍设置默认分类
            int unclassifiedCount = books.Count(b => b.Category == "未分类");
            if (unclassifiedCount > 0)
            {
                Console.WriteLine($"有 {unclassifiedCount} 本书籍未能确定分类，将设为'其他'");
                foreach (var book in books.Where(b => b.Category == "未分类"))
                {
                    book.Category = "其他";
                }
            }
        }
        private void AnalyzePageStructure(HtmlDocument doc)
        {
            try
            {
                Console.WriteLine("分析页面结构:");

                // 查找所有div元素
                var allDivs = doc.DocumentNode.SelectNodes("//div");
                if (allDivs != null)
                {
                    Console.WriteLine($"页面包含 {allDivs.Count} 个div元素");

                    // 查找可能的容器元素
                    var containers = allDivs
                        .Where(d => d.GetAttributeValue("class", "").Contains("list") ||
                                   d.GetAttributeValue("class", "").Contains("book") ||
                                   d.GetAttributeValue("class", "").Contains("content"))
                        .Take(5)
                        .ToList();

                    if (containers.Any())
                    {
                        Console.WriteLine("可能的容器元素:");
                        foreach (var container in containers)
                        {
                            Console.WriteLine($"  - <{container.Name} class=\"{container.GetAttributeValue("class", "")}\" id=\"{container.GetAttributeValue("id", "")}\">");
                        }
                    }
                }

                // 查找所有链接
                var allLinks = doc.DocumentNode.SelectNodes("//a");
                if (allLinks is { Count: > 0 })
                {
                    Console.WriteLine($"页面包含 {allLinks.Count} 个链接");

                    // 获取可能是书籍链接的前5个
                    var bookLinks = allLinks
                        .Where(a => a.GetAttributeValue("href", "").Contains("/lilunshuji/") &&
                              !a.GetAttributeValue("href", "").Contains("index___"))
                        .Take(5)
                        .ToList();

                    if (bookLinks.Any())
                    {
                        Console.WriteLine("可能的书籍链接样本:");
                        foreach (var link in bookLinks)
                        {
                            Console.WriteLine($"  - {link.InnerText.Trim()} [{link.GetAttributeValue("href", "")}]");
                        }
                    }
                }

                // 查找所有ul和li元素
                var allUls = doc.DocumentNode.SelectNodes("//ul");
                if (allUls != null)
                {
                    Console.WriteLine($"页面包含 {allUls.Count} 个ul元素");

                    foreach (var ul in allUls.Take(3))
                    {
                        var lis = ul.SelectNodes(".//li");
                        if (lis is { Count: > 0 })
                        {
                            Console.WriteLine($"  - ul包含 {lis.Count} 个li元素");
                            var firstLi = lis.First();
                            Console.WriteLine($"    第一个li: {firstLi.InnerText.Trim()}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"分析页面结构时出错: {ex.Message}");
            }
        }

        private async Task ScrapeBookAsync(string bookUrl, BookInfo bookInfo)
        {
            try
            {
                var pageHtml = await RequestWithRetryAsync(bookUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(pageHtml);

                Console.WriteLine($"开始提取书籍内容: {bookInfo.Title}");

                // 提取书籍元数据
                ExtractBookMetadata(doc, bookInfo);

                // 检查容器元素
                var contentNode = doc.DocumentNode.SelectSingleNode("//div[@id='container']") ??
                                 doc.DocumentNode.SelectSingleNode("//div[@class='container']") ??
                                 doc.DocumentNode.SelectSingleNode("//div[@class='content']");

                if (contentNode == null)
                {
                    Console.WriteLine("未找到内容容器，尝试使用body元素");
                    contentNode = doc.DocumentNode.SelectSingleNode("//body");

                    if (contentNode == null)
                    {
                        throw new Exception("无法找到内容容器");
                    }
                }

                // 提取标题层级结构
                var book = await ExtractBookContentAsync(contentNode, bookInfo);

                // 保存书籍
                await _repository.SaveBookAsync(book);

                Console.WriteLine($"书籍 {bookInfo.Title} 提取和保存完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"爬取书籍内容时出错: {ex.Message}");
                throw;
            }
        }

        private void ExtractBookMetadata(HtmlDocument doc, BookInfo bookInfo)
        {
            try
            {
                // 查找可能包含作者、朝代、年份等信息的元素
                var h2Nodes = doc.DocumentNode.SelectNodes("//h2");

                if (h2Nodes != null)
                {
                    foreach (var h2 in h2Nodes)
                    {
                        string text = h2.InnerText.Trim();

                        // 检查是否包含作者、朝代、年份等信息
                        if (text.Contains("作者") || text.Contains("朝代") || text.Contains("年份"))
                        {
                            Console.WriteLine($"找到书籍信息: {text}");

                            // 提取作者
                            var authorMatch = Regex.Match(text, @"作者[：:]\s*([^\s，。]+)");
                            if (authorMatch.Success)
                            {
                                bookInfo.Author = authorMatch.Groups[1].Value.Trim();
                            }

                            // 提取朝代
                            var dynastyMatch = Regex.Match(text, @"朝代[：:]\s*([^\s，。]+)");
                            if (dynastyMatch.Success)
                            {
                                bookInfo.Dynasty = dynastyMatch.Groups[1].Value.Trim();
                            }

                            // 提取年份
                            var yearMatch = Regex.Match(text, @"年份[：:]\s*([^\s，。]+)");
                            if (yearMatch.Success)
                            {
                                bookInfo.Year = yearMatch.Groups[1].Value.Trim();
                            }

                            break; // 找到信息后停止循环
                        }
                    }
                }

                // 如果在h2中未找到，尝试在其他元素中查找
                if (string.IsNullOrEmpty(bookInfo.Author) || string.IsNullOrEmpty(bookInfo.Dynasty))
                {
                    // 寻找可能包含作者信息的文本
                    var authorNodes = doc.DocumentNode.SelectNodes("//p[contains(text(), '作者') or contains(text(), '撰')]");

                    if (authorNodes != null)
                    {
                        foreach (var node in authorNodes)
                        {
                            string text = node.InnerText.Trim();

                            // 提取作者
                            var authorMatch = Regex.Match(text, @"作者[：:]\s*([^\s，。]+)");
                            if (authorMatch.Success && string.IsNullOrEmpty(bookInfo.Author))
                            {
                                bookInfo.Author = authorMatch.Groups[1].Value.Trim();
                            }

                            // 提取朝代
                            var dynastyMatch = Regex.Match(text, @"朝代[：:]\s*([^\s，。]+)|([唐宋元明清][代])\s*([^\s，。]+)");
                            if (dynastyMatch.Success && string.IsNullOrEmpty(bookInfo.Dynasty))
                            {
                                bookInfo.Dynasty = dynastyMatch.Groups[1].Value.Trim();
                                if (string.IsNullOrEmpty(bookInfo.Dynasty))
                                {
                                    bookInfo.Dynasty = dynastyMatch.Groups[2].Value.Trim();
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"提取到的书籍元数据: 作者={bookInfo.Author ?? "未知"}, 朝代={bookInfo.Dynasty ?? "未知"}, 年份={bookInfo.Year ?? "未知"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提取书籍元数据时出错: {ex.Message}");
            }
        }

        private async Task<Book> ExtractBookContentAsync(HtmlNode contentNode, BookInfo bookInfo)
        {
            try
            {
                var book = new Book
                {
                    Title = bookInfo.Title,
                    Author = bookInfo.Author,
                    Dynasty = bookInfo.Dynasty,
                    Year = bookInfo.Year,
                    Category = bookInfo.Category,
                    Url = bookInfo.Url,
                    Chapters = new List<BookChapter>()
                };

                // 寻找所有标题元素
                var headingNodes = contentNode.SelectNodes(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");

                if (headingNodes is { Count: > 0 })
                {
                    Console.WriteLine($"找到{headingNodes.Count}个标题元素");

                    // 按层级结构提取
                    BookChapter currentChapter = null;
                    BookSection currentSection = null;
                    StringBuilder currentContent = new StringBuilder();

                    // 遍历所有节点来构建章节结构
                    var allNodes = contentNode.DescendantsAndSelf();

                    foreach (var node in allNodes)
                    {
                        // 如果是标题元素
                        if (node.Name.StartsWith("h") && node.Name.Length == 2 && char.IsDigit(node.Name[1]))
                        {
                            // 保存之前收集的内容
                            if (currentContent.Length > 0)
                            {
                                if (currentSection != null)
                                {
                                    currentSection.Content += currentContent.ToString().Trim();
                                }
                                else if (currentChapter != null)
                                {
                                    currentChapter.Content += currentContent.ToString().Trim();
                                }

                                currentContent.Clear();
                            }

                            // 获取标题级别
                            int level = int.Parse(node.Name.Substring(1));
                            string title = node.InnerText.Trim();

                            // 跳过包含元数据的标题
                            if (title.Contains("作者：") || title.Contains("朝代："))
                                continue;

                            Console.WriteLine($"处理{node.Name}标题: {title}");

                            // 处理不同级别的标题
                            if (level <= 2) // h1或h2视为章
                            {
                                currentChapter = new BookChapter
                                {
                                    Title = title,
                                    Sections = new List<BookSection>(),
                                    Content = ""
                                };

                                book.Chapters.Add(currentChapter);
                                currentSection = null;
                            }
                            else // h3及以下视为节
                            {
                                if (currentChapter == null)
                                {
                                    currentChapter = new BookChapter
                                    {
                                        Title = "正文",
                                        Sections = new List<BookSection>(),
                                        Content = ""
                                    };

                                    book.Chapters.Add(currentChapter);
                                }

                                currentSection = new BookSection
                                {
                                    Title = title,
                                    Content = ""
                                };

                                currentChapter.Sections.Add(currentSection);
                            }
                        }
                        // 如果是文本内容元素
                        else if (node.Name == "p" || node is { Name: "div", HasChildNodes: false })
                        {
                            string text = node.InnerText.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                currentContent.AppendLine(text);
                                currentContent.AppendLine(); // 额外添加一个换行
                            }
                        }
                    }

                    // 处理最后一段内容
                    if (currentContent.Length > 0)
                    {
                        if (currentSection != null)
                        {
                            currentSection.Content += currentContent.ToString().Trim();
                        }
                        else if (currentChapter != null)
                        {
                            currentChapter.Content += currentContent.ToString().Trim();
                        }
                        else
                        {
                            // 如果没有找到任何章节，创建一个默认章节
                            var defaultChapter = new BookChapter
                            {
                                Title = "正文",
                                Content = currentContent.ToString().Trim(),
                                Sections = new List<BookSection>()
                            };

                            book.Chapters.Add(defaultChapter);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("未找到标题元素，尝试提取整体内容");

                    // 如果没有标题结构，提取所有段落作为内容
                    var paragraphs = contentNode.SelectNodes(".//p");

                    if (paragraphs is { Count: > 0 })
                    {
                        var defaultChapter = new BookChapter
                        {
                            Title = "正文",
                            Content = "",
                            Sections = new List<BookSection>()
                        };

                        StringBuilder content = new StringBuilder();

                        foreach (var p in paragraphs)
                        {
                            string text = p.InnerText.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                content.AppendLine(text);
                                content.AppendLine(); // 额外添加一个换行
                            }
                        }

                        defaultChapter.Content = content.ToString().Trim();
                        book.Chapters.Add(defaultChapter);
                    }
                    else
                    {
                        // 如果连段落都没有，提取所有文本
                        string fullText = contentNode.InnerText.Trim();

                        if (!string.IsNullOrEmpty(fullText))
                        {
                            var defaultChapter = new BookChapter
                            {
                                Title = "正文",
                                Content = fullText,
                                Sections = new List<BookSection>()
                            };

                            book.Chapters.Add(defaultChapter);
                        }
                        else
                        {
                            Console.WriteLine("未找到有效内容");
                        }
                    }
                }

                Console.WriteLine($"提取了 {book.Chapters.Count} 个章节");
                foreach (var chapter in book.Chapters)
                {
                    Console.WriteLine($"  章节: {chapter.Title} ({chapter.Content.Length} 字符, {chapter.Sections.Count} 小节)");
                }

                return book;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提取书籍内容时出错: {ex.Message}");

                // 创建一个空书籍对象，避免返回null
                var emptyBook = new Book
                {
                    Title = bookInfo.Title,
                    Author = bookInfo.Author,
                    Dynasty = bookInfo.Dynasty,
                    Year = bookInfo.Year,
                    Category = bookInfo.Category,
                    Url = bookInfo.Url,
                    Chapters = new List<BookChapter>
                    {
                        new()
                        {
                            Title = "提取失败",
                            Content = $"提取内容时出错: {ex.Message}",
                            Sections = new List<BookSection>()
                        }
                    }
                };

                return emptyBook;
            }
        }

        private async Task<string> RequestWithRetryAsync(string url, int maxRetries = 3)
        {
            int retries = 0;
            while (retries < maxRetries)
            {
                try
                {
                    return await _httpClient.GetStringAsync(url);
                }
                catch (Exception ex)
                {
                    retries++;

                    if (retries == maxRetries)
                    {
                        throw; // 重试次数用完，抛出异常
                    }

                    Console.WriteLine($"请求失败，正在进行第{retries}次重试... ({ex.Message})");

                    // 等待时间随重试次数增加
                    int delay = retries * 2000 + _random.Next(1000, 3000);
                    await Task.Delay(delay);
                }
            }

            throw new Exception("请求失败，已达到最大重试次数");
        }

        public void Dispose()
        {
            // TODO 在此释放托管资源
        }

        [GeneratedRegex(@"/index\.html$")]
        private static partial Regex MyRegex();
    }
}