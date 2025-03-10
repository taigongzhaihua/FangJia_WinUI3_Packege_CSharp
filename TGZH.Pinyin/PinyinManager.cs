using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TGZH.Pinyin;

/// <summary>
/// 拼音管理器 - 内部类，处理拼音转换的核心逻辑
/// </summary>
internal class PinyinManager
{
    private PinyinDatabase _database;
    private PinyinWordDictionary _wordDictionary;
    private Dictionary<char, Dictionary<PinyinFormat, string[]>> _memoryCache;
    private PinyinLibraryOptions _options;

    /// <summary>
    /// 初始化状态
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 初始化拼音管理器
    /// </summary>
    public async Task InitializeAsync(PinyinLibraryOptions options)
    {
        if (IsInitialized)
            return;

        _options = options ?? new PinyinLibraryOptions();

        // 初始化数据库
        _database = new PinyinDatabase(_options.DatabasePath);
        await _database.InitializeAsync();

        // 初始化词典
        _wordDictionary = new PinyinWordDictionary(_database);
        await _wordDictionary.InitializeAsync();

        // 预加载内存缓存
        if (_options.PreloadCommonChars)
        {
            _memoryCache = [];
            await PreloadCommonCharactersAsync();
        }

        IsInitialized = true;
    }

    /// <summary>
    /// 获取单个汉字的拼音
    /// </summary>
    public async Task<string[]> GetCharPinyinAsync(char c, PinyinFormat format)
    {
        if (!PinyinHelper.IsChinese(c))
        {
            return [c.ToString()];
        }

        // 先从内存缓存找
        if (_memoryCache == null || !_memoryCache.TryGetValue(c, out var formatDict))
            return await _database.GetCharPinyinAsync(c, format);
        if (formatDict.TryGetValue(format, out var cachedResult))
            return cachedResult;

        // 从数据库查找
        return await _database.GetCharPinyinAsync(c, format);
    }

    /// <summary>
    /// 获取文本的拼音（考虑词语处理）
    /// </summary>
    public async Task<string> GetTextPinyinAsync(string text, PinyinFormat format, string separator)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        // 是否优先使用词语拼音
        if (_options.PrioritizeWordPinyin)
        {
            await AppendWordBasedPinyinAsync(text, format, separator, result);
        }
        else
        {
            // 逐字转换模式
            await AppendCharByCharPinyinAsync(text, format, separator, result);
        }

        return result.ToString();
    }

    /// <summary>
    /// 获取文本拼音（同步方法）
    /// </summary>
    public string GetTextPinyinSync(string text, PinyinFormat format, string separator)
    {
        // 同步方法，内部使用已缓存的数据
        // 注意：首次调用可能会较慢，推荐使用异步方法

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        // 按字符转换（同步方法主要用于简单场景，不处理词语）
        foreach (var c in text)
        {
            if (PinyinHelper.IsChinese(c))
            {
                // 从缓存获取
                var pinyins = GetCharPinyinFromCache(c, format);

                if (pinyins is not { Length: > 0 }) continue;
                if (result.Length > 0) result.Append(separator);
                result.Append(pinyins[0]); // 使用第一个拼音
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 基于词语的拼音转换（处理多音字）
    /// </summary>
    private async Task AppendWordBasedPinyinAsync(string text, PinyinFormat format, string separator, StringBuilder result)
    {
        var i = 0;
        while (i < text.Length)
        {
            // 尝试最长词语匹配
            var wordMatched = false;
            for (var len = Math.Min(_options.MaxWordLength, text.Length - i); len > 1; len--)
            {
                if (i + len > text.Length) continue;
                var word = text.Substring(i, len);
                var wordPinyin = await _wordDictionary.GetWordPinyinAsync(word, format, separator);

                if (string.IsNullOrEmpty(wordPinyin)) continue;
                // 词语匹配成功
                if (result.Length > 0) result.Append(separator);
                result.Append(wordPinyin);

                i += len;
                wordMatched = true;
                break;
            }

            // 如果没有匹配词语，按单字处理
            if (wordMatched) continue;
            var c = text[i];

            if (PinyinHelper.IsChinese(c))
            {
                var pinyins = await GetCharPinyinAsync(c, format);

                if (pinyins is { Length: > 0 })
                {
                    if (result.Length > 0) result.Append(separator);
                    result.Append(pinyins[0]); // 使用第一个拼音
                }
            }
            else
            {
                result.Append(c);
            }

            i++;
        }
    }


    // 在PinyinManager类中添加以下批量处理方法
    public async Task<string[]> GetTextPinyinBatchAsync(string text, PinyinFormat format, string separator = " ")
    {
        if (string.IsNullOrEmpty(text))
            return [string.Empty];
        if (!IsInitialized)
        {
            await InitializeAsync(null);
        }
        var result = new string[text.Length];

        // 获取文本中所有汉字的拼音
        var charPinyinArray = await _database.GetTextCharPinyinBatchAsync(text, format);

        // 对每个字符选择一个拼音（通常是第一个）
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (i < charPinyinArray.Length && charPinyinArray[i] != null && charPinyinArray[i].Length > 0)
            {
                // 使用该字符的第一个拼音
                result[i] = charPinyinArray[i][0];
            }
            else
            {
                // 非中文字符或查询失败，保持原字符
                result[i] = c.ToString();
            }
        }

        // 连接结果
        return result;
    }

    /// <summary>
    /// 批量获取多个文本的拼音
    /// </summary>
    public async Task<Dictionary<string, string>> GetTextPinyinBatchAsync(
        string[] texts, PinyinFormat format, string separator)
    {
        if (texts == null || texts.Length == 0)
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>();

        // 使用并行处理提高性能
        var options = new ParallelOptions { MaxDegreeOfParallelism = _options.MaxParallelism };
        var concurrentResults = new ConcurrentDictionary<string, string>();

        await Parallel.ForEachAsync(texts, options, async (text, _) =>
        {
            if (string.IsNullOrEmpty(text))
            {
                concurrentResults.TryAdd(text, string.Empty);
                return;
            }

            var pinyin = await ProcessTextBatchAsync(text, format, separator);
            concurrentResults.TryAdd(text, pinyin);
        });

        foreach (var pair in concurrentResults)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    /// <summary>
    /// 批量获取多个文本的拼音（同步方法）
    /// </summary>
    public Dictionary<string, string> GetTextPinyinBatchSync(string[] texts, PinyinFormat format, string separator)
    {
        if (texts == null || texts.Length == 0)
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>();

        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                result[text] = string.Empty;
                continue;
            }

            result[text] = GetTextPinyinSync(text, format, separator);
        }

        return result;
    }

    /// <summary>
    /// 流式处理多个文本的拼音，逐个返回结果
    /// </summary>
    public async IAsyncEnumerable<KeyValuePair<string, string>> GetTextPinyinStreamingAsync(
        IEnumerable<string> texts,
        PinyinFormat format,
        string separator)
    {
        // 预先收集文本并分析常用字符
        var textList = texts.ToList();

        if (textList.Count > 0)
        {
            await PreloadCommonCharsFromTexts(textList, format);
        }

        foreach (var text in textList)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield return new KeyValuePair<string, string>(text, string.Empty);
                continue;
            }

            var pinyin = await ProcessTextBatchAsync(text, format, separator);
            yield return new KeyValuePair<string, string>(text, pinyin);
        }
    }

    /// <summary>
    /// 流式处理单个大文本，分块返回结果
    /// </summary>
    public async IAsyncEnumerable<string> GetTextChunkStreamingAsync(
        string text,
        PinyinFormat format,
        string separator,
        int chunkSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }

        // 预热：提前分析并加载常见字符到缓存
        await PreloadCommonCharsFromText(text, format);

        for (var i = 0; i < text.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, text.Length - i);
            var chunk = text.Substring(i, length);

            var pinyin = await ProcessTextBatchAsync(chunk, format, separator);
            yield return pinyin;
        }
    }

    // 高效批量处理文本
    public async Task<string> ProcessTextBatchAsync(string text, PinyinFormat format, string separator = " ")
    {
        // 空文本直接返回
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // 先尝试查找多字词
        var words = SegmentText(text);
        var sb = new StringBuilder();

        // 查询所有词的拼音
        var wordPinyinDict = await _database.GetWordsPinyinBatchAsync(
            words.Where(w => w.Length > 1).ToArray(), format);

        // 处理分词结果
        foreach (var word in words)
        {
            if (word.Length > 1 && wordPinyinDict.TryGetValue(word, out var wordPinyin))
            {
                // 使用词语的拼音
                sb.Append(wordPinyin);
                sb.Append(separator);
            }
            else
            {
                // 单个字符或未找到词语拼音，逐字处理
                var charPinyin = await GetTextPinyinBatchAsync(word, format, "");
                sb.Append(string.Join(separator, charPinyin));
                sb.Append(separator);
            }
        }

        return sb.ToString().TrimEnd();
    }

    // 简单的文本分词方法（实际应用中可能需要更复杂的分词算法）
    private string[] SegmentText(string text)
    {
        // 这里使用一个非常简单的分词方法，实际应用可以使用专业分词库
        var segments = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsChinese(c)) continue;
            // 遇到非汉字时，将之前的汉字段作为一个词
            if (i > start)
            {
                segments.Add(text.Substring(start, i - start));
            }
            segments.Add(c.ToString());
            start = i + 1;
        }

        // 添加最后一段
        if (start < text.Length)
        {
            segments.Add(text[start..]);
        }

        return segments.ToArray();
    }

    // 判断是否为汉字
    private bool IsChinese(char c)
    {
        // 基本汉字范围和扩展区
        return (c >= 0x4E00 && c <= 0x9FFF) ||
               (c >= 0x3400 && c <= 0x4DBF) ||
               (c >= 0x20000 && c <= 0x2A6DF);
    }
    /// <summary>
    /// 逐字转换拼音（不考虑词语）
    /// </summary>
    private async Task AppendCharByCharPinyinAsync(string text, PinyinFormat format, string separator, StringBuilder result)
    {
        foreach (var c in text)
        {
            if (PinyinHelper.IsChinese(c))
            {
                var pinyins = await GetCharPinyinAsync(c, format);

                if (pinyins is not { Length: > 0 }) continue;
                if (result.Length > 0) result.Append(separator);
                result.Append(pinyins[0]); // 使用第一个拼音
            }
            else
            {
                result.Append(c);
            }
        }
    }

    /// <summary>
    /// 从缓存获取汉字拼音（用于同步方法）
    /// </summary>
    private string[] GetCharPinyinFromCache(char c, PinyinFormat format)
    {
        if (_memoryCache != null && _memoryCache.TryGetValue(c, out var formatDict) &&
            formatDict.TryGetValue(format, out var result))
        {
            return result;
        }

        // 缓存中没有，返回原字符
        return [c.ToString()];
    }

    /// <summary>
    /// 预加载常用字符到内存
    /// </summary>
    private async Task PreloadCommonCharactersAsync()
    {
        // 常用汉字集合 (前2000个常用汉字)
        const string commonChars = "的一是了我不人在他有这个上们来到时大地为子中你说生国年着就那和要她出也得里后自以会家可下而过天去能对小多然于心学么之都好看起发当没成只如事把还用第样道想作种开美总从无情己面最文化些月者所日手又行意动方期它头经长儿回位分爱老因很给名法间斯知世什两次使身者被高已亲其进此话常与活正感见明问力理尔点文几定本公特做外孩相西果走将月十实向声车全信重三机工物气每并别真打太新比才便夫再书部水像眼等体却加电主界门利海受听表德少克代员许稜先口由死安写性马光白或住难望教命花结乐色更拉东神记处让母父应直字场平报友关放至张认接告入笑内英军候民岁往何度山觉路带万男边风解叫任金快原吃妈变通师立象数四失满战远格士音轻目条呢病始达深完今提求清王化空业思切怎非找片罗钱紶吗语元喜曾离飞科言干流欢约各即指合反题必该论交终林请医晚制球决传画保读运及则房早院量苦火布品近坐产答星精视五连司巴夜青才识六治木灭区什光用习制"
                                   + "直写算性场明体今合任保府走态导众图温管赛粮信仍手转极支存近般响件林语既刻师军团合石回刘线候司满青利状表六正感局照写布刘啊纪基旧板类拉律鱼显衣否身革息标选包希尽管鲁赵余林宗兴岁丽早形货制昨微断消业候究制决窗必格队庆跑运县导功战灵图层林获均调沙穿病楼紧免实况造谓历公价活鲁湖办选规约巴材演集百录历戏写协增批江建称仍呢街冷呼规土较玉革互洲户加历极判投找标市设治形倒价楼周强传弟呼微调忙乎局永黑公板采跟刘亚业连阶焦周林胡红仅鲁配究何青舞质古易斯岁证刚岁红她床既敬示复价部甚选登座络宁乎神合示板材空达设罗规图台令料调角低沉复仅集条严究批节青易斯岁晚称制弦取湖依范复具程通陈收节营口险底戏青跑啊最章商族密批供销究矛胜郭劳胜弱询陈音赶火粮斗乡呼牛习卫质顾客霞街白汽助物乡跳描厂辆君具膀石划状消参护派卫章户厂算温济妇均房尽注思充株粉英宽医否兴语料帮轮木亮旧板闭枢际舞杂志闻史码质担弱圆凝介功练详慢方谓阴飞满河市验船验愿持空医左异忽榜段团屋脚效居既队竟芝类养底售速梦收层价肉呼阵利角豆腹往印管破矿批蔬登居竟养音配革顾党倒沿缓划残丽饮医济界待设营厚菜票降枝重略贵凭础港损避缩照择术盐束敢拿肥妇困族似杨剧令配领社布张罢压荷牌架占励架续景财猛害首势档残拉确移棋路剧词呼肉杀害继团皮份委帐使挥藏态烟岁练"
                                   + "杨词圣改版亩祖透免喜首脑儿溪倒杨年败供申权置态表婚词苏训捉火候协判拍挥值育录况谣激途防观远货招纸周征脚尽散防杂乡顺辈览副属盟斗板背恶励牛村章刑床经销谓恶阵管侵验码规村烧贵地艺脚规痛崇优织装疑层建堂草血视居升占房章哥朝某规库够椅述划补伯限历景玛环末茶启政际园端破购香药旧迅欢兵场命啊尊强纸制兴排印表小购哪争树雷养杰饭含宪退规哥伤役醒茶掉温背毕触核湖推晓宪销签田施控怕该退引杂概存饭快练痛虽谢获络拥料配克典临伤挥掌甚材户占份阳际采曲征纪律润丝若策验究团径项洁替存县芝阶限殖引降摇批翻胜依群首营减杂环康宁慢播抗干架伟形害永月买移领的勒划连波演算败箱抗良控炼械怕尾众移硬确店题某凝液升蔬烯养聚扩证售脸探销批溶液织掌牛树营留验须菜括税限伤箱剧承胜移略符织顺胶察线亚奏组奖售误鸡抓势块护售宣税午讨套针获温液鹏络括肩括获伤汽言败率消织扮谊奖委欢龙晓透折伤宣策婚堂批况瓶源剧环护粮环条获伤离球环如埋环材绍脱水克反品旧臂染乐担废怀触络浓践抓态胜午蓝败篇感品持偏胜溶践悉残验伤及租悉阻畅雷尽赶改晴毫泽衡宁泪若硬佛湖停络碗担症鼓垂航纪狂吧泪拥拒估液汇倍狱烧汽掉播韵雨括秋娱继退且穷配宽兆猛拒撑括污掌获液谊净允减慢员汇币拥验育伤章慢限益烈肠续租抗殖肿据无榜版练减继汽满堆框堆伟托倍斗锋奏宪泪症策掉胜伟坏遍诸持确抗隐环培侨鼓儿括突暴毫培赵沿颗抓孙碎伤指殖奇系毫类拒信培烈孙缓刘培超绍据盖绪投坏聚植伤绍楚木举姆帐盖壮促咬润案胞践檐跟敌徐织"
                                   +
                                   "促纯陆狱尾牛膜壁减梁妻胜毫跳雪枯喝骨膜坏秋恢妻柜伤椅练减触担陆扮参良畅距拥弹壁译恢黎疑哪汗辖降钱络瓦伯哪但辆探防殖跳洽印晒护叶奇陪村贸碎染喝炼况倍诉诉毯宇似毯促环投秤展医障睡沾翻护叶乎蒸颜署责杰技陵惜肾隐社翻诸午诸泪侧尾胜维烧恐弃获脱陆志围稻广顾园良轮次秘译伙浓衡伙系哪译姆班慢校删娱饭谊班碎烧枯冒还锅实济谁令技胃券骗绕掉辛泽咬融壶探椰译剧汽徐互群彻械缩跳缓卵汽纳范谁印枯晨彻丰疑且隐倍徐损诉责确净狱腰讯遇译志贸宪探签译教福盯协弃签诸安词卵窗哲侧革痛彻署腰似劲簧沾促诉贸搞错辖签模肾诊援普围析诉融钟敬暑绍诵志徐乎谁互净触午旦毯侧奉陵贸狱肾搬杰弃纬奋姆铜挖商护谓继抹彻堆堂绕匀归遇阅溶盯诉误钢弹改睡乎较养溯侧毯陪浙陪骗烧归控纽但皮类欢野萨差遗泪技技钢班首归港纠徐递哲旦融菜植益脾胆姆秋吊答窗锦萨略净获挖陪伏梁班弹益倍予愈附袖纹锋伯洽预纪亮殊降洽烧锅犹熟良慢客弹良依净损慢含淡伏颜纽鼻翻肉饮墨港伙泽签烧暗装届岛陵配暑磁贸税垫教仪伟域恢答肯泽伏技皮武益肠避促互吧辑谁阵仁互钟倍诵映搬矛予惊校递梁弃袖贵檐框率僚董凭辊属胁洁食促究殖诸仪族获刑牲疗仿俄诉促腐试室惊谁栽链兹狠烃凭疑炸碰退械肯踪汤定倪抹估伏纯络秋钢燃促皮纠偷弊悼殖腊净但徐捷园俄扶喷校牲棱亮宏以而仿综侵系俊绘炼担胀助丽焦偿棱概乏蒸阿凡器映吴龄岸诉触锐侧车痛笑俄躺肯伊徐构倪胶裁研檐凭悼估阿伏智燃械洽侵歌亮研恼吧婆缓悲概检踪降循迹蒸焦乃诊监钟优鸟宏凭悼聋悔呆殖匹养刑装睹胁咐尴尾誉独颜塔抚践循涌船烧跑厅烧辆曲酒挥陵塔佛忌丹吓抚屏俊牲倾泛乏真脂熄宴但伴购阅综炭赖盗贤粪抹遭脚辑累轻阔谅宫循序侍恋尾饮伏疆检勿述善答辅故搞呆擦谅勿焦勉却船";

        var loadTasks = (from c in commonChars where PinyinHelper.IsChinese(c) select LoadCharacterAllFormatsAsync(c)).ToList();

        // 等待所有异步加载任务完成
        var results = await Task.WhenAll(loadTasks);

        // 将结果放入内存缓存
        for (int i = 0, j = 0; i < commonChars.Length; i++)
        {
            var c = commonChars[i];
            if (PinyinHelper.IsChinese(c))
            {
                _memoryCache[c] = results[j++];
            }
        }
    }

    /// <summary>
    /// 加载一个汉字的所有拼音格式
    /// </summary>
    private async Task<Dictionary<PinyinFormat, string[]>> LoadCharacterAllFormatsAsync(char c)
    {
        var result = new Dictionary<PinyinFormat, string[]>
        {
            // 加载所有格式的拼音
            [PinyinFormat.WithToneMark] = await _database.GetCharPinyinAsync(c, PinyinFormat.WithToneMark),
            [PinyinFormat.WithoutTone] = await _database.GetCharPinyinAsync(c, PinyinFormat.WithoutTone),
            [PinyinFormat.WithToneNumber] = await _database.GetCharPinyinAsync(c, PinyinFormat.WithToneNumber),
            [PinyinFormat.FirstLetter] = await _database.GetCharPinyinAsync(c, PinyinFormat.FirstLetter)
        };

        return result;
    }

    /// <summary>
    /// 从文本集合中预加载常用字符
    /// </summary>
    private async Task PreloadCommonCharsFromTexts(List<string> texts, PinyinFormat format)
    {
        var uniqueChars = new HashSet<char>();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text)) continue;

            foreach (var c in text)
            {
                if (PinyinHelper.IsChinese(c))
                {
                    uniqueChars.Add(c);
                }
            }
        }

        if (uniqueChars.Count > 0)
        {
            await _database.GetCharsPinyinBatchAsync(uniqueChars.ToArray(), format);
        }
    }

    /// <summary>
    /// 从单个文本预加载常用字符
    /// </summary>
    private async Task PreloadCommonCharsFromText(string text, PinyinFormat format)
    {
        if (string.IsNullOrEmpty(text)) return;

        var uniqueChars = new HashSet<char>();
        foreach (var c in text.Where(PinyinHelper.IsChinese))
        {
            uniqueChars.Add(c);
        }

        if (uniqueChars.Count > 0)
        {
            await _database.GetCharsPinyinBatchAsync(uniqueChars.ToArray(), format);
        }
    }
}