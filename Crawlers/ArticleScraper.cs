using Crawlers.Models;
using Crawlers.Repository;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;

namespace Crawlers
{
    public partial class ArticleScraper : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://www.zysj.com.cn";
        private readonly string _baseZajiUrl = "https://www.zysj.com.cn/zaji";
        private readonly Random _random = new();
        private readonly ArticleRepository _repository;
        private readonly ConcurrentDictionary<int, bool> _processedPages = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        // 爬虫配置
        private readonly int _maxConcurrency;
        private readonly int _delayBetweenRequests;
        private readonly int _maxPages;
        private readonly int _startPage;

        public ArticleScraper(int maxConcurrency = 5, int delayBetweenRequests = 2000, int startPage = 1, int maxPages = -1)
        {
            _maxConcurrency = maxConcurrency;
            _delayBetweenRequests = delayBetweenRequests;
            _startPage = startPage;
            _maxPages = maxPages;

            // 配置HttpClient
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.zysj.com.cn/");

            // 初始化仓库
            _repository = new ArticleRepository();
        }

        public async Task StartScrapingAsync()
        {
            Console.WriteLine("中医杂集多线程爬虫程序启动...");

            try
            {
                // 获取总页数
                int totalPages = await GetTotalPagesAsync();

                if (_maxPages > 0 && _maxPages < totalPages)
                {
                    Console.WriteLine($"根据设置，将只爬取 {_maxPages} 页（共 {totalPages} 页）");
                    totalPages = _maxPages;
                }
                else
                {
                    Console.WriteLine($"共有 {totalPages} 页杂集数据需要爬取");
                }

                // 创建数据流管道
                var pageProcessingBlock = CreatePageProcessingPipeline(totalPages);

                // 提交页面任务
                for (int i = _startPage; i <= totalPages; i++)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    await pageProcessingBlock.SendAsync(i);
                }

                // 完成发送，等待处理完成
                pageProcessingBlock.Complete();
                await pageProcessingBlock.Completion;

                // 显示统计信息
                Console.WriteLine("\n爬取完成！");
                Console.WriteLine($"成功处理页面数: {_processedPages.Count}");
                Console.WriteLine($"文章数据已保存到数据库");

                // 生成统计报告
                await GenerateStatisticsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"爬取过程中出错: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private TransformBlock<int, int> CreatePageProcessingPipeline(int totalPages)
        {
            // 创建页面处理块 - 限制并发度
            var pageProcessingBlock = new TransformBlock<int, int>(
                async pageNumber =>
                {
                    try
                    {
                        await ProcessPageAsync(pageNumber, totalPages);
                        return pageNumber;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理页面 {pageNumber} 时出错: {ex.Message}");
                        return -1;
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = _maxConcurrency,
                    CancellationToken = _cancellationTokenSource.Token
                }
            );

            // 创建结果处理块
            var resultProcessingBlock = new ActionBlock<int>(
                pageNumber =>
                {
                    if (pageNumber > 0)
                    {
                        _processedPages[pageNumber] = true;
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    CancellationToken = _cancellationTokenSource.Token
                }
            );

            // 链接管道
            pageProcessingBlock.LinkTo(resultProcessingBlock, new DataflowLinkOptions { PropagateCompletion = true });

            return pageProcessingBlock;
        }

        private async Task<int> GetTotalPagesAsync()
        {
            try
            {
                Console.WriteLine("获取杂集总页数...");

                var mainPageUrl = $"{_baseZajiUrl}/index.html";
                var pageHtml = await RequestWithRetryAsync(mainPageUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(pageHtml);

                // 查找分页信息
                var pageNodes = doc.DocumentNode.SelectNodes("//div[@id='page']//ul[@class='flow-x']//li//a[@class='BUTTON']");

                if (pageNodes != null && pageNodes.Count > 0)
                {
                    // 获取最后一个分页链接
                    var lastPageNode = pageNodes.LastOrDefault();
                    if (lastPageNode != null)
                    {
                        string href = lastPageNode.GetAttributeValue("href", "");
                        string title = lastPageNode.GetAttributeValue("title", "");

                        // 从链接或标题中提取页码
                        int totalPages = 0;

                        // 从标题中提取
                        Match titleMatch = MyRegex().Match(title);
                        if (titleMatch.Success && int.TryParse(titleMatch.Groups[1].Value, out totalPages))
                        {
                            Console.WriteLine($"从标题中提取到总页数: {totalPages}");
                            return totalPages;
                        }

                        // 从URL中提取
                        Match urlMatch = Regex.Match(href, @"index(\d+)\.html");
                        if (urlMatch.Success && int.TryParse(urlMatch.Groups[1].Value, out totalPages))
                        {
                            Console.WriteLine($"从URL中提取到总页数: {totalPages}");
                            return totalPages;
                        }
                    }
                }

                // 如果无法自动检测，使用用户提供的页数
                Console.WriteLine("无法自动检测总页数，使用默认值: 937");
                return 937;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取总页数时出错: {ex.Message}");
                return 937; // 默认页数
            }
        }

        private async Task ProcessPageAsync(int pageNumber, int totalPages)
        {
            string pageUrl = $"{_baseZajiUrl}/{(pageNumber == 1 ? "index.html" : $"index{pageNumber}.html")}";

            try
            {
                // 延迟请求，避免过快
                int delay = _random.Next(_delayBetweenRequests, _delayBetweenRequests * 2);
                await Task.Delay(delay);

                Console.WriteLine($"处理页面 {pageNumber}/{totalPages}: {pageUrl}");

                var pageHtml = await RequestWithRetryAsync(pageUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(pageHtml);

                // 查找文章列表
                var articleNodes = FindArticleNodes(doc);

                if (articleNodes != null && articleNodes.Count > 0)
                {
                    Console.WriteLine($"在页面 {pageNumber} 上找到 {articleNodes.Count} 篇文章");

                    foreach (var articleNode in articleNodes)
                    {
                        try
                        {
                            var article = ExtractArticleInfo(articleNode);

                            if (article != null && !string.IsNullOrEmpty(article.Title))
                            {
                                // 如果有详情链接，获取完整内容
                                if (!string.IsNullOrEmpty(article.Url))
                                {
                                    // 添加随机延迟，避免请求过于频繁
                                    await Task.Delay(_random.Next(500, 1500));

                                    try
                                    {
                                        await EnrichArticleWithDetailsAsync(article);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"获取文章详情时出错: {ex.Message}");
                                    }
                                }

                                // 保存文章
                                await _repository.SaveArticleAsync(article);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"处理文章时出错: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"在页面 {pageNumber} 上未找到文章列表");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理页面 {pageNumber} 时出错: {ex.Message}");
                throw;
            }
        }

        private HtmlNodeCollection FindArticleNodes(HtmlDocument doc)
        {
            // 尝试多种可能的选择器查找文章列表
            var articleNodes = doc.DocumentNode.SelectNodes("//ul[@class='article-list']/li");

            if (articleNodes == null || articleNodes.Count == 0)
            {
                // 尝试其他可能的选择器
                articleNodes = doc.DocumentNode.SelectNodes("//div[@class='article-list']/div");

                if (articleNodes == null || articleNodes.Count == 0)
                {
                    // 再尝试一种选择器
                    articleNodes = doc.DocumentNode.SelectNodes("//div[@id='list-content']//ul/li");

                    if (articleNodes == null || articleNodes.Count == 0)
                    {
                        // 分析页面结构
                        AnalyzeArticleListStructure(doc);
                    }
                }
            }

            return articleNodes;
        }

        private void AnalyzeArticleListStructure(HtmlDocument doc)
        {
            Console.WriteLine("分析页面结构以查找文章列表...");

            // 查找所有ul和div，可能是文章列表容器
            var uls = doc.DocumentNode.SelectNodes("//ul");
            var divs = doc.DocumentNode.SelectNodes("//div");

            if (uls != null)
            {
                Console.WriteLine($"页面包含 {uls.Count} 个ul元素");

                // 查找包含li较多的ul
                var potentialUls = uls
                    .Where(ul =>
                    {
                        var lis = ul.SelectNodes(".//li");
                        return lis != null && lis.Count >= 5; // 至少5个列表项
                    })
                    .Take(3)
                    .ToList();

                if (potentialUls.Any())
                {
                    Console.WriteLine("可能的文章列表ul元素:");
                    foreach (var ul in potentialUls)
                    {
                        var lis = ul.SelectNodes(".//li");
                        Console.WriteLine($"  - ul ({lis?.Count ?? 0} 个li): class=\"{ul.GetAttributeValue("class", "")}\"");

                        // 显示第一个li的内容
                        if (lis != null && lis.Count > 0)
                        {
                            var firstLi = lis.First();
                            var link = firstLi.SelectSingleNode(".//a");
                            if (link != null)
                            {
                                Console.WriteLine($"    示例: {link.InnerText.Trim()} [{link.GetAttributeValue("href", "")}]");
                            }
                            else
                            {
                                Console.WriteLine($"    示例: {firstLi.InnerText.Trim()}");
                            }
                        }
                    }
                }
            }

            if (divs != null)
            {
                // 查找可能包含文章的div
                var potentialDivs = divs
                    .Where(div => div.GetAttributeValue("class", "").Contains("list") ||
                                div.GetAttributeValue("id", "").Contains("list"))
                    .Take(3)
                    .ToList();

                if (potentialDivs.Any())
                {
                    Console.WriteLine("可能的文章列表div元素:");
                    foreach (var div in potentialDivs)
                    {
                        Console.WriteLine($"  - div: id=\"{div.GetAttributeValue("id", "")}\" class=\"{div.GetAttributeValue("class", "")}\"");
                    }
                }
            }

            // 查找所有链接
            var links = doc.DocumentNode.SelectNodes("//a");
            if (links != null && links.Count > 0)
            {
                Console.WriteLine($"页面包含 {links.Count} 个链接，前5个:");
                foreach (var link in links.Take(5))
                {
                    Console.WriteLine($"  - {link.InnerText.Trim()} [{link.GetAttributeValue("href", "")}]");
                }
            }
        }

        private Article ExtractArticleInfo(HtmlNode articleNode)
        {
            try
            {
                var article = new Article();

                // 查找标题链接
                var titleNode = articleNode.SelectSingleNode(".//a") ??
                               articleNode.SelectSingleNode(".//h2") ??
                               articleNode.SelectSingleNode(".//h3");

                if (titleNode != null)
                {
                    article.Title = titleNode.InnerText.Trim();

                    // 获取详情链接
                    string href = titleNode.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(href))
                    {
                        // 确保是完整URL
                        if (!href.StartsWith("http"))
                        {
                            href = _baseUrl + (href.StartsWith("/") ? "" : "/") + href;
                        }

                        article.Url = href;
                    }
                }
                else
                {
                    // 如果没有明显的标题节点，尝试提取所有文本作为标题
                    article.Title = articleNode.InnerText.Trim();
                }

                // 查找日期
                var dateNode = articleNode.SelectSingleNode(".//span[contains(@class, 'date')] | .//span[contains(@class, 'time')] | .//div[contains(@class, 'date')]");
                if (dateNode != null)
                {
                    string dateText = dateNode.InnerText.Trim();

                    // 尝试提取日期
                    Match dateMatch = Regex.Match(dateText, @"\d{4}-\d{1,2}-\d{1,2}|\d{4}/\d{1,2}/\d{1,2}|\d{4}\.\d{1,2}\.\d{1,2}");
                    if (dateMatch.Success)
                    {
                        article.PublishDate = dateMatch.Value;
                    }
                }

                // 查找预览内容
                var previewNode = articleNode.SelectSingleNode(".//div[contains(@class, 'summary')] | .//p[contains(@class, 'summary')] | .//span[contains(@class, 'summary')]");
                if (previewNode != null)
                {
                    article.Summary = previewNode.InnerText.Trim();
                }

                return article;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提取文章信息时出错: {ex.Message}");
                return null;
            }
        }

        private async Task EnrichArticleWithDetailsAsync(Article article)
        {
            try
            {
                if (string.IsNullOrEmpty(article.Url))
                    return;

                Console.WriteLine($"获取文章详情: {article.Title}");

                var pageHtml = await RequestWithRetryAsync(article.Url);
                var doc = new HtmlDocument();
                doc.LoadHtml(pageHtml);

                // 查找文章内容
                var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'article-content')] | //div[@id='content'] | //div[@class='content']");

                if (contentNode == null)
                {
                    // 尝试查找其他可能的内容容器
                    contentNode = doc.DocumentNode.SelectSingleNode("//div[@id='container'] | //div[@class='container'] | //div[@id='article']");
                }

                if (contentNode != null)
                {
                    // 获取文章正文
                    StringBuilder contentBuilder = new StringBuilder();

                    // 获取所有段落
                    var paragraphs = contentNode.SelectNodes(".//p");

                    if (paragraphs != null && paragraphs.Count > 0)
                    {
                        foreach (var p in paragraphs)
                        {
                            string text = p.InnerText.Trim();
                            if (!string.IsNullOrEmpty(text))
                            {
                                contentBuilder.AppendLine(text);
                                contentBuilder.AppendLine(); // 额外添加一个换行
                            }
                        }

                        article.Content = contentBuilder.ToString().Trim();
                    }
                    else
                    {
                        // 如果没有找到段落，获取容器的所有文本
                        article.Content = contentNode.InnerText.Trim();
                    }

                    // 尝试提取作者
                    var authorNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'author')] | //div[contains(@class, 'author')] | //p[contains(text(), '作者')]");
                    if (authorNode != null)
                    {
                        string authorText = authorNode.InnerText;
                        Match authorMatch = Regex.Match(authorText, @"作者[：:]\s*([^\s]+)");

                        if (authorMatch.Success)
                        {
                            article.Author = authorMatch.Groups[1].Value.Trim();
                        }
                        else
                        {
                            article.Author = authorText.Trim();
                        }
                    }

                    // 提取更精确的发布日期
                    if (string.IsNullOrEmpty(article.PublishDate))
                    {
                        var dateNode = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'date')] | //div[contains(@class, 'date')] | //span[contains(@class, 'time')]");
                        if (dateNode != null)
                        {
                            string dateText = dateNode.InnerText;
                            Match dateMatch = Regex.Match(dateText, @"\d{4}-\d{1,2}-\d{1,2}|\d{4}/\d{1,2}/\d{1,2}|\d{4}\.\d{1,2}\.\d{1,2}");

                            if (dateMatch.Success)
                            {
                                article.PublishDate = dateMatch.Value;
                            }
                        }
                    }

                    // 提取标签
                    var tagNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'tag')]/a | //div[contains(@class, 'tags')]/a | //span[contains(@class, 'tag')]/a");
                    if (tagNodes != null)
                    {
                        article.Tags = tagNodes
                            .Select(node => node.InnerText.Trim())
                            .Where(tag => !string.IsNullOrEmpty(tag))
                            .ToList();
                    }
                }
                else
                {
                    Console.WriteLine("未找到文章内容");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取文章详情时出错: {ex.Message}");
            }
        }

        private async Task GenerateStatisticsAsync()
        {
            try
            {
                var stats = await _repository.GetStatisticsAsync();

                Console.WriteLine("\n=== 杂集爬取统计 ===");
                Console.WriteLine($"总文章数: {stats.TotalArticles}");
                Console.WriteLine($"有完整内容文章数: {stats.ArticlesWithContent}");
                Console.WriteLine($"有作者信息文章数: {stats.ArticlesWithAuthor}");
                Console.WriteLine($"有日期信息文章数: {stats.ArticlesWithDate}");
                Console.WriteLine($"有标签信息文章数: {stats.ArticlesWithTags}");

                if (stats.TopTags.Count > 0)
                {
                    Console.WriteLine("\n热门标签:");
                    foreach (var tag in stats.TopTags)
                    {
                        Console.WriteLine($"  {tag.Key}: {tag.Value}篇文章");
                    }
                }

                // 保存统计信息到文件
                string statsFilePath = Path.Combine(_repository.GetJsonBasePath(), "statistics.json");
                string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
                await File.WriteAllTextAsync(statsFilePath, json, Encoding.UTF8);

                Console.WriteLine($"\n统计信息已保存至: {statsFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"生成统计信息时出错: {ex.Message}");
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
                    int delay = retries * 1000 + _random.Next(500, 1500);
                    await Task.Delay(delay);
                }
            }

            throw new Exception("请求失败，已达到最大重试次数");
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _httpClient?.Dispose();
            _repository?.Dispose();
        }

        // 停止爬取
        public void StopScraping()
        {
            Console.WriteLine("正在停止爬取...");
            _cancellationTokenSource?.Cancel();
        }

        [GeneratedRegex(@"第(\d+)页")]
        private static partial Regex MyRegex();
    }
}