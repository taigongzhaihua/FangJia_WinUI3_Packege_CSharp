using Crawlers.Models;
using Crawlers.Repository;
using HtmlAgilityPack;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Crawlers;

public partial class TerminologyScraper : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _terminologyUrl = "https://www.zysj.com.cn/lilunshuji/mingcicidian/quanben.html";
    private readonly Random _random = new();
    private readonly DictionaryRepository _repository;

    public TerminologyScraper()
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

        // 初始化仓库 - 使用专门的中医名词词典数据库
        _repository = new DictionaryRepository(
            liteDbPath: "TCMTerminology.litedb",
            sqliteDbPath: "TCMTerminology.db",
            jsonBasePath: "TCMTerminologyData"
        );
    }

    public async Task StartScrapingAsync()
    {
        Console.WriteLine("中医名词词典爬虫程序启动...");

        try
        {
            Console.WriteLine($"正在访问中医名词词典: {_terminologyUrl}");
            await ScrapeTerminologyDictionaryAsync();
            Console.WriteLine("中医名词词典爬取完成！");
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

    private async Task ScrapeTerminologyDictionaryAsync()
    {
        try
        {
            Console.WriteLine("开始下载页面内容...");
            var pageHtml = await RequestWithRetryAsync(_terminologyUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);

            // 分析页面结构并记录信息
            AnalyzePageStructure(doc);

            // 尝试多种可能的结构提取词条

            // 1. 首先尝试找section结构 (与中医词典相同的结构)
            var sectionNodes = doc.DocumentNode.SelectNodes("//div[@class='section']");
            if (sectionNodes is { Count: > 0 })
            {
                Console.WriteLine($"找到{sectionNodes.Count}个section节点，使用section模式提取");
                await ExtractFromSectionsAsync(sectionNodes);
                return;
            }

            // 2. 尝试查找container内的内容
            var containerNode = doc.DocumentNode.SelectSingleNode("//div[@id='container']") ??
                                doc.DocumentNode.SelectSingleNode("//div[@class='container']");

            if (containerNode != null)
            {
                Console.WriteLine("找到container节点，尝试提取其中的词条");

                // 2.1 尝试找dt/dd定义列表结构
                var dtNodes = containerNode.SelectNodes(".//dt");
                var ddNodes = containerNode.SelectNodes(".//dd");

                if (dtNodes is { Count: > 0 } && ddNodes is { Count: > 0 })
                {
                    Console.WriteLine($"找到定义列表结构，包含{dtNodes.Count}个dt和{ddNodes.Count}个dd，使用定义列表模式提取");
                    await ExtractFromDefinitionListAsync(containerNode);
                    return;
                }

                // 2.2 尝试段落标题模式
                var headers = containerNode.SelectNodes(".//h1|.//h2|.//h3|.//h4|.//h5|.//strong");
                var paragraphs = containerNode.SelectNodes(".//p");

                if (headers is { Count: > 0 } && paragraphs is { Count: > 0 })
                {
                    Console.WriteLine($"找到{headers.Count}个标题和{paragraphs.Count}个段落，使用标题段落关联模式提取");
                    await ExtractFromHeadersAndParagraphsAsync(headers, paragraphs);
                    return;
                }

                // 2.3 尝试纯文本模式
                Console.WriteLine("未找到特定结构，尝试使用文本模式提取");
                await ExtractFromTextContentAsync(containerNode.InnerText);
            }
            else
            {
                // 3. 如果找不到container节点，尝试从body提取
                Console.WriteLine("未找到container节点，尝试从body提取");
                var bodyNode = doc.DocumentNode.SelectSingleNode("//body");
                if (bodyNode != null)
                {
                    await ExtractFromTextContentAsync(bodyNode.InnerText);
                }
                else
                {
                    Console.WriteLine("无法解析页面结构");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"爬取页面出错: {ex.Message}");
        }
    }

    private void AnalyzePageStructure(HtmlDocument doc)
    {
        Console.WriteLine("页面结构分析:");

        try
        {
            // 检查常见容器元素
            string[] containers = { "container", "content", "main", "article", "body" };
            foreach (var container in containers)
            {
                var node = doc.DocumentNode.SelectSingleNode($"//div[@id='{container}']") ??
                           doc.DocumentNode.SelectSingleNode($"//div[@class='{container}']");

                if (node != null)
                {
                    Console.WriteLine($"找到'{container}'容器");
                }
            }

            // 检查文档的主要结构
            var sections = doc.DocumentNode.SelectNodes("//div[@class='section']");
            var dts = doc.DocumentNode.SelectNodes("//dt");
            var dds = doc.DocumentNode.SelectNodes("//dd");
            var headings = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4");
            var paragraphs = doc.DocumentNode.SelectNodes("//p");

            Console.WriteLine($"检测到的元素: " +
                              $"section: {(sections?.Count ?? 0)}, " +
                              $"dt: {(dts?.Count ?? 0)}, " +
                              $"dd: {(dds?.Count ?? 0)}, " +
                              $"headings: {(headings?.Count ?? 0)}, " +
                              $"p: {(paragraphs?.Count ?? 0)}");

            // 显示一些样本内容，帮助分析
            if (headings is { Count: > 0 })
            {
                Console.WriteLine("\n标题样本:");
                for (var i = 0; i < Math.Min(3, headings.Count); i++)
                {
                    Console.WriteLine($"  {headings[i].Name}: {headings[i].InnerText.Trim()}");
                }
            }

            if (paragraphs is { Count: > 0 })
            {
                Console.WriteLine("\n段落样本:");
                for (var i = 0; i < Math.Min(3, paragraphs.Count); i++)
                {
                    var sample = paragraphs[i].InnerText.Trim();
                    sample = sample.Length > 100 ? sample.Substring(0, 100) + "..." : sample;
                    Console.WriteLine($"  {sample}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"分析页面结构时出错: {ex.Message}");
        }
    }

    private async Task ExtractFromSectionsAsync(HtmlNodeCollection sectionNodes)
    {
        try
        {
            Console.WriteLine($"开始从{sectionNodes.Count}个section节点提取词条...");
            var processedCount = 0;
            var savedCount = 0;

            foreach (var sectionNode in sectionNodes)
            {
                processedCount++;

                try
                {
                    // 提取ID
                    var id = sectionNode.GetAttributeValue("id", "");

                    // 提取标题
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

                        // 创建词条
                        var entry = new DictionaryEntry
                        {
                            Term = term,
                            Category = "名词",
                            Url = _terminologyUrl,
                            Contents = new List<DictionaryContent>
                            {
                                new()
                                {
                                    Source = "《中医名词词典》",
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

            Console.WriteLine($"section提取完成: 总共找到{sectionNodes.Count}个词条，成功保存{savedCount}个");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从section提取词条时出错: {ex.Message}");
        }
    }

    private async Task ExtractFromDefinitionListAsync(HtmlNode containerNode)
    {
        try
        {
            // 查找所有dl/dt/dd结构
            var dlNodes = containerNode.SelectNodes(".//dl");

            if (dlNodes is { Count: > 0 })
            {
                Console.WriteLine($"找到{dlNodes.Count}个定义列表(dl)");
                var savedCount = 0;

                foreach (var dl in dlNodes)
                {
                    var dts = dl.SelectNodes(".//dt");
                    var dds = dl.SelectNodes(".//dd");

                    if (dts != null && dds != null)
                    {
                        Console.WriteLine($"处理定义列表，包含{dts.Count}个dt和{dds.Count}个dd");

                        // 假设dt和dd是一一对应的
                        var count = Math.Min(dts.Count, dds.Count);
                        for (var i = 0; i < count; i++)
                        {
                            var term = dts[i].InnerText.Trim();
                            var definition = dds[i].InnerText.Trim();

                            if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(definition))
                            {
                                var entry = new DictionaryEntry
                                {
                                    Term = term,
                                    Category = "名词",
                                    Url = _terminologyUrl,
                                    Contents = new List<DictionaryContent>
                                    {
                                        new()
                                        {
                                            Source = "《中医名词词典》",
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
                    }
                }

                Console.WriteLine($"定义列表提取完成，成功保存{savedCount}个词条");
            }
            else
            {
                // 也许dt/dd没有被dl包裹
                var dts = containerNode.SelectNodes(".//dt");
                var dds = containerNode.SelectNodes(".//dd");

                if (dts != null && dds != null)
                {
                    Console.WriteLine($"找到{dts.Count}个dt和{dds.Count}个dd");
                    var savedCount = 0;

                    // 假设dt和dd是一一对应的
                    var count = Math.Min(dts.Count, dds.Count);
                    for (var i = 0; i < count; i++)
                    {
                        var term = dts[i].InnerText.Trim();
                        var definition = dds[i].InnerText.Trim();

                        if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(definition))
                        {
                            var entry = new DictionaryEntry
                            {
                                Term = term,
                                Category = "名词",
                                Url = _terminologyUrl,
                                Contents = new List<DictionaryContent>
                                {
                                    new()
                                    {
                                        Source = "《中医名词词典》",
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

                    Console.WriteLine($"dt/dd提取完成，成功保存{savedCount}个词条");
                }
                else
                {
                    Console.WriteLine("未找到完整的定义列表结构");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从定义列表提取词条时出错: {ex.Message}");
        }
    }

    private async Task ExtractFromHeadersAndParagraphsAsync(HtmlNodeCollection headers, HtmlNodeCollection paragraphs)
    {
        try
        {
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

            if (headerIndexMap.Count > 0)
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
                            Category = "名词",
                            Url = _terminologyUrl,
                            Contents = new List<DictionaryContent>
                            {
                                new()
                                {
                                    Source = "《中医名词词典》",
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

                Console.WriteLine($"标题-段落关联提取完成，成功保存{savedCount}个词条");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从标题和段落提取词条时出错: {ex.Message}");
        }
    }

    private async Task ExtractFromTextContentAsync(string text)
    {
        try
        {
            // 移除HTML标签
            text = MyRegex().Replace(text, "");

            // 清理空白字符
            text = Regex.Replace(text, @"\s+", " ").Trim();

            Console.WriteLine("使用文本模式提取词条");
            Console.WriteLine($"文本总长度: {text.Length} 字符");

            // 常见的词条模式
            var patterns = new List<(string name, string pattern)>
            {
                ("模式1 【词条】定义", "【([^】]+)】([^【]+)"),
                ("模式2 词条：定义", @"([\u4e00-\u9fa5]{1,10})[：:]\s*(.+?)(?=[\u4e00-\u9fa5]{1,10}[：:]|\Z)"),
                ("模式3 定义列表", @"^([\u4e00-\u9fa5]+)\s+(.+?)$")
            };

            var savedCount = 0;

            foreach (var (name, pattern) in patterns)
            {
                Console.WriteLine($"尝试{name}...");
                var matches = Regex.Matches(text, pattern, RegexOptions.Multiline);

                Console.WriteLine($"  找到{matches.Count}个可能的匹配项");

                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        var term = match.Groups[1].Value.Trim();
                        var definition = match.Groups[2].Value.Trim();

                        if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(definition) && term.Length <= 20)
                        {
                            var entry = new DictionaryEntry
                            {
                                Term = term,
                                Category = "名词",
                                Url = _terminologyUrl,
                                Contents = new List<DictionaryContent>
                                {
                                    new()
                                    {
                                        Source = "《中医名词词典》",
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
                }
            }

            Console.WriteLine($"文本模式提取完成，成功保存{savedCount}个词条");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"从文本内容提取词条时出错: {ex.Message}");
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

            foreach (var relatedTerm in from pattern in relatedTermPatterns
                                        select pattern.Matches(definition) into matches
                                        from Match match in matches
                                        select match.Groups[1].Value.Trim() into relatedTerm
                                        where !string.IsNullOrEmpty(relatedTerm) &&
                                              relatedTerm != entry.Term &&
                                              relatedTerm.Length <= 20 &&  // 避免匹配过长文本
                                              !relatedTerm.Contains("《") && !relatedTerm.Contains("》")
                                        select relatedTerm)
            {
                relatedTerms.Add(relatedTerm);
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
        _httpClient?.Dispose();
        _repository?.Dispose();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex MyRegex();
}