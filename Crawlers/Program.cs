using Crawlers.Models;
using Crawlers.Repository;
using Newtonsoft.Json;
using SQLitePCL;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。

namespace Crawlers;

[SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
internal class Program
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 初始化SQLite
        Batteries.Init();

        // 检查是否需要删除旧数据库文件
        CheckAndCleanOldDatabases();

        try
        {
            // 提示用户选择操作
            Console.WriteLine("中医药专业数据系统 - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("============= 中药材 =============");
            Console.WriteLine("1. 爬取中药材数据");
            Console.WriteLine("2. 查询中药材数据");
            Console.WriteLine("============= 方剂 =============");
            Console.WriteLine("3. 爬取方剂数据");
            Console.WriteLine("4. 查询方剂数据");
            // 在Main方法中的菜单项添加选项
            Console.WriteLine("============= 中医词典 =============");
            Console.WriteLine("5. 爬取中医词典数据");
            Console.WriteLine("6. 查询中医词典数据");
            Console.WriteLine("============= 名词词典 =============");
            Console.WriteLine("7. 爬取中医名词词典数据");
            Console.WriteLine("8. 查询中医名词词典数据");
            Console.WriteLine("============= 古籍书籍 =============");
            Console.WriteLine("9. 爬取中医古籍书籍");
            Console.WriteLine("10. 查询中医古籍书籍");
            Console.WriteLine("============= 杂集文章 =============");  // 新增
            Console.WriteLine("11. 爬取中医杂集文章");              // 新增
            Console.WriteLine("12. 配置多线程爬虫参数");           // 新增
            Console.WriteLine("13. 查询中医杂集文章");             // 新增
            Console.WriteLine("============= 工具 =============");
            Console.WriteLine("14. 查看数据统计信息");             // 编号调整
            Console.WriteLine("15. 清理临时数据库文件");           // 编号调整
            Console.Write("请选择操作 (0-15): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "0":
                    return;
                case "1":
                    // 爬取中药材数据
                    var scraper = new MedicineScraper();
                    await scraper.StartScrapingAsync();
                    break;
                case "2":
                    // 查询中药材数据
                    using (var repository = new MedicineRepository())
                    {
                        await QueryMedicineData(repository);
                    }
                    break;
                case "3":
                    // 爬取方剂数据
                    var formulaScraper = new FormulaScraper();
                    await formulaScraper.StartScrapingAsync();
                    break;
                case "4":
                    // 查询方剂数据
                    using (var repository = new FormulaRepository())
                    {
                        await QueryFormulaData(repository);
                    }
                    break;
                // 在switch语句中添加新的case
                case "5":
                    // 爬取中医名词词典数据
                    using (var terminologyScraper = new TerminologyScraper())
                    {
                        await terminologyScraper.StartScrapingAsync();
                    }
                    break;
                case "6":
                    // 查询中医名词词典数据
                    using (var repository = new DictionaryRepository(
                               liteDbPath: "TCMTerminology.litedb",
                               sqliteDbPath: "TCMTerminology.db",
                               jsonBasePath: "TCMTerminologyData"))
                    {
                        await QueryDictionaryData(repository, "中医名词词典");
                    }
                    break;
                case "7":
                    // 显示各种统计信息
                    await ShowAllStatisticsAsync();
                    break;
                case "8":
                    // 清理临时数据库文件
                    CleanupTempDatabases();
                    break;
                // 在switch语句中添加新的case
                case "9":
                    // 爬取中医古籍书籍
                    using (var bookScraper = new BookScraper())
                    {
                        await bookScraper.StartScrapingAsync();
                    }
                    break;
                case "10":
                    // 查询中医古籍书籍
                    await QueryBookData();
                    break;
                // 在switch语句中添加新的case
                case "11":
                    // 爬取中医杂集文章
                    await RunArticleScraperAsync();
                    break;
                case "12":
                    // 配置多线程爬虫参数
                    ConfigureArticleScraper();
                    break;
                case "13":
                    // 查询中医杂集文章
                    await QueryArticleData();
                    break;
                default:
                    Console.WriteLine("无效选择");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"程序出错: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
    private static void CheckAndCleanOldDatabases()
    {
        // 检测是否有临时数据库文件
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        var tempFiles = Directory.GetFiles(directory, "ChineseMedicine_*.litedb");

        if (tempFiles.Length <= 0) return;
        Console.WriteLine($"发现 {tempFiles.Length} 个临时数据库文件");
        Console.Write("是否清理这些临时文件? (Y/N): ");
        var response = Console.ReadLine();

        if (response?.ToUpper() == "Y")
        {
            CleanupTempDatabases();
        }
    }

    private static void CleanupTempDatabases()
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        var tempFiles = Directory.GetFiles(directory, "ChineseMedicine_*.litedb");

        if (tempFiles.Length == 0)
        {
            Console.WriteLine("没有找到临时数据库文件");
            return;
        }

        var successCount = 0;
        foreach (var file in tempFiles)
        {
            try
            {
                File.Delete(file);
                successCount++;
                Console.WriteLine($"已删除: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"无法删除 {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine($"清理完成: 删除了 {successCount}/{tempFiles.Length} 个临时文件");
    }
    // 添加查询书籍数据的方法
    private static async Task QueryBookData()
    {
        using (var repository = new BookRepository())
        {
            Console.WriteLine("\n中医古籍书籍查询");
            Console.WriteLine("1. 按分类查看书籍");
            Console.WriteLine("2. 查看所有书籍");
            Console.WriteLine("3. 通过书名查看详情");
            Console.Write("请选择查询方式 (1-3): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await QueryBooksByCategory(repository);
                    break;
                case "2":
                    await ViewAllBooks(repository);
                    break;
                case "3":
                    await ViewBookByTitle(repository);
                    break;
                default:
                    Console.WriteLine("无效选择");
                    break;
            }
        }
    }

    private static async Task QueryBooksByCategory(BookRepository repository)
    {
        Console.WriteLine("\n请选择书籍分类：");
        Console.WriteLine("1. 经论");
        Console.WriteLine("2. 伤寒、金匮");
        Console.WriteLine("3. 诊治");
        Console.WriteLine("4. 本草");
        Console.WriteLine("5. 方言");
        Console.WriteLine("6. 内科");
        Console.WriteLine("7. 妇科");
        Console.WriteLine("8. 儿科");
        Console.WriteLine("9. 外科");
        Console.WriteLine("10. 五官");
        Console.WriteLine("11. 针灸");
        Console.WriteLine("12. 医论");
        Console.WriteLine("13. 医案");
        Console.WriteLine("14. 综合");
        Console.WriteLine("15. 养生");
        Console.WriteLine("16. 其他");

        Console.Write("请输入分类编号(1-16): ");
        var input = Console.ReadLine();

        if (int.TryParse(input, out var num) && num is >= 1 and <= 16)
        {
            string[] categories = { "经论", "伤寒、金匮", "诊治", "本草", "方言", "内科",
                               "妇科", "儿科", "外科", "五官", "针灸", "医论",
                               "医案", "综合", "养生", "其他" };

            var category = categories[num - 1];
            Console.WriteLine($"\n查询分类: {category}");

            var books = await repository.GetBooksByCategoryAsync(category);

            if (books.Count > 0)
            {
                DisplayBookList(books);
                await ViewBookDetails(repository);
            }
            else
            {
                Console.WriteLine($"未找到分类为 \"{category}\" 的书籍");
            }
        }
        else
        {
            Console.WriteLine("无效的分类编号");
        }
    }

    private static async Task ViewAllBooks(BookRepository repository)
    {
        var books = await repository.GetAllBooksAsync();

        if (books.Count > 0)
        {
            DisplayBookList(books);
            await ViewBookDetails(repository);
        }
        else
        {
            Console.WriteLine("未找到任何书籍");
        }
    }

    private static async Task ViewBookByTitle(BookRepository repository)
    {
        Console.Write("\n请输入书名: ");
        var title = Console.ReadLine();

        if (string.IsNullOrEmpty(title))
        {
            Console.WriteLine("书名不能为空");
            return;
        }

        var book = await repository.GetBookByTitleAsync(title);

        if (book != null)
        {
            DisplayBookDetails(book);
        }
        else
        {
            Console.WriteLine($"未找到 \"{title}\" 这本书");
        }
    }

    private static void DisplayBookList(List<BookInfo> books)
    {
        Console.WriteLine($"\n共找到 {books.Count} 本书籍:");
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"{"序号",-5}{"书名",-30}{"作者",-15}{"朝代",-10}{"分类",-10}");
        Console.WriteLine(new string('-', 80));

        for (var i = 0; i < books.Count; i++)
        {
            var book = books[i];
            Console.WriteLine($"{i + 1,-5}{book.Title,-30}{book.Author,-15}{book.Dynasty,-10}{book.Category,-10}");
        }

        Console.WriteLine(new string('=', 80));
    }

    private static async Task ViewBookDetails(BookRepository repository)
    {
        Console.Write("\n输入书籍序号或书名查看详情(或按回车返回): ");
        var input = Console.ReadLine();

        if (!string.IsNullOrEmpty(input))
        {
            if (int.TryParse(input, out var num) && num > 0)
            {
                // 如果输入的是数字，假设是序号
                // 注意：这里无法实现直接通过序号获取，因为我们没有保存完整的书籍列表
                // 所以需要优化逻辑或提示用户输入书名
                Console.WriteLine("请输入完整书名以查看详情");
                var title = Console.ReadLine();
                if (!string.IsNullOrEmpty(title))
                {
                    var book = await repository.GetBookByTitleAsync(title);
                    if (book != null)
                    {
                        DisplayBookDetails(book);
                    }
                    else
                    {
                        Console.WriteLine($"未找到 \"{title}\" 这本书");
                    }
                }
            }
            else
            {
                // 如果输入的是文本，假设是书名
                var book = await repository.GetBookByTitleAsync(input);
                if (book != null)
                {
                    DisplayBookDetails(book);
                }
                else
                {
                    Console.WriteLine($"未找到 \"{input}\" 这本书");
                }
            }
        }
    }

    private static void DisplayBookDetails(Book book)
    {
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"《{book.Title}》");
        Console.WriteLine(new string('-', 60));

        if (!string.IsNullOrEmpty(book.Author))
            Console.WriteLine($"作者: {book.Author}");

        if (!string.IsNullOrEmpty(book.Dynasty))
            Console.WriteLine($"朝代: {book.Dynasty}");

        if (!string.IsNullOrEmpty(book.Year))
            Console.WriteLine($"年份: {book.Year}");

        Console.WriteLine($"分类: {book.Category}");
        Console.WriteLine($"章节数: {book.Chapters.Count}");
        Console.WriteLine(new string('-', 60));

        // 显示章节列表
        Console.WriteLine("章节目录:");
        for (var i = 0; i < book.Chapters.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {book.Chapters[i].Title} ({book.Chapters[i].Sections.Count} 节)");
        }

        Console.WriteLine(new string('-', 60));
        Console.Write("输入章节序号查看内容(或按回车返回): ");
        var input = Console.ReadLine();

        if (int.TryParse(input, out var chapterNum) && chapterNum > 0 && chapterNum <= book.Chapters.Count)
        {
            DisplayChapterContent(book.Chapters[chapterNum - 1]);
        }
    }

    private static void DisplayChapterContent(BookChapter chapter)
    {
        Console.Clear();
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"【{chapter.Title}】");
        Console.WriteLine(new string('-', 60));

        // 显示章节内容
        if (!string.IsNullOrEmpty(chapter.Content))
        {
            Console.WriteLine(chapter.Content);
            Console.WriteLine();
        }

        // 显示小节内容
        foreach (var section in chapter.Sections)
        {
            Console.WriteLine($"【{section.Title}】");
            Console.WriteLine(section.Content);
            Console.WriteLine();
        }

        Console.WriteLine(new string('=', 80));
        Console.WriteLine("按任意键返回...");
        Console.ReadKey();
    }
    private static async Task QueryMedicineData(MedicineRepository repository)
    {
        Console.Write("请输入拼音首字母查询(A-Z): ");
        var letter = Console.ReadLine()?.ToUpper();

        if (string.IsNullOrEmpty(letter) || letter.Length != 1)
        {
            Console.WriteLine("无效的首字母");
            return;
        }

        var medicines = await repository.GetMedicinesByFirstLetterAsync(letter);

        Console.WriteLine($"找到 {medicines.Count} 种中药");
        foreach (var medicine in medicines)
        {
            Console.WriteLine($"- {medicine.Name} ({medicine.PinYin})");
        }

        Console.Write("输入药名查看详情(或按回车返回): ");
        var name = Console.ReadLine();

        if (!string.IsNullOrEmpty(name))
        {
            var detail = await repository.GetMedicineByNameAsync(name);
            if (detail != null)
            {
                // 显示详情
                Console.WriteLine(new string('=', 80));
                Console.WriteLine($"【药名】{detail.Name}");
                Console.WriteLine($"【拼音】{detail.PinYin}");

                var sourceIndex = 1;
                foreach (var source in detail.DataSources)
                {
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine($"[数据来源 {sourceIndex}] {source.Title}");

                    // 1. 先显示常用属性
                    if (!string.IsNullOrEmpty(source.Aliases))
                        Console.WriteLine($"【别名】{source.Aliases}");

                    if (!string.IsNullOrEmpty(source.Source))
                        Console.WriteLine($"【来源】{source.Source}");

                    if (!string.IsNullOrEmpty(source.Properties))
                        Console.WriteLine($"【性味】{source.Properties}");

                    if (!string.IsNullOrEmpty(source.Meridian))
                        Console.WriteLine($"【归经】{source.Meridian}");

                    if (!string.IsNullOrEmpty(source.Effects))
                        Console.WriteLine($"【功效】{source.Effects}");

                    if (!string.IsNullOrEmpty(source.Indications))
                        Console.WriteLine($"【主治】{source.Indications}");

                    if (!string.IsNullOrEmpty(source.Usage))
                        Console.WriteLine($"【用法用量】{source.Usage}");

                    // 2. 显示重要属性
                    var importantItems = source.Items.Where(i =>
                        i.IsImportant &&
                        i.Name != "别名" &&
                        i.Name != "来源" &&
                        i.Name != "性味" &&
                        i.Name != "归经" &&
                        i.Name != "功效" &&
                        i.Name != "功能主治" &&
                        i.Name != "主治" &&
                        i.Name != "用法用量");

                    foreach (var item in importantItems)
                    {
                        Console.WriteLine($"【{item.Name}】{item.Content}");
                    }

                    // 3. 显示其他所有属性
                    Console.WriteLine("\n【其他信息】");
                    var otherItems = source.Items.Where(i =>
                        !i.IsImportant &&
                        i.Name != "别名" &&
                        i.Name != "来源" &&
                        i.Name != "性味" &&
                        i.Name != "归经" &&
                        i.Name != "功效" &&
                        i.Name != "功能主治" &&
                        i.Name != "主治" &&
                        i.Name != "用法用量");

                    var medicineItems = otherItems.ToList();
                    if (medicineItems.Count != 0)
                    {
                        foreach (var item in medicineItems)
                        {
                            Console.WriteLine($"  • {item.Name}：{item.Content}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  (无其他信息)");
                    }

                    sourceIndex++;
                }
            }
            else
            {
                Console.WriteLine("未找到该药材");
            }
        }
    }

    private static async Task QueryDictionaryData(DictionaryRepository repository, string dictionaryType = "中医词典")
    {
        Console.WriteLine($"\n{dictionaryType}查询");
        Console.WriteLine("1. 按首字母查询");
        Console.WriteLine("2. 按关键词搜索");
        Console.Write("请选择查询方式 (1-2): ");

        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await QueryDictionaryByLetter(repository, dictionaryType);
                break;
            case "2":
                await SearchDictionary(repository, dictionaryType);
                break;
            default:
                Console.WriteLine("无效选择");
                break;
        }
    }
    private static async Task QueryDictionaryByLetter(DictionaryRepository repository, string dictionaryType = "中医词典")
    {
        Console.Write($"请输入{dictionaryType}拼音首字母查询(A-Z): ");
        var letter = Console.ReadLine()?.ToUpper();

        if (string.IsNullOrEmpty(letter) || letter.Length != 1)
        {
            Console.WriteLine("无效的首字母");
            return;
        }

        var entries = await repository.GetEntriesByFirstLetterAsync(letter);

        Console.WriteLine($"{dictionaryType}找到 {entries.Count} 个词条");

        if (entries.Count > 0)
        {
            // 显示词条列表
            DisplayEntryList(entries);

            // 查看详情
            await ViewEntryDetails(repository, dictionaryType);
        }
    }

    private static async Task SearchDictionary(DictionaryRepository repository, string dictionaryType = "中医词典")
    {
        Console.Write($"请输入{dictionaryType}搜索关键词: ");
        var keyword = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            Console.WriteLine("关键词不能为空");
            return;
        }

        var entries = await repository.SearchEntriesAsync(keyword);

        Console.WriteLine($"{dictionaryType}找到 {entries.Count} 个相关词条");

        if (entries.Count > 0)
        {
            // 显示词条列表
            DisplayEntryList(entries);

            // 查看详情
            await ViewEntryDetails(repository, dictionaryType);
        }
    }

    // 同样需要更新ViewEntryDetails方法
    private static async Task ViewEntryDetails(DictionaryRepository repository, string dictionaryType = "中医词典")
    {
        Console.Write($"\n输入{dictionaryType}词条名查看详情(或按回车返回): ");
        var term = Console.ReadLine();

        if (!string.IsNullOrEmpty(term))
        {
            var entry = await repository.GetEntryByTermAsync(term);

            if (entry != null)
            {
                DisplayEntryDetails(entry, dictionaryType);
            }
            else
            {
                Console.WriteLine($"未找到{dictionaryType}该词条");
            }
        }
    }

    // 更新显示方法以包含词典类型
    private static void DisplayEntryDetails(DictionaryEntry entry, string dictionaryType = "中医词典")
    {
        Console.WriteLine(new string('=', 80));
        Console.WriteLine($"【{dictionaryType}词条】{entry.Term}");
        Console.WriteLine($"【拼音】{entry.PinYin}");

        if (!string.IsNullOrEmpty(entry.Category))
        {
            Console.WriteLine($"【分类】{entry.Category}");
        }

        var i = 1;
        foreach (var content in entry.Contents)
        {
            Console.WriteLine(new string('-', 60));

            if (entry.Contents.Count > 1)
            {
                Console.WriteLine($"[释义 {i}] {content.Source}");
            }

            Console.WriteLine(content.Definition);

            if (!string.IsNullOrEmpty(content.Reference))
            {
                Console.WriteLine($"\n参考文献: {content.Reference}");
            }

            if (content.RelatedTerms.Count > 0)
            {
                Console.WriteLine($"\n相关词条: {string.Join(", ", content.RelatedTerms)}");
            }

            i++;
        }

        Console.WriteLine(new string('=', 80));
    }

    /*
        private static Task BrowseDictionaryByCategory(DictionaryRepository repository)
        {
            var stats = repository.GetDictionaryStatistics();

            Console.WriteLine("\n词典分类：");

            // 显示所有分类及其词条数量
            var categories = stats.Where(s => s.Key.StartsWith("Cat_"))
                                 .Select(s => new
                                 {
                                     Category = s.Key.Substring(4),
                                     Count = s.Value
                                 })
                                 .OrderByDescending(c => c.Count)
                                 .ToList();

            for (var i = 0; i < categories.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {categories[i].Category} ({categories[i].Count}个词条)");
            }

            Console.Write("\n请选择分类编号: ");
            var choice = Console.ReadLine();

            if (int.TryParse(choice, out var index) && index >= 1 && index <= categories.Count)
            {
                var category = categories[index - 1].Category;

                // 这里需要添加一个按分类查询的方法
                // 由于我们没有在DictionaryRepository中实现这个方法，
                // 这里只是展示界面逻辑
                Console.WriteLine($"\n{category}分类的词条：");
                Console.WriteLine("该功能暂未实现，请使用关键词搜索。");
            }
            else
            {
                Console.WriteLine("无效选择");
            }

            return Task.CompletedTask;
        }
    */

    private static void DisplayEntryList(List<DictionaryEntry> entries)
    {
        // 按首字母分组显示
        var groupedEntries = entries
            .GroupBy(e => e.FirstLetter)
            .OrderBy(g => g.Key);

        foreach (var group in groupedEntries)
        {
            Console.WriteLine($"\n[{group.Key}]");

            foreach (var entry in group.OrderBy(e => e.PinYin))
            {
                var brief = entry.Contents.Count > 0
                    ? TruncateText(entry.Contents[0].Definition, 30)
                    : "";

                Console.WriteLine($"  {entry.Term} ({entry.PinYin}) - {brief}");
            }
        }
    }

    /*
        private static async Task ViewEntryDetails(DictionaryRepository repository)
        {
            Console.Write("\n输入词条名查看详情(或按回车返回): ");
            var term = Console.ReadLine();

            if (!string.IsNullOrEmpty(term))
            {
                var entry = await repository.GetEntryByTermAsync(term);

                if (entry != null)
                {
                    DisplayEntryDetails(entry);
                }
                else
                {
                    Console.WriteLine("未找到该词条");
                }
            }
        }
    */

    /*
        private static void DisplayEntryDetails(DictionaryEntry entry)
        {
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"【词条】{entry.Term}");
            Console.WriteLine($"【拼音】{entry.PinYin}");
            Console.WriteLine($"【分类】{entry.Category}");

            var i = 1;
            foreach (var content in entry.Contents)
            {
                Console.WriteLine(new string('-', 60));

                if (entry.Contents.Count > 1)
                {
                    Console.WriteLine($"[释义 {i}] {content.Source}");
                }

                Console.WriteLine(content.Definition);

                if (!string.IsNullOrEmpty(content.Reference))
                {
                    Console.WriteLine($"\n参考文献: {content.Reference}");
                }

                if (content.RelatedTerms.Count > 0)
                {
                    Console.WriteLine($"\n相关词条: {string.Join(", ", content.RelatedTerms)}");
                }

                i++;
            }

            Console.WriteLine(new string('=', 80));
        }
    */

    private static Task ShowAllStatisticsAsync()
    {
        Console.WriteLine("\n数据统计信息");
        Console.WriteLine("============= 中药材 =============");

        try
        {
            using var medicineRepo = new MedicineRepository();
            ShowMedicineStatistics(medicineRepo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取中药材统计信息失败: {ex.Message}");
        }

        Console.WriteLine("\n============= 方剂 =============");

        try
        {
            using var formulaRepo = new FormulaRepository();
            ShowFormulaStatistics(formulaRepo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取方剂统计信息失败: {ex.Message}");
        }

        Console.WriteLine("\n============= 词典 =============");

        try
        {
            using var dictRepo = new DictionaryRepository();
            ShowDictionaryStatistics(dictRepo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取词典统计信息失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static void ShowDictionaryStatistics(DictionaryRepository repository)
    {
        var stats = repository.GetDictionaryStatistics();

        Console.WriteLine($"总词条数: {stats.GetValueOrDefault("Total", 0)}");

        // 按首字母统计
        Console.WriteLine("\n按拼音首字母统计:");
        var letterStats = stats
            .Where(kv => kv.Key != "Total" && !kv.Key.StartsWith("Cat_"))
            .OrderBy(kv => kv.Key);

        foreach (var item in letterStats)
        {
            if (item.Value > 0)
            {
                Console.WriteLine($"{item.Key}: {item.Value}个");
            }
        }

        // 按分类统计
        Console.WriteLine("\n按分类统计:");
        var categoryStats = stats
            .Where(kv => kv.Key.StartsWith("Cat_"))
            .OrderByDescending(kv => kv.Value);

        foreach (var item in categoryStats)
        {
            Console.WriteLine($"{item.Key.Substring(4)}: {item.Value}个");
        }
    }

    // 辅助方法：截断文本
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
    }
    private static void ShowMedicineStatistics(MedicineRepository repository)
    {
        var stats = repository.GetMedicineStatistics();

        Console.WriteLine(new string('=', 40));
        Console.WriteLine("中药材数据统计");
        Console.WriteLine(new string('=', 40));

        Console.WriteLine($"总药材数: {stats["Total"]}");
        Console.WriteLine("\n按拼音首字母统计:");

        foreach (var item in stats.OrderBy(x => x.Key))
        {
            if (item.Key != "Total" && item.Value > 0)
            {
                Console.WriteLine($"{item.Key}: {item.Value}种");
            }
        }

        Console.WriteLine(new string('=', 40));
    }

    private static async Task QueryFormulaData(FormulaRepository repository)
    {
        Console.Write("请输入拼音首字母查询(A-Z): ");
        var letter = Console.ReadLine()?.ToUpper();

        if (string.IsNullOrEmpty(letter) || letter.Length != 1)
        {
            Console.WriteLine("无效的首字母");
            return;
        }

        var formulas = await repository.GetFormulasByFirstLetterAsync(letter);

        Console.WriteLine($"找到 {formulas.Count} 个方剂");
        foreach (var formula in formulas)
        {
            Console.WriteLine($"- {formula.Name} ({formula.PinYin})");
        }

        Console.Write("输入方剂名查看详情(或按回车返回): ");
        var name = Console.ReadLine();

        if (!string.IsNullOrEmpty(name))
        {
            var detail = await repository.GetFormulaByNameAsync(name);
            if (detail != null)
            {
                // 显示详情
                Console.WriteLine(new string('=', 80));
                Console.WriteLine($"【方剂名】{detail.Name}");
                Console.WriteLine($"【拼音】{detail.PinYin}");

                var sourceIndex = 1;
                foreach (var source in detail.DataSources)
                {
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine($"[数据来源 {sourceIndex}] {source.Title}");

                    // 1. 先显示常用属性
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

                    // 2. 显示重要属性
                    var importantItems = source.Items.Where(i =>
                        i.IsImportant &&
                        i.Name != "别名" &&
                        i.Name != "出处" &&
                        i.Name != "组成" &&
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

                    // 3. 显示其他所有属性
                    Console.WriteLine("\n【其他信息】");
                    var otherItems = source.Items.Where(i =>
                        !i.IsImportant &&
                        i.Name != "别名" &&
                        i.Name != "出处" &&
                        i.Name != "组成" &&
                        i.Name != "制法" &&
                        i.Name != "主治" &&
                        i.Name != "功能主治" &&
                        i.Name != "用法用量" &&
                        i.Name != "方解" &&
                        i.Name != "临床应用");

                    var formulaItems = otherItems as FormulaItem[] ?? otherItems.ToArray();
                    if (formulaItems.Length != 0)
                    {
                        foreach (var item in formulaItems)
                        {
                            Console.WriteLine($"  • {item.Name}：{item.Content}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  (无其他信息)");
                    }

                    sourceIndex++;
                }
            }
            else
            {
                Console.WriteLine("未找到该方剂");
            }
        }
    }

    private static void ShowFormulaStatistics(FormulaRepository repository)
    {
        var stats = repository.GetFormulaStatistics();

        Console.WriteLine(new string('=', 40));
        Console.WriteLine("方剂数据统计");
        Console.WriteLine(new string('=', 40));

        Console.WriteLine($"总方剂数: {stats["Total"]}");
        Console.WriteLine("\n按拼音首字母统计:");

        foreach (var item in stats.OrderBy(x => x.Key))
        {
            if (item.Key != "Total" && item.Value > 0)
            {
                Console.WriteLine($"{item.Key}: {item.Value}个");
            }
        }

        Console.WriteLine(new string('=', 40));
    }
    // 添加杂集爬虫相关方法
    private static async Task RunArticleScraperAsync()
    {
        Console.WriteLine("\n中医杂集多线程爬虫");

        // 使用存储的配置或默认配置
        var config = LoadArticleScraperConfig();

        Console.WriteLine($"线程数: {config.MaxConcurrency}");
        Console.WriteLine($"请求间隔(毫秒): {config.DelayBetweenRequests}");
        Console.WriteLine($"起始页: {config.StartPage}");
        Console.WriteLine($"最大页数: {config.MaxPages}");

        Console.Write("\n确认开始爬取? (Y/N): ");
        string confirm = Console.ReadLine()?.Trim().ToUpper();

        if (confirm == "Y")
        {
            using (var scraper = new ArticleScraper(
                maxConcurrency: config.MaxConcurrency,
                delayBetweenRequests: config.DelayBetweenRequests,
                startPage: config.StartPage,
                maxPages: config.MaxPages))
            {
                try
                {
                    // 设置控制台取消处理
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true; // 阻止进程退出
                        scraper.StopScraping();
                        Console.WriteLine("\n爬虫收到停止信号，正在清理资源...");
                    };

                    Console.WriteLine("\n爬虫已启动，按Ctrl+C可以随时停止...");
                    await scraper.StartScrapingAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"爬虫运行出错: {ex.Message}");
                }
            }
        }
    }

    private static void ConfigureArticleScraper()
    {
        Console.WriteLine("\n配置中医杂集爬虫参数");

        // 加载当前配置
        var config = LoadArticleScraperConfig();

        // 线程数
        Console.Write($"请输入并发线程数 (1-10) [当前: {config.MaxConcurrency}]: ");
        string input = Console.ReadLine();
        if (int.TryParse(input, out int concurrency) && concurrency >= 1 && concurrency <= 10)
        {
            config.MaxConcurrency = concurrency;
        }

        // 请求间隔
        Console.Write($"请输入请求间隔(毫秒, 建议>=1000) [当前: {config.DelayBetweenRequests}]: ");
        input = Console.ReadLine();
        if (int.TryParse(input, out int delay) && delay >= 100)
        {
            config.DelayBetweenRequests = delay;
        }

        // 起始页
        Console.Write($"请输入起始页码 [当前: {config.StartPage}]: ");
        input = Console.ReadLine();
        if (int.TryParse(input, out int startPage) && startPage >= 1)
        {
            config.StartPage = startPage;
        }

        // 最大页数
        Console.Write($"请输入最大爬取页数 (-1表示不限) [当前: {config.MaxPages}]: ");
        input = Console.ReadLine();
        if (int.TryParse(input, out int maxPages) && (maxPages == -1 || maxPages >= 1))
        {
            config.MaxPages = maxPages;
        }

        // 保存配置
        SaveArticleScraperConfig(config);

        Console.WriteLine("\n配置已保存!");
    }

    private static async Task QueryArticleData()
    {
        using (var repository = new ArticleRepository())
        {
            Console.WriteLine("\n中医杂集查询");
            Console.WriteLine("1. 按标签查询文章");
            Console.WriteLine("2. 查看统计信息");
            Console.Write("请选择查询方式 (1-2): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await QueryArticlesByTag(repository);
                    break;
                case "2":
                    await ShowArticleStatistics(repository);
                    break;
                default:
                    Console.WriteLine("无效选择");
                    break;
            }
        }
    }

    private static async Task QueryArticlesByTag(ArticleRepository repository)
    {
        Console.Write("\n请输入要查询的标签: ");
        string tag = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(tag))
        {
            Console.WriteLine("标签不能为空");
            return;
        }

        var articles = await repository.GetArticlesByTagAsync(tag);

        Console.WriteLine($"\n找到 {articles.Count} 篇关于 \"{tag}\" 的文章");

        if (articles.Count > 0)
        {
            for (int i = 0; i < articles.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {articles[i].Title}");
            }

            Console.Write("\n输入序号查看详情 (或按回车返回): ");
            string input = Console.ReadLine();

            if (int.TryParse(input, out int index) && index >= 1 && index <= articles.Count)
            {
                DisplayArticleDetails(articles[index - 1]);
            }
        }
    }

    private static async Task ShowArticleStatistics(ArticleRepository repository)
    {
        var stats = await repository.GetStatisticsAsync();

        Console.WriteLine("\n=== 中医杂集统计 ===");
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
    }

    private static void DisplayArticleDetails(Article article)
    {
        Console.Clear();
        Console.WriteLine(new string('=', 80));
        Console.WriteLine(article.Title);
        Console.WriteLine(new string('-', 40));

        if (!string.IsNullOrEmpty(article.Author))
            Console.WriteLine($"作者: {article.Author}");

        if (!string.IsNullOrEmpty(article.PublishDate))
            Console.WriteLine($"发布日期: {article.PublishDate}");

        if (article.Tags != null && article.Tags.Count > 0)
            Console.WriteLine($"标签: {string.Join(", ", article.Tags)}");

        Console.WriteLine(new string('-', 40));

        if (!string.IsNullOrEmpty(article.Summary))
        {
            Console.WriteLine("摘要:");
            Console.WriteLine(article.Summary);
            Console.WriteLine();
        }

        if (!string.IsNullOrEmpty(article.Content))
        {
            // 分段显示长内容
            string[] paragraphs = article.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < paragraphs.Length; i++)
            {
                if (i > 0 && i % 20 == 0)
                {
                    Console.WriteLine("\n--- 按任意键继续... ---");
                    Console.ReadKey(true);
                }

                Console.WriteLine(paragraphs[i]);
            }
        }
        else
        {
            Console.WriteLine("[无内容]");
        }

        Console.WriteLine(new string('=', 80));
        Console.WriteLine("按任意键返回...");
        Console.ReadKey();
    }

    // 爬虫配置类和相关方法
    private class ArticleScraperConfig
    {
        public int MaxConcurrency { get; set; } = 5;
        public int DelayBetweenRequests { get; set; } = 2000;
        public int StartPage { get; set; } = 1;
        public int MaxPages { get; set; } = -1;
    }

    private static ArticleScraperConfig LoadArticleScraperConfig()
    {
        string configPath = "ArticleScraperConfig.json";

        if (File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                return JsonConvert.DeserializeObject<ArticleScraperConfig>(json) ?? new ArticleScraperConfig();
            }
            catch
            {
                return new ArticleScraperConfig();
            }
        }

        return new ArticleScraperConfig();
    }

    private static void SaveArticleScraperConfig(ArticleScraperConfig config)
    {
        string configPath = "ArticleScraperConfig.json";

        try
        {
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存配置时出错: {ex.Message}");
        }
    }
}