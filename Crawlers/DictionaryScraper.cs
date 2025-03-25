using Crawlers.Models;
using Crawlers.Repository;
using HtmlAgilityPack;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Crawlers;

public partial class DictionaryScraper : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl = "https://www.zysj.com.cn";
    private readonly Random _random = new();
    private readonly DictionaryRepository _repository;

    // 词典页面URLs，从2050到2059
    private readonly List<string> _dictionaryUrls = new()
    {
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2050/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2051/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2052/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2053/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2054/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2055/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2056/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2057/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2058/quanben.html",
        "https://www.zysj.com.cn/lilunshuji/zhongyicidian2059/quanben.html"
    };

    public DictionaryScraper()
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

        // 初始化仓库
        _repository = new DictionaryRepository();
    }

    public async Task StartScrapingAsync()
    {
        Console.WriteLine("中医词典爬虫程序启动...");

        try
        {
            var totalPages = _dictionaryUrls.Count;
            var currentPage = 0;

            foreach (var url in _dictionaryUrls)
            {
                currentPage++;
                Console.WriteLine($"正在处理[{currentPage}/{totalPages}]页面");
                Console.WriteLine($"URL: {url}");

                await ScrapePageAsync(url);

                // 随机等待5-10秒，避免请求过于频繁
                var delay = _random.Next(5000, 10000);
                Console.WriteLine($"等待{delay / 1000.0:F1}秒后继续...");
                await Task.Delay(delay);
            }

            Console.WriteLine("所有词典页面爬取完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"爬取过程中出错: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private async Task<string> RequestWithRetryAsync(string url, int maxRetries = 3)
    {
        var retries = 0;
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
                var delay = retries * 2000 + _random.Next(1000, 3000);
                await Task.Delay(delay);
            }
        }

        throw new Exception("请求失败，已达到最大重试次数");
    }

    private async Task ScrapePageAsync(string pageUrl)
    {
        try
        {
            Console.WriteLine($"正在访问: {pageUrl}");
            var pageHtml = await RequestWithRetryAsync(pageUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);

            // 查找所有词条div（class="section"）
            var sectionNodes = doc.DocumentNode.SelectNodes("//div[@class='section']");

            if (sectionNodes is { Count: > 0 })
            {
                Console.WriteLine($"找到{sectionNodes.Count}个词条节点");
                var processedCount = 0;
                var savedCount = 0;

                foreach (var sectionNode in sectionNodes)
                {
                    processedCount++;

                    try
                    {
                        // 提取ID
                        var id = sectionNode.GetAttributeValue("id", "");

                        // 提取标题（h4.title）
                        var titleNode = sectionNode.SelectSingleNode(".//h4[@class='title']");
                        if (titleNode == null)
                        {
                            titleNode = sectionNode.SelectSingleNode(".//h4") ??
                                        sectionNode.SelectSingleNode(".//h3") ??
                                        sectionNode.SelectSingleNode(".//h2");
                        }

                        // 提取内容（p标签）
                        var contentNodes = sectionNode.SelectNodes(".//p");

                        if (titleNode != null)
                        {
                            var term = titleNode.InnerText.Trim();

                            if (string.IsNullOrEmpty(term))
                                continue;

                            // 合并所有p标签内容
                            var definitionBuilder = new StringBuilder();
                            if (contentNodes != null)
                            {
                                foreach (var p in contentNodes)
                                {
                                    var pText = p.InnerText.Trim();
                                    if (!string.IsNullOrEmpty(pText))
                                    {
                                        if (definitionBuilder.Length > 0)
                                            definitionBuilder.AppendLine();

                                        definitionBuilder.Append(pText);
                                    }
                                }
                            }

                            var definition = definitionBuilder.ToString().Trim();
                            if (string.IsNullOrEmpty(definition))
                                continue;

                            // 创建词条 - 不再设置Category字段
                            var entry = new DictionaryEntry
                            {
                                Term = term,
                                Category = "", // 不再设置分类
                                Url = pageUrl,
                                Contents = new List<DictionaryContent>
                                {
                                    new()
                                    {
                                        Source = "《中医词典》",
                                        Definition = definition,
                                        RelatedTerms = new List<string>()
                                    }
                                }
                            };

                            // 提取相关词条
                            ExtractRelatedTerms(entry);

                            // 保存词条
                            if (await _repository.SaveEntryAsync(entry))
                                savedCount++;

                            if (processedCount % 10 == 0 || processedCount == sectionNodes.Count)
                            {
                                Console.WriteLine($"已处理{processedCount}/{sectionNodes.Count}个词条，成功保存{savedCount}个");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"处理词条时出错: {ex.Message}");
                    }
                }

                Console.WriteLine($"页面处理完成: 总共找到{sectionNodes.Count}个词条，成功保存{savedCount}个");
            }
            else
            {
                Console.WriteLine("未找到词条节点，尝试其它方式...");

                // 尝试查找其它可能的结构
                var containerNode = doc.DocumentNode.SelectSingleNode("//div[@id='container']") ??
                                    doc.DocumentNode.SelectSingleNode("//div[@class='container']");

                if (containerNode != null)
                {
                    Console.WriteLine("找到container节点，尝试提取内容...");
                    await ProcessContainerContent(containerNode, pageUrl);
                }
                else
                {
                    Console.WriteLine("未找到有效内容结构，爬取失败");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"爬取页面出错: {ex.Message}");
        }
    }

    private async Task ProcessContainerContent(HtmlNode containerNode, string pageUrl)
    {
        try
        {
            // 尝试使用清单和标题组合的方式提取
            var headers = containerNode.SelectNodes(".//h1|.//h2|.//h3|.//h4");
            var paragraphs = containerNode.SelectNodes(".//p");

            if (headers is { Count: > 0 })
            {
                Console.WriteLine($"找到{headers.Count}个标题，尝试以标题为词条名称提取");

                Dictionary<int, string> headerIndexMap = new Dictionary<int, string>();
                Dictionary<string, string> termDefinitionMap = new Dictionary<string, string>();

                // 收集所有标题及其索引
                foreach (var header in headers)
                {
                    var term = header.InnerText.Trim();
                    if (!string.IsNullOrEmpty(term) && header.Line > 0)
                    {
                        headerIndexMap[header.Line] = term;
                    }
                }

                if (headerIndexMap.Count > 0 && paragraphs is { Count: > 0 })
                {
                    // 按行号排序标题
                    var sortedHeaders = headerIndexMap.OrderBy(kv => kv.Key).ToList();

                    // 对每个段落，找到它前面最近的标题
                    foreach (var p in paragraphs)
                    {
                        if (p.Line <= 0) continue;

                        var content = p.InnerText.Trim();
                        if (string.IsNullOrEmpty(content)) continue;

                        // 找到前面最近的标题
                        string currentTerm = null;
                        for (var i = sortedHeaders.Count - 1; i >= 0; i--)
                        {
                            if (sortedHeaders[i].Key < p.Line)
                            {
                                currentTerm = sortedHeaders[i].Value;
                                break;
                            }
                        }

                        if (currentTerm != null)
                        {
                            // 将内容添加到相应词条
                            if (termDefinitionMap.ContainsKey(currentTerm))
                            {
                                termDefinitionMap[currentTerm] += "\n" + content;
                            }
                            else
                            {
                                termDefinitionMap[currentTerm] = content;
                            }
                        }
                    }

                    // 保存提取到的词条
                    var savedCount = 0;
                    foreach (var kvp in termDefinitionMap)
                    {
                        if (!string.IsNullOrEmpty(kvp.Value))
                        {
                            var entry = new DictionaryEntry
                            {
                                Term = kvp.Key,
                                Category = "", // 不再设置分类
                                Url = pageUrl,
                                Contents = new List<DictionaryContent>
                                {
                                    new()
                                    {
                                        Source = "《中医词典》",
                                        Definition = kvp.Value,
                                        RelatedTerms = new List<string>()
                                    }
                                }
                            };

                            ExtractRelatedTerms(entry);

                            if (await _repository.SaveEntryAsync(entry))
                                savedCount++;
                        }
                    }

                    Console.WriteLine($"通过标题-内容关联方式提取并保存了{savedCount}个词条");
                }
                else
                {
                    Console.WriteLine("未找到足够的标题和段落内容");
                }
            }
            else
            {
                Console.WriteLine("未找到标题元素，尝试文本模式提取");

                // 如果没有明显的结构，回退到文本处理
                var fullText = containerNode.InnerText;
                await ProcessTextContentAsync(fullText, pageUrl);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理容器内容时出错: {ex.Message}");
        }
    }

    private async Task ProcessTextContentAsync(string text, string pageUrl)
    {
        try
        {
            // 移除HTML标签
            text = MyRegex().Replace(text, "");

            // 清理空白字符
            text = MyRegex1().Replace(text, " ").Trim();

            // 按可能的词条分隔符拆分
            var entryTexts = Regex.Split(text, @"(?=\s*[【〔《]|^\s*[\u4e00-\u9fa5]{1,10}\s*[。：])");

            Console.WriteLine($"通过文本模式提取到{entryTexts.Length}个可能的词条片段");

            var savedCount = 0;

            foreach (var entryText in entryTexts)
            {
                var trimmedText = entryText.Trim();
                if (string.IsNullOrEmpty(trimmedText) || trimmedText.Length < 5) // 太短的忽略
                    continue;

                // 尝试提取词条名和定义
                string term = null;
                string definition = null;

                // 常见格式：【词条】定义
                var bracketMatch = Regex.Match(trimmedText, "^[【〔《]([^】〕》]+)[】〕》](.+)$");
                if (bracketMatch.Success)
                {
                    term = bracketMatch.Groups[1].Value.Trim();
                    definition = bracketMatch.Groups[2].Value.Trim();
                }
                else
                {
                    // 尝试另一种格式：词条：定义
                    var colonMatch = Regex.Match(trimmedText, @"^([\u4e00-\u9fa5]{1,10})\s*[：:]\s*(.+)$");
                    if (colonMatch.Success)
                    {
                        term = colonMatch.Groups[1].Value.Trim();
                        definition = colonMatch.Groups[2].Value.Trim();
                    }
                }

                // 如果提取成功且有效，保存词条
                if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(definition) && term.Length <= 10)
                {
                    Console.WriteLine($"提取到词条: {term}");

                    var entry = new DictionaryEntry
                    {
                        Term = term,
                        Category = "", // 不再设置分类
                        Url = pageUrl,
                        Contents = new List<DictionaryContent>
                        {
                            new()
                            {
                                Source = "《中医词典》",
                                Definition = definition,
                                RelatedTerms = new List<string>()
                            }
                        }
                    };

                    ExtractRelatedTerms(entry);

                    if (await _repository.SaveEntryAsync(entry))
                        savedCount++;
                }
            }

            Console.WriteLine($"通过文本模式提取并保存了{savedCount}个词条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理文本内容时出错: {ex.Message}");
        }
    }

    private void ExtractRelatedTerms(DictionaryEntry entry)
    {
        try
        {
            if (entry?.Contents == null || entry.Contents.Count == 0)
            {
                return;
            }

            var content = entry.Contents[0];
            var definition = content.Definition;

            if (string.IsNullOrEmpty(definition))
            {
                return;
            }

            // 提取参考文献
            var referencePatterns = new List<Regex>
            {
                new("(?:见|参|引|出|详)《([^》]+)》"),
                new("《([^》]+)》[卷篇章]")
            };

            var references = new HashSet<string>();

            foreach (var pattern in referencePatterns)
            {
                var matches = pattern.Matches(definition);
                foreach (Match match in matches)
                {
                    references.Add(match.Value.Trim());
                }
            }

            if (references.Count > 0)
            {
                content.Reference = string.Join("; ", references);
            }

            // 提取相关词条
            var relatedTermPatterns = new List<Regex>
            {
                new("【([^】]+)】"),   // 【词条】
                new("《([^》]+)》"),   // 《词条》
                new("\"([^\"\"]+)\"")     // "词条"
            };

            var relatedTerms = new HashSet<string>();

            foreach (var pattern in relatedTermPatterns)
            {
                var matches = pattern.Matches(definition);
                foreach (Match match in matches)
                {
                    var relatedTerm = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(relatedTerm) &&
                        relatedTerm != entry.Term &&
                        relatedTerm.Length <= 20 &&  // 避免匹配过长文本
                        !relatedTerm.Contains("《") && !relatedTerm.Contains("》")) // 避免嵌套引用
                    {
                        relatedTerms.Add(relatedTerm);
                    }
                }
            }

            content.RelatedTerms = relatedTerms.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"提取相关词条时出错: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // TODO 在此释放托管资源
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex MyRegex();
    [GeneratedRegex(@"\s+")]
    private static partial Regex MyRegex1();
}