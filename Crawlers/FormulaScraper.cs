using Crawlers.Models;
using Crawlers.Repository;
using HtmlAgilityPack;
using System.Net;

namespace Crawlers;

public partial class FormulaScraper
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://www.zysj.com.cn";
    private const string StartUrl = "https://www.zysj.com.cn/zhongyaofang/index.html";
    private readonly Random _random = new();
    private readonly FormulaRepository _repository;

    public FormulaScraper()
    {
        // 配置HttpClient以处理SSL问题
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            // 在正式环境中应避免这样做，但在爬虫场景可以尝试绕过SSL验证
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
        _repository = new FormulaRepository();
    }

    public async Task StartScrapingAsync()
    {
        Console.WriteLine("方剂爬虫程序启动...");
        Console.WriteLine("开始爬取拼音分类下的方剂...");

        try
        {
            // 爬取首页
            var indexHtml = await RequestWithRetryAsync(StartUrl);
            var indexDoc = new HtmlDocument();
            indexDoc.LoadHtml(indexHtml);

            // 查找拼音分类链接（A-Z字母开头的链接）
            var pinyinLinks = new Dictionary<string, string>();

            // 查找导航栏中的拼音分类
            var allLinks = indexDoc.DocumentNode.SelectNodes("//a[@href]");
            if (allLinks != null)
            {
                foreach (var link in allLinks)
                {
                    var href = link.GetAttributeValue("href", "");
                    var text = link.InnerText.Trim();

                    // 只查找A-Z字母的链接
                    if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(href) ||
                        text.Length != 1 || !((text[0] >= 0x41 && text[0] <= 0x5A) ||
                                              (text[0] >= 0x61 && text[0] <= 0x7A)) ||
                        (!href.Contains("index__") && !href.Contains(".html"))) continue;

                    var letter = text.ToUpper();
                    if (!pinyinLinks.ContainsKey(letter))
                    {
                        pinyinLinks.Add(letter, href);
                        Console.WriteLine($"找到拼音分类: {letter} - {href}");
                    }
                }
            }

            // 如果没找到拼音分类链接，尝试直接构造链接
            if (pinyinLinks.Count == 0)
            {
                Console.WriteLine("未在页面上找到拼音分类链接，尝试直接访问拼音页面...");

                // A-Z字母（排除I、U、V因为中文拼音中没有这些开头的字）
                string[] letters =
                [
                    "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M",
                    "N", "O", "P", "Q", "R", "S", "T", "W", "X", "Y", "Z"
                ];

                for (var i = 0; i < letters.Length; i++)
                {
                    pinyinLinks.Add(letters[i], $"/zhongyaofang/index__{i + 1}.html");
                }
            }

            // 按字母顺序爬取各个拼音分类
            foreach (var pinyinLink in pinyinLinks.OrderBy(x => x.Key))
            {
                var letter = pinyinLink.Key;
                var url = pinyinLink.Value;

                // 跳过锚点链接（以#开头的链接）
                if (url.StartsWith("#"))
                {
                    continue;
                }

                if (!url.StartsWith("http"))
                {
                    url = BaseUrl + url;
                }

                Console.WriteLine($"爬取{letter}开头的方剂...");
                await ScrapePinyinCategoryAsync(url, letter);

                // 随机等待1-3秒，避免请求过于频繁
                var delay = _random.Next(1000, 3000);
                await Task.Delay(delay);
            }

            Console.WriteLine("爬取完成！");
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

        // 不应该到达这里，但编译器要求返回值
        throw new Exception("请求失败，已达到最大重试次数");
    }

    private async Task ScrapePinyinCategoryAsync(string categoryUrl, string letter)
    {
        try
        {
            Console.WriteLine($"  正在访问: {categoryUrl}");
            var categoryHtml = await RequestWithRetryAsync(categoryUrl);

            var categoryDoc = new HtmlDocument();
            categoryDoc.LoadHtml(categoryHtml);

            // 尝试多种选择器找到方剂链接
            var formulaLinks = categoryDoc.DocumentNode.SelectNodes("//div[@id='list-content']//li/a");

            if (formulaLinks == null || formulaLinks.Count == 0)
            {
                Console.WriteLine("  未找到方剂链接，尝试其他选择器...");

                formulaLinks = categoryDoc.DocumentNode.SelectNodes("//ul[@class='drug_materials']/li/a") ??
                               categoryDoc.DocumentNode.SelectNodes("//div[contains(@class,'list')]//li/a");
            }

            if (formulaLinks != null && formulaLinks.Count != 0)
            {
                Console.WriteLine($"  找到{formulaLinks.Count}个{letter}开头的方剂");

                // 按字母顺序排序方剂列表
                var formulaList = formulaLinks
                    .Select(link => new
                    {
                        Name = link.InnerText.Trim(),
                        Url = link.GetAttributeValue("href", "")
                    })
                    .Where(m => !string.IsNullOrEmpty(m.Url) && !string.IsNullOrEmpty(m.Name))
                    .OrderBy(m => m.Name)
                    .ToList();

                var processedCount = 0;
                foreach (var formula in formulaList)
                {
                    var formulaUrl = formula.Url;
                    if (!formulaUrl.StartsWith("http"))
                    {
                        formulaUrl = BaseUrl + formulaUrl;
                    }

                    processedCount++;
                    Console.WriteLine($"  [{processedCount}/{formulaList.Count}] 爬取方剂: {formula.Name}");

                    try
                    {
                        await ScrapeFormulaDetailsAsync(formulaUrl, formula.Name);

                        // 随机等待0.5-2秒，避免请求过于频繁
                        var delay = _random.Next(500, 2000);
                        await Task.Delay(delay);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  爬取方剂 {formula.Name} 失败: {ex.Message}");

                        // 记录失败的方剂，以便后续重试
                        await File.AppendAllTextAsync("failed_formulas.txt", $"{formula.Name}\t{formulaUrl}\r\n");

                        // 失败后增加延迟
                        await Task.Delay(5000);
                    }
                }
            }
            else
            {
                Console.WriteLine($"  未找到{letter}开头的方剂");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  爬取{letter}分类时出错: {ex.Message}");
        }
    }

    private async Task ScrapeFormulaDetailsAsync(string formulaUrl, string formulaName)
    {
        try
        {
            Console.WriteLine($"    访问方剂详情页: {formulaUrl}");
            var formulaHtml = await RequestWithRetryAsync(formulaUrl);

            var formulaDoc = new HtmlDocument();
            formulaDoc.LoadHtml(formulaHtml);

            var formula = new ChineseFormula
            {
                Name = formulaName,
                Url = formulaUrl,
                DataSources = []
            };

            // 查找id为"content"的div元素
            var contentNode = formulaDoc.DocumentNode.SelectSingleNode("//div[@id='content']");

            if (contentNode != null)
            {
                Console.WriteLine("    找到内容区域...");

                // 查找所有section
                var sections = contentNode.SelectNodes(".//div[contains(@class,'section')]");

                if (sections is { Count: > 0 })
                {
                    Console.WriteLine($"    找到{sections.Count}个数据来源");

                    // 遍历每个section获取数据
                    foreach (var section in sections)
                    {
                        var dataSource = new FormulaDataSource();

                        // 提取h2标题（如：《中药方典》：xxx方）
                        var titleNode = section.SelectSingleNode(".//h2");
                        if (titleNode != null)
                        {
                            dataSource.Title = titleNode.InnerText.Trim();
                            Console.WriteLine($"    数据来源: {dataSource.Title}");
                        }

                        // 提取所有项目
                        ExtractAllItems(section, dataSource);

                        // 添加到方剂数据源列表
                        formula.DataSources.Add(dataSource);
                    }
                }
                else
                {
                    Console.WriteLine("    未找到数据section，尝试直接查找item元素");

                    // 直接在content下查找item元素
                    var dataSource = new FormulaDataSource { Title = "默认数据源" };
                    ExtractAllItems(contentNode, dataSource);
                    formula.DataSources.Add(dataSource);
                }
            }
            else
            {
                Console.WriteLine("    未找到内容区域，尝试从整个页面提取...");

                // 从整个页面提取
                var bodyText = formulaDoc.DocumentNode.SelectSingleNode("//body").InnerText;
                var dataSource = new FormulaDataSource { Title = "页面提取" };

                // 尝试从文本中提取信息
                ExtractFromBodyText(bodyText, dataSource);
                formula.DataSources.Add(dataSource);
            }

            // 在控制台显示方剂信息
            DisplayFormulaInfo(formula);

            // 存储方剂信息
            if (formula.DataSources.Count > 0)
            {
                // 设置拼音和首字母
                formula.PinYin = GetPinyin(formula.Name);
                formula.FirstLetter = formula.PinYin[..1].ToUpper();

                // 保存到数据仓库
                var saved = await _repository.SaveFormulaAsync(formula);
                if (saved)
                {
                    Console.WriteLine($"方剂 {formula.Name} 已成功保存");
                }
            }
        }
        catch (Exception ex)
        {
            // 重新抛出异常，由上层处理
            throw new Exception($"详情获取失败: {ex.Message}", ex);
        }
    }

    private void ExtractAllItems(HtmlNode sectionNode, FormulaDataSource dataSource)
    {
        // 查找所有item元素
        var itemNodes = sectionNode.SelectNodes(".//div[contains(@class,'item')]");

        if (itemNodes == null) return;
        foreach (var itemNode in itemNodes)
        {
            // 获取项目名称
            var nameNode = itemNode.SelectSingleNode(".//div[@class='item-name']");
            var contentNode = itemNode.SelectSingleNode(".//div[@class='item-content']");

            if (nameNode == null || contentNode == null) continue;
            var name = nameNode.InnerText.Trim();
            var content = contentNode.InnerHtml
                .Replace("<p>", "")
                .Replace("</p>", "\n")
                .Replace("<br>", "\n")
                .Replace("<br/>", "\n")
                .Replace("<br />", "\n");

            // 移除其他HTML标签
            content = MyRegex().Replace(content, "");
            content = content.Trim();

            // 添加到数据源的项目集合
            dataSource.Items.Add(new FormulaItem { Name = name, Content = content });

            // 根据不同项目名称，设置常用属性
            SetCommonPropertyByName(dataSource, name, content);
        }
    }

    private static void SetCommonPropertyByName(FormulaDataSource dataSource, string name, string content)
    {
        // 根据项目名称设置常用属性
        switch (name)
        {
            case "别名":
            case "异名":
                dataSource.Aliases = content;
                break;
            case "出处":
            case "来源":
                dataSource.Source = content;
                break;
            case "组成":
            case "处方":
                dataSource.Composition = content;
                break;
            case "制法":
            case "制备方法":
                dataSource.Preparation = content;
                break;
            case "功效":
            case "主治":
            case "功能主治":
                dataSource.Indications = content;
                break;
            case "用法用量":
                dataSource.Usage = content;
                break;
            case "方解":
            case "方义":
                dataSource.Theory = content;
                break;
            case "临床应用":
            case "现代应用":
                dataSource.Application = content;
                break;
            case "备注":
            case "注解":
            case "加减":
            case "临证加减":
            case "方论":
                // 其他重要属性
                dataSource.Items.FirstOrDefault(i => i.Name == name)!.IsImportant = true;
                break;
        }
    }

    private void ExtractFromBodyText(string bodyText, FormulaDataSource dataSource)
    {
        // 尝试从页面文本中提取常见属性
        var commonPatterns = new Dictionary<string, string[]> {
            { "别名", ["别名：", "别名:", "异名：", "异名:"] },
            { "出处", ["出处：", "出处:", "来源：", "来源:"] },
            { "组成", ["组成：", "组成:", "处方：", "处方:"] },
            { "制法", ["制法：", "制法:", "制备方法：", "制备方法:"] },
            { "功效", ["功效：", "功效:", "功能：", "功能:"] },
            { "主治", ["主治：", "主治:", "功能主治：", "功能主治:"] },
            { "用法用量", ["用法用量：", "用法用量:", "用法：", "用法:"] },
            { "方解", ["方解：", "方解:", "方义：", "方义:"] }
        };

        foreach (var pattern in commonPatterns)
        {
            var content = ExtractFromText(bodyText, pattern.Value, ["\n", "。", "；"]);
            if (!string.IsNullOrEmpty(content))
            {
                dataSource.Items.Add(new FormulaItem { Name = pattern.Key, Content = content });
                SetCommonPropertyByName(dataSource, pattern.Key, content);
            }
        }
    }

    private static string ExtractFromText(string text, string[] startMarkers, string[] endMarkers)
    {
        foreach (var marker in startMarkers)
        {
            var startIndex = text.IndexOf(marker, StringComparison.Ordinal);
            if (startIndex < 0) continue;
            startIndex += marker.Length;
            var endIndex = text.Length;

            foreach (var endMarker in endMarkers)
            {
                var tempIndex = text.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
                if (tempIndex >= 0 && tempIndex < endIndex)
                {
                    endIndex = tempIndex;
                }
            }

            return text.Substring(startIndex, endIndex - startIndex).Trim();
        }

        return string.Empty;
    }

    private void DisplayFormulaInfo(ChineseFormula formula)
    {
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"【方剂名】{formula.Name}");
        Console.WriteLine($"【网址】{formula.Url}");
        Console.WriteLine(new string('-', 80));

        var sourceCount = 1;
        foreach (var source in formula.DataSources)
        {
            Console.WriteLine($"[数据来源 {sourceCount}] {source.Title}");
            Console.WriteLine(new string('-', 60));

            // 先显示常用属性
            if (!string.IsNullOrEmpty(source.Aliases))
                Console.WriteLine($"【别名】{source.Aliases}");

            if (!string.IsNullOrEmpty(source.Source))
                Console.WriteLine($"【出处】{source.Source}");

            if (!string.IsNullOrEmpty(source.Composition))
                Console.WriteLine($"【组成】{source.Composition}");

            if (!string.IsNullOrEmpty(source.Preparation))
                Console.WriteLine($"【制法】{source.Preparation}");

            if (!string.IsNullOrEmpty(source.Indications))
                Console.WriteLine($"【主治】{source.Indications}");

            if (!string.IsNullOrEmpty(source.Usage))
                Console.WriteLine($"【用法用量】{source.Usage}");

            if (!string.IsNullOrEmpty(source.Theory))
                Console.WriteLine($"【方解】{source.Theory}");

            if (!string.IsNullOrEmpty(source.Application))
                Console.WriteLine($"【临床应用】{source.Application}");

            // 显示其他重要属性
            var importantItems = source.Items.Where(i =>
                i.IsImportant &&
                i.Name != "别名" &&
                i.Name != "出处" &&
                i.Name != "组成" &&
                i.Name != "处方" &&
                i.Name != "制法" &&
                i.Name != "主治" &&
                i.Name != "功能主治" &&
                i.Name != "用法用量" &&
                i.Name != "方解" &&
                i.Name != "临床应用");

            foreach (var item in importantItems)
            {
                Console.WriteLine($"【{item.Name}】{item.Content}");
            }

            // 显示其余所有属性
            Console.WriteLine("\n[其他信息]");
            var otherItems = source.Items.Where(i =>
                !i.IsImportant &&
                i.Name != "别名" &&
                i.Name != "出处" &&
                i.Name != "组成" &&
                i.Name != "处方" &&
                i.Name != "制法" &&
                i.Name != "主治" &&
                i.Name != "功能主治" &&
                i.Name != "用法用量" &&
                i.Name != "方解" &&
                i.Name != "临床应用");

            foreach (var item in otherItems)
            {
                Console.WriteLine($"  • {item.Name}：{item.Content}");
            }

            sourceCount++;
            Console.WriteLine(new string('-', 60));
        }

        Console.WriteLine(new string('=', 80));
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
            // 如果转换失败，返回原名首字母
            if (!string.IsNullOrEmpty(chineseName))
            {
                return NPinyin.Pinyin.GetPinyin(chineseName) + chineseName[..1];
            }
            return "A";
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("<[^>]+>")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}