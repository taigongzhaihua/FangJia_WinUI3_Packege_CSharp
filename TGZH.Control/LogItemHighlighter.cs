using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.UI;
using Windows.UI.Text;

namespace TGZH.Control
{
    public partial class LogItemHighlighter
    {
        // 日志级别样式
        private Dictionary<string, TextStyle> _levelStyles;

        // 元素样式
        private Dictionary<LogElementType, TextStyle> _elementStyles;

        // 内容细节样式
        private Dictionary<ContentDetailType, TextStyle> _detailStyles;

        // 当前操作用户名
        private string _currentUser = "taigongzhaihua";

        // 日志元素类型
        public enum LogElementType
        {
            Timestamp,
            Level,
            Logger,
            Message,
            Exception,
            Application,
            EventId
        }

        // 内容细节类型
        public enum ContentDetailType
        {
            Punctuation, // 标点符号
            Year, // 年份
            Month, // 月份
            Day, // 日期
            Hour, // 小时
            Minute, // 分钟  
            Second, // 秒
            Millisecond, // 毫秒
            Namespace, // 命名空间
            ClassName, // 类名
            MethodName, // 方法名
            LineNumber, // 行号
            Property, // 属性
            Number, // 普通数字
            Keyword, // 关键字
            String, // 字符串值
            Boolean, // 布尔值
            Null, // null值
            CurrentUser, // 当前用户名
            Special, // 特殊标记
            Path
        }

        public LogItemHighlighter()
        {
            InitializeDefaultStyles();
        }

        public void SetCurrentUser(string username)
        {
            _currentUser = username;
        }

        private void InitializeDefaultStyles()
        {
            // 初始化日志级别样式 - 暗色主题优化
            _levelStyles = new Dictionary<string, TextStyle>(StringComparer.OrdinalIgnoreCase)
            {
                { "TRACE", new TextStyle { Foreground = new SolidColorBrush(Colors.LightGray) } },
                { "DEBUG", new TextStyle { Foreground = new SolidColorBrush(Colors.Cyan) } },
                { "INFO", new TextStyle { Foreground = new SolidColorBrush(Colors.DeepSkyBlue) } },
                { "WARNING", new TextStyle { Foreground = new SolidColorBrush(Colors.Gold), IsBold = true } },
                { "WARN", new TextStyle { Foreground = new SolidColorBrush(Colors.Gold), IsBold = true } },
                { "ERROR", new TextStyle { Foreground = new SolidColorBrush(Colors.Tomato), IsBold = true } },
                {
                    "CRITICAL",
                    new TextStyle
                    {
                        Foreground = new SolidColorBrush(Colors.OrangeRed), IsBold = true,
                        Background = new SolidColorBrush(Color.FromArgb(80, 255, 127, 80))
                    }
                },
                {
                    "FATAL",
                    new TextStyle
                    {
                        Foreground = new SolidColorBrush(Colors.OrangeRed), IsBold = true,
                        Background = new SolidColorBrush(Color.FromArgb(80, 255, 127, 80))
                    }
                }
            };

            // 初始化元素样式 - 暗色主题优化
            _elementStyles = new Dictionary<LogElementType, TextStyle>
            {
                { LogElementType.Timestamp, new TextStyle { Foreground = new SolidColorBrush(Colors.SandyBrown) } },
                {
                    LogElementType.Logger,
                    new TextStyle { Foreground = new SolidColorBrush(Colors.MediumOrchid), IsItalic = true }
                },
                { LogElementType.Message, new TextStyle { Foreground = new SolidColorBrush(Colors.LightBlue) } },
                {
                    LogElementType.Exception,
                    new TextStyle { Foreground = new SolidColorBrush(Colors.LightCoral), IsBold = true }
                },
                {
                    LogElementType.Application,
                    new TextStyle { Foreground = new SolidColorBrush(Colors.MediumTurquoise) }
                },
                { LogElementType.EventId, new TextStyle { Foreground = new SolidColorBrush(Colors.Silver) } }
            };

            // 初始化内容细节样式 - 暗色主题优化
            _detailStyles = new Dictionary<ContentDetailType, TextStyle>
            {
                // 标点符号
                { ContentDetailType.Punctuation, new TextStyle { Foreground = new SolidColorBrush(Colors.Gold) } },

                // 时间部分
                { ContentDetailType.Year, new TextStyle { Foreground = new SolidColorBrush(Colors.PaleGoldenrod) } },
                { ContentDetailType.Month, new TextStyle { Foreground = new SolidColorBrush(Colors.PaleGoldenrod) } },
                { ContentDetailType.Day, new TextStyle { Foreground = new SolidColorBrush(Colors.PaleGoldenrod) } },
                { ContentDetailType.Hour, new TextStyle { Foreground = new SolidColorBrush(Colors.Khaki) } },
                { ContentDetailType.Minute, new TextStyle { Foreground = new SolidColorBrush(Colors.Khaki) } },
                { ContentDetailType.Second, new TextStyle { Foreground = new SolidColorBrush(Colors.Khaki) } },
                { ContentDetailType.Millisecond, new TextStyle { Foreground = new SolidColorBrush(Colors.LightPink) } },

                // 代码元素
                { ContentDetailType.Namespace, new TextStyle { Foreground = new SolidColorBrush(Colors.LightPink) } },
                {
                    ContentDetailType.ClassName,
                    new TextStyle { Foreground = new SolidColorBrush(Colors.HotPink), IsBold = true }
                },
                { ContentDetailType.MethodName, new TextStyle { Foreground = new SolidColorBrush(Colors.SandyBrown) } },
                {
                    ContentDetailType.LineNumber,
                    new TextStyle { Foreground = new SolidColorBrush(Colors.Coral), IsBold = true }
                },
                {
                    ContentDetailType.Property, new TextStyle { Foreground = new SolidColorBrush(Colors.PaleTurquoise) }
                },

                // 值类型
                { ContentDetailType.Number, new TextStyle { Foreground = new SolidColorBrush(Colors.CornflowerBlue) } },
                { ContentDetailType.String, new TextStyle { Foreground = new SolidColorBrush(Colors.LightSalmon) } },
                { ContentDetailType.Boolean, new TextStyle { Foreground = new SolidColorBrush(Colors.Plum) } },
                {
                    ContentDetailType.Null,
                    new TextStyle { Foreground = new SolidColorBrush(Colors.LightCoral), IsItalic = true }
                },

                // 特殊内容
                {
                    ContentDetailType.CurrentUser,
                    new TextStyle { Foreground = new SolidColorBrush(Colors.Yellow), IsBold = true }
                },
                { ContentDetailType.Keyword, new TextStyle { Foreground = new SolidColorBrush(Colors.SkyBlue) } },
                { ContentDetailType.Special, new TextStyle { Foreground = new SolidColorBrush(Colors.HotPink) } },
                { ContentDetailType.Path, new TextStyle { Foreground = new SolidColorBrush(Colors.LightGreen) } }
            };
        }

        // 设置元素样式
        public void SetElementStyle(LogElementType elementType, TextStyle style)
        {
            _elementStyles[elementType] = style;
        }

        // 设置内容细节样式
        public void SetDetailStyle(ContentDetailType detailType, TextStyle style)
        {
            _detailStyles[detailType] = style;
        }

        // 设置日志级别样式
        public void SetLevelStyle(string level, TextStyle style)
        {
            _levelStyles[level] = style;
        }

        // 高亮LogItem并返回WinUI的Paragraph
        public Paragraph HighlightLogItem(LogItem logItem)
        {
            var paragraph = new Paragraph();

            // 高亮时间戳，分解成各个部分
            HighlightTimestamp(paragraph, logItem.TimestampUtc);

            // 高亮日志级别
            HighlightLevel(paragraph, logItem.Level);

            // 高亮Logger（类名或命名空间）
            if (!string.IsNullOrEmpty(logItem.Logger))
            {
                HighlightLogger(paragraph, logItem.Logger);
                paragraph.Inlines.Add(new Run { Text = "\t：" });
            }

            // 高亮消息部分（详细解析）
            if (!string.IsNullOrEmpty(logItem.Message))
            {
                HighlightMessageContent(paragraph, logItem.Message);
            }

            // 高亮异常部分
            if (!string.IsNullOrEmpty(logItem.Exception))
            {
                paragraph.Inlines.Add(new Run { Text = " - " });
                HighlightExceptionContent(paragraph, logItem.Exception);
            }

            // // 应用程序信息（如果需要显示）
            // if (!string.IsNullOrEmpty(logItem.Application))
            // {
            //     paragraph.Inlines.Add(new LineBreak());
            //     var appRun = new Run { Text = "Application: " };
            //     paragraph.Inlines.Add(appRun);
            //     HighlightNameContent(paragraph, logItem.Application);
            // }

            // EventId（如果需要显示）
            if (logItem.EventId != 0)
            {
                paragraph.Inlines.Add(new LineBreak());
                var eventIdRun = new Run { Text = "EventId: " };
                paragraph.Inlines.Add(eventIdRun);

                var idRun = new Run { Text = logItem.EventId.ToString() };
                ApplyStyle(idRun, _detailStyles[ContentDetailType.Number]);
                paragraph.Inlines.Add(idRun);
            }

            return paragraph;
        }

        // 高亮时间戳（细分年月日时分秒）
        private void HighlightTimestamp(Paragraph paragraph, DateTime timestamp)
        {
            paragraph.Inlines.Add(new Run { Text = "[" });

            // 年
            var yearRun = new Run { Text = timestamp.Year.ToString() };
            ApplyStyle(yearRun, _detailStyles[ContentDetailType.Year]);
            paragraph.Inlines.Add(yearRun);

            paragraph.Inlines.Add(new Run { Text = "-" });

            // 月
            var monthRun = new Run { Text = timestamp.Month.ToString("D2") };
            ApplyStyle(monthRun, _detailStyles[ContentDetailType.Month]);
            paragraph.Inlines.Add(monthRun);

            paragraph.Inlines.Add(new Run { Text = "-" });

            // 日
            var dayRun = new Run { Text = timestamp.Day.ToString("D2") };
            ApplyStyle(dayRun, _detailStyles[ContentDetailType.Day]);
            paragraph.Inlines.Add(dayRun);

            paragraph.Inlines.Add(new Run { Text = " " });

            // 时
            var hourRun = new Run { Text = timestamp.Hour.ToString("D2") };
            ApplyStyle(hourRun, _detailStyles[ContentDetailType.Hour]);
            paragraph.Inlines.Add(hourRun);

            paragraph.Inlines.Add(new Run { Text = ":" });

            // 分
            var minuteRun = new Run { Text = timestamp.Minute.ToString("D2") };
            ApplyStyle(minuteRun, _detailStyles[ContentDetailType.Minute]);
            paragraph.Inlines.Add(minuteRun);

            paragraph.Inlines.Add(new Run { Text = ":" });

            // 秒
            var secondRun = new Run { Text = timestamp.Second.ToString("D2") };
            ApplyStyle(secondRun, _detailStyles[ContentDetailType.Second]);
            paragraph.Inlines.Add(secondRun);

            paragraph.Inlines.Add(new Run { Text = "." });

            // 毫秒
            var msRun = new Run { Text = timestamp.Millisecond.ToString("D3") };
            ApplyStyle(msRun, _detailStyles[ContentDetailType.Millisecond]);
            paragraph.Inlines.Add(msRun);

            paragraph.Inlines.Add(new Run { Text = "] " });
        }

        // 高亮日志级别
        private void HighlightLevel(Paragraph paragraph, string level)
        {
            var bracketRun = new Run { Text = "[" };
            ApplyStyle(bracketRun, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(bracketRun);

            var levelRun = new Run { Text = level };
            if (_levelStyles.TryGetValue(level ?? "", out var style))
            {
                ApplyStyle(levelRun, style);
            }

            paragraph.Inlines.Add(levelRun);

            bracketRun = new Run { Text = "]\t" };
            ApplyStyle(bracketRun, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(bracketRun);
        }

        // 高亮Logger信息（分析命名空间和类名）
        private void HighlightLogger(Paragraph paragraph, string logger)
        {
            // 检查是否包含命名空间格式 (例如 "Namespace.Class")
            var parts = logger.Split('.');

            if (parts.Length > 1)
            {
                // 最后一部分通常是类名，之前的是命名空间
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var namespacePart = new Run { Text = parts[i] };
                    ApplyStyle(namespacePart, _detailStyles[ContentDetailType.Namespace]);
                    paragraph.Inlines.Add(namespacePart);

                    var dotRun = new Run { Text = "." };
                    ApplyStyle(dotRun, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(dotRun);
                }

                // 类名部分
                var classNameRun = new Run { Text = parts[^1] };
                ApplyStyle(classNameRun, _detailStyles[ContentDetailType.ClassName]);
                paragraph.Inlines.Add(classNameRun);
            }
            else
            {
                // 如果没有分隔符，当作类名处理
                var loggerRun = new Run { Text = logger };
                ApplyStyle(loggerRun, _detailStyles[ContentDetailType.ClassName]);
                paragraph.Inlines.Add(loggerRun);
            }

            // 特殊用户高亮
            if (logger.Contains(_currentUser))
            {
                var usernameRun = new Run { Text = logger };
                ApplyStyle(usernameRun, _detailStyles[ContentDetailType.CurrentUser]);
                paragraph.Inlines.Add(usernameRun);
            }
        }

        // 高亮消息内容，包含对代码元素、数字、关键字等的识别
        private void HighlightMessageContent(Paragraph paragraph, string message)
        {
            // 提高匹配精确度的正则表达式

            // 1. 优化引号内容匹配，优先处理它们
            const string quotedContentPattern = """
                                                "([^"\\]*(?:\\.[^"\\]*)*)"
                                                """;

            // 2. 识别类似 SomeClass.Method:123 的模式（类名.方法名:行号）
            const string classMethodLinePattern = @"([A-Za-z0-9_\.]+)\.([A-Za-z0-9_]+)(:(\d+))?";

            // 3. 识别类似 at Namespace.Class.Method() in File.cs:line 123 的模式
            const string stackTracePattern =
                @"at\s+([A-Za-z0-9_\.]+)\.([A-Za-z0-9_]+)\(.*\)\s+in\s+([^:]+):line\s+(\d+)";

            // 4. 识别属性赋值 Property=Value
            const string propertyPattern = @"([A-Za-z0-9_]+)=([^\.,:;\(\)\[\]{}=\s]+)";

            // 5. 识别数字 (包括整数、小数、百分比)
            const string numberPattern = @"(?<![a-zA-Z0-9_])(\d+(\.\d+)?%?)(?=\s*(ms|KB|MB|GB|TB|B|s|Hz|V|A|W|px|em|rem)?\b|[^a-zA-Z0-9_]|$)";

            // 6. 识别路径 - 仅匹配非引号内的路径
            const string pathPattern = """(?<!["'])([A-Za-z]:\\[^"',\s\]]+)(?!["'])""";

            // 7. 识别关键字
            const string keywordPattern =
                @"((?<=\b)(null|true|false|async|await|new|throw|return|if|else|for|while|try|catch|finally)|(?<=\b|\d)(ms|KB|MB|GB|TB|B|s|Hz|V|A|W|px|em|rem))(?=\b)";

            // 8. 识别标点符号
            const string punctuationPattern = @"[.,:;()\[\]{}=]";

            // 9. 识别当前用户名
            var currentUserPattern = $@"\b({_currentUser})\b";

            // 首先处理高优先级模式，避免冲突
            var processingOrder = new Dictionary<string, ContentDetailType>
            {
                // 首先处理引号中的内容，避免其他模式干扰
                { quotedContentPattern, ContentDetailType.String },

                // 特殊模式
                { classMethodLinePattern, ContentDetailType.ClassName },
                { stackTracePattern, ContentDetailType.ClassName },
                { propertyPattern, ContentDetailType.Property },

                // 然后是路径，确保引号中的路径已被处理
                { pathPattern, ContentDetailType.String },

                // 最后是其他简单模式
                { numberPattern, ContentDetailType.Number },
                { keywordPattern, ContentDetailType.Keyword },
                { punctuationPattern, ContentDetailType.Punctuation },
                { currentUserPattern, ContentDetailType.CurrentUser }
            };

            // 处理消息内容
            ProcessContentWithRegex(paragraph, message, processingOrder);
        }

        // 高亮异常内容
        private void HighlightExceptionContent(Paragraph paragraph, string exception)
        {
            // 异常类型识别模式

            // 处理异常内容
            var exceptionTypeMatch = ExceptionTypeRegex().Match(exception);
            if (exceptionTypeMatch.Success)
            {
                // 异常类型
                var exTypeRun = new Run { Text = exceptionTypeMatch.Groups[1].Value };
                ApplyStyle(exTypeRun, new TextStyle
                {
                    Foreground = new SolidColorBrush(Colors.Red),
                    IsBold = true
                });
                paragraph.Inlines.Add(exTypeRun);

                // 异常后续部分
                string remaining = exception.Substring(exceptionTypeMatch.Length);
                if (!string.IsNullOrEmpty(remaining))
                {
                    // 递归处理异常详情部分
                    HighlightMessageContent(paragraph, remaining);
                }
            }
            else
            {
                // 如果没有匹配异常类型模式，当作普通消息处理
                HighlightMessageContent(paragraph, exception);
            }
        }

        // 高亮包含命名空间的内容
        private void HighlightNameContent(Paragraph paragraph, string content)
        {
            // 命名空间和类型模式
            var namespacePattern = @"([A-Za-z0-9]+)\.([A-Za-z0-9\.]+)";

            var match = Regex.Match(content, namespacePattern);
            if (match.Success)
            {
                // 分解命名空间和类型
                string[] parts = content.Split('.');

                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == parts.Length - 1)
                    {
                        // 最后部分当作类名处理
                        var classRun = new Run { Text = parts[i] };
                        ApplyStyle(classRun, _detailStyles[ContentDetailType.ClassName]);
                        paragraph.Inlines.Add(classRun);
                    }
                    else
                    {
                        // 其他部分当作命名空间处理
                        var nsRun = new Run { Text = parts[i] };
                        ApplyStyle(nsRun, _detailStyles[ContentDetailType.Namespace]);
                        paragraph.Inlines.Add(nsRun);

                        // 添加点
                        var dotRun = new Run { Text = "." };
                        ApplyStyle(dotRun, _detailStyles[ContentDetailType.Punctuation]);
                        paragraph.Inlines.Add(dotRun);
                    }
                }
            }
            else
            {
                // 如果没有命名空间格式，当作普通内容处理
                var contentRun = new Run { Text = content };
                paragraph.Inlines.Add(contentRun);
            }
        }

        // 使用正则表达式处理内容
        private void ProcessContentWithRegex(Paragraph paragraph, string content,
            Dictionary<string, ContentDetailType> patterns)
        {
            if (string.IsNullOrEmpty(content))
                return;

            // 保存所有匹配结果
            var allMatches = new List<MatchWithType>();

            // 保存已处理过的位置，避免重复处理
            var processedRanges = new List<(int Start, int End)>();

            // 按顺序找出所有匹配
            foreach (var pattern in patterns)
            {
                try
                {
                    foreach (Match match in Regex.Matches(content, pattern.Key))
                    {
                        // 检查这个匹配是否与已处理区域重叠
                        bool overlaps = processedRanges.Any(range =>
                            (match.Index >= range.Start && match.Index < range.End) ||
                            (match.Index + match.Length > range.Start && match.Index + match.Length <= range.End));

                        if (!overlaps)
                        {
                            allMatches.Add(new MatchWithType
                            {
                                Match = match,
                                Type = pattern.Value,
                                Pattern = pattern.Key
                            });

                            // 记录这个区域已处理
                            processedRanges.Add((match.Index, match.Index + match.Length));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 记录异常但继续处理
                    Debug.WriteLine($"正则表达式匹配出错: {ex.Message}");
                }
            }

            // 按起始位置排序
            allMatches = [.. allMatches.OrderBy(m => m.Match.Index)];

            var currentPos = 0;

            foreach (var matchInfo in allMatches)
            {
                // 添加未匹配部分
                if (matchInfo.Match.Index > currentPos)
                {
                    var normalText = content.Substring(currentPos, matchInfo.Match.Index - currentPos);
                    var normalRun = new Run { Text = normalText };
                    paragraph.Inlines.Add(normalRun);
                }

                // 特殊处理不同类型的匹配
                switch (matchInfo.Pattern)
                {
                    case """
                         "([^"\\]*(?:\\.[^"\\]*)*)"
                         """:
                        // 引号包围的内容（优化处理）
                        HandleQuotedContent(paragraph, matchInfo.Match);
                        break;

                    case @"([A-Za-z0-9_\.]+)\.([A-Za-z0-9_]+)(:(\d+))?":
                        // 类名.方法名:行号
                        HandleClassMethodLine(paragraph, matchInfo.Match);
                        break;

                    case @"at\s+([A-Za-z0-9_\.]+)\.([A-Za-z0-9_]+)\(.*\)\s+in\s+([^:]+):line\s+(\d+)":
                        // 堆栈跟踪行
                        HandleStackTraceLine(paragraph, matchInfo.Match);
                        break;

                    case @"([A-Za-z0-9_]+)=([^\.,:;\(\)\[\]{}=\s]+)":
                        // 属性=值
                        HandlePropertyValue(paragraph, matchInfo.Match);
                        break;

                    default:
                        // 默认处理
                        var matchRun = new Run { Text = matchInfo.Match.Value };
                        ApplyStyle(matchRun, _detailStyles[matchInfo.Type]);
                        paragraph.Inlines.Add(matchRun);
                        break;
                }

                // 更新当前位置
                currentPos = matchInfo.Match.Index + matchInfo.Match.Length;
            }

            // 添加剩余文本
            if (currentPos >= content.Length) return;
            var remainingText = content[currentPos..];
            var remainingRun = new Run { Text = remainingText };
            paragraph.Inlines.Add(remainingRun);
        }

        // 新增方法：专门处理引号包围的内容
        private void HandleQuotedContent(Paragraph paragraph, Match match)
        {
            // 添加开始引号
            var startQuote = new Run { Text = "\"" };
            ApplyStyle(startQuote, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(startQuote);

            // 引号内容
            var quotedText = match.Groups[1].Value;

            // 检查是否为路径格式
            if (PathHeadRegex().IsMatch(quotedText))
            {
                // 这是一个路径，使用路径样式
                var pathRun = new Run { Text = quotedText };
                ApplyStyle(pathRun, _detailStyles[ContentDetailType.Path]);
                paragraph.Inlines.Add(pathRun);
            }
            else
            {
                // 常规字符串内容
                var contentRun = new Run { Text = quotedText };
                ApplyStyle(contentRun, _detailStyles[ContentDetailType.String]);
                paragraph.Inlines.Add(contentRun);
            }

            // 添加结束引号
            var endQuote = new Run { Text = "\"" };
            ApplyStyle(endQuote, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(endQuote);
        }

        // 处理类名.方法名:行号的格式
        private void HandleClassMethodLine(Paragraph paragraph, Match match)
        {
            // 类名部分
            var fullTypeName = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            var lineNumber = match.Groups[4].Value;

            // 处理可能包含命名空间的类名
            var namespaceParts = fullTypeName.Split('.');

            for (var i = 0; i < namespaceParts.Length; i++)
            {
                if (i == namespaceParts.Length - 1 && !string.IsNullOrEmpty(lineNumber))
                {
                    // 类名
                    var classRun = new Run { Text = namespaceParts[i] };
                    ApplyStyle(classRun, _detailStyles[ContentDetailType.ClassName]);
                    paragraph.Inlines.Add(classRun);
                }
                else if (i == namespaceParts.Length - 1 && string.IsNullOrEmpty(lineNumber))
                {
                    // 命名空间
                    var nsRun = new Run { Text = namespaceParts[i] };
                    ApplyStyle(nsRun, _detailStyles[ContentDetailType.Namespace]);
                    paragraph.Inlines.Add(nsRun);
                }
                else
                {
                    // 命名空间
                    var nsRun = new Run { Text = namespaceParts[i] };
                    ApplyStyle(nsRun, _detailStyles[ContentDetailType.Namespace]);
                    paragraph.Inlines.Add(nsRun);

                    // 点
                    var dotRun = new Run { Text = "." };
                    ApplyStyle(dotRun, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(dotRun);
                }
            }

            // 点
            var methodDotRun = new Run { Text = "." };
            ApplyStyle(methodDotRun, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(methodDotRun);

            // 方法名
            var methodRun = new Run { Text = methodName };
            ApplyStyle(methodRun, _detailStyles[ContentDetailType.MethodName]);
            paragraph.Inlines.Add(methodRun);

            if (!string.IsNullOrEmpty(lineNumber))
            {
                // 冒号
                var colonRun = new Run { Text = ":" };
                ApplyStyle(colonRun, _detailStyles[ContentDetailType.Punctuation]);
                paragraph.Inlines.Add(colonRun);

                // 行号
                var lineRun = new Run { Text = lineNumber };
                ApplyStyle(lineRun, _detailStyles[ContentDetailType.LineNumber]);
                paragraph.Inlines.Add(lineRun);
            }
        }

        // 处理堆栈跟踪行
        private void HandleStackTraceLine(Paragraph paragraph, Match match)
        {
            // "at" 前缀
            var atRun = new Run { Text = "at " };
            paragraph.Inlines.Add(atRun);

            // 命名空间+类名
            var fullTypeName = match.Groups[1].Value;
            var namespaceParts = fullTypeName.Split('.');

            for (var i = 0; i < namespaceParts.Length; i++)
            {
                if (i == namespaceParts.Length - 1)
                {
                    // 类名
                    var classRun = new Run { Text = namespaceParts[i] };
                    ApplyStyle(classRun, _detailStyles[ContentDetailType.ClassName]);
                    paragraph.Inlines.Add(classRun);
                }
                else
                {
                    // 命名空间
                    var nsRun = new Run { Text = namespaceParts[i] };
                    ApplyStyle(nsRun, _detailStyles[ContentDetailType.Namespace]);
                    paragraph.Inlines.Add(nsRun);

                    // 点
                    var dotRun = new Run { Text = "." };
                    ApplyStyle(dotRun, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(dotRun);
                }
            }

            // 点
            var methodDotRun = new Run { Text = "." };
            ApplyStyle(methodDotRun, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(methodDotRun);

            // 方法名
            var methodName = match.Groups[2].Value;
            var methodRun = new Run { Text = methodName };
            ApplyStyle(methodRun, _detailStyles[ContentDetailType.MethodName]);
            paragraph.Inlines.Add(methodRun);

            // 参数部分
            var paramsRun = new Run { Text = "()" };
            ApplyStyle(paramsRun, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(paramsRun);

            // in 部分
            var inRun = new Run { Text = " in " };
            paragraph.Inlines.Add(inRun);

            // 文件名
            var fileName = match.Groups[3].Value;
            var fileRun = new Run { Text = fileName };
            ApplyStyle(fileRun, _detailStyles[ContentDetailType.String]);
            paragraph.Inlines.Add(fileRun);

            // :line 部分
            var lineTextRun = new Run { Text = ":line " };
            paragraph.Inlines.Add(lineTextRun);

            // 行号
            var lineNumber = match.Groups[4].Value;
            var lineRun = new Run { Text = lineNumber };
            ApplyStyle(lineRun, _detailStyles[ContentDetailType.LineNumber]);
            paragraph.Inlines.Add(lineRun);
        }

        // 处理属性=值格式
        private void HandlePropertyValue(Paragraph paragraph, Match match)
        {
            var propertyName = match.Groups[1].Value;
            var propertyValue = match.Groups[2].Value;

            // 属性名
            var propRun = new Run { Text = propertyName };
            ApplyStyle(propRun, _detailStyles[ContentDetailType.Property]);
            paragraph.Inlines.Add(propRun);

            // 等号
            var equalsRun = new Run { Text = "=" };
            ApplyStyle(equalsRun, _detailStyles[ContentDetailType.Punctuation]);
            paragraph.Inlines.Add(equalsRun);

            // 属性值 - 根据值类型选择样式
            ContentDetailType valueType;

            if (propertyValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                propertyValue.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                valueType = ContentDetailType.Boolean;
            }
            else if (propertyValue.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                valueType = ContentDetailType.Null;
            }
            else if (double.TryParse(propertyValue.TrimEnd('%'), out _))
            {
                valueType = ContentDetailType.Number;
            }
            else
            {
                valueType = ContentDetailType.String;
            }

            var valueRun = new Run { Text = propertyValue };
            ApplyStyle(valueRun, _detailStyles[valueType]);
            paragraph.Inlines.Add(valueRun);
        }

        // 高亮文本形式的日志（使用LogItem.ToString()的输出格式）
        public Paragraph HighlightLogText(string logText)
        {
            try
            {
                var paragraph = new Paragraph();

                // 匹配时间戳 [yyyy-MM-dd HH:mm:ss.fff]
                var timestampMatch = TimestampRegex().Match(logText);

                if (timestampMatch.Success)
                {
                    // 添加左括号
                    var leftBracket = new Run { Text = "[" };
                    ApplyStyle(leftBracket, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(leftBracket);

                    // 添加年
                    var yearRun = new Run { Text = timestampMatch.Groups[1].Value };
                    ApplyStyle(yearRun, _detailStyles[ContentDetailType.Year]);
                    paragraph.Inlines.Add(yearRun);

                    // 添加分隔符
                    var dash1 = new Run { Text = "-" };
                    ApplyStyle(dash1, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(dash1);

                    // 添加月
                    var monthRun = new Run { Text = timestampMatch.Groups[2].Value };
                    ApplyStyle(monthRun, _detailStyles[ContentDetailType.Month]);
                    paragraph.Inlines.Add(monthRun);

                    // 添加分隔符
                    var dash2 = new Run { Text = "-" };
                    ApplyStyle(dash2, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(dash2);

                    // 添加日
                    var dayRun = new Run { Text = timestampMatch.Groups[3].Value };
                    ApplyStyle(dayRun, _detailStyles[ContentDetailType.Day]);
                    paragraph.Inlines.Add(dayRun);

                    // 添加空格
                    paragraph.Inlines.Add(new Run { Text = " " });

                    // 添加时
                    var hourRun = new Run { Text = timestampMatch.Groups[4].Value };
                    ApplyStyle(hourRun, _detailStyles[ContentDetailType.Hour]);
                    paragraph.Inlines.Add(hourRun);

                    // 添加冒号
                    var colon1 = new Run { Text = ":" };
                    ApplyStyle(colon1, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(colon1);

                    // 添加分
                    var minuteRun = new Run { Text = timestampMatch.Groups[5].Value };
                    ApplyStyle(minuteRun, _detailStyles[ContentDetailType.Minute]);
                    paragraph.Inlines.Add(minuteRun);

                    // 添加冒号
                    var colon2 = new Run { Text = ":" };
                    ApplyStyle(colon2, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(colon2);

                    // 添加秒
                    var secondRun = new Run { Text = timestampMatch.Groups[6].Value };
                    ApplyStyle(secondRun, _detailStyles[ContentDetailType.Second]);
                    paragraph.Inlines.Add(secondRun);

                    // 添加点
                    var dot = new Run { Text = "." };
                    ApplyStyle(dot, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(dot);

                    // 添加毫秒
                    var msRun = new Run { Text = timestampMatch.Groups[7].Value };
                    ApplyStyle(msRun, _detailStyles[ContentDetailType.Millisecond]);
                    paragraph.Inlines.Add(msRun);

                    // 添加右括号
                    var rightBracket = new Run { Text = "]" };
                    ApplyStyle(rightBracket, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(rightBracket);

                    // 移除已处理的部分
                    logText = logText.Substring(timestampMatch.Length);
                }

                // 匹配日志级别 [INFO], [ERROR] 等
                var levelMatch = LevelRegex().Match(logText);

                if (levelMatch.Success)
                {
                    // 添加空格和左括号
                    paragraph.Inlines.Add(new Run { Text = " " });
                    var leftBracket = new Run { Text = "[" };
                    ApplyStyle(leftBracket, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(leftBracket);

                    // 添加级别文本
                    var levelValue = levelMatch.Groups[1].Value;
                    var levelRun = new Run { Text = levelValue };
                    if (_levelStyles.TryGetValue(levelValue, out var levelStyle))
                    {
                        ApplyStyle(levelRun, levelStyle);
                    }

                    paragraph.Inlines.Add(levelRun);

                    // 添加右括号
                    var rightBracket = new Run { Text = "]" };
                    ApplyStyle(rightBracket, _detailStyles[ContentDetailType.Punctuation]);
                    paragraph.Inlines.Add(rightBracket);

                    // 移除已处理的部分
                    logText = logText[levelMatch.Length..];
                }

                // 处理日志源和消息部分
                var parts = logText.Split(["\t："], 2, StringSplitOptions.None);

                switch (parts.Length)
                {
                    case > 0 when !string.IsNullOrWhiteSpace(parts[0]):
                        {
                            // 处理Logger部分
                            HighlightLogger(paragraph, parts[0]);

                            if (parts.Length <= 1) return paragraph;
                            // 添加制表符和冒号
                            paragraph.Inlines.Add(new Run { Text = "\t：" });

                            // 处理消息部分
                            HighlightMessageContent(paragraph, parts[1]);
                            break;
                        }
                    case > 1:
                        // 直接处理消息部分
                        HighlightMessageContent(paragraph, parts[1]);
                        break;
                    default:
                        {
                            if (!string.IsNullOrWhiteSpace(logText))
                            {
                                // 处理剩余文本
                                HighlightMessageContent(paragraph, logText);
                            }

                            break;
                        }
                }

                return paragraph;
            }
            catch (Exception)
            {
                // 出错时降级为普通显示
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = logText });
                return paragraph;
            }
        }

        // 应用文本样式到WinUI的Run
        private static void ApplyStyle(Run run, TextStyle style)
        {
            if (style == null) return;

            if (style.Foreground != null)
                run.Foreground = style.Foreground;

            // Remove the line that sets the Background property as Run does not have a Background property
            // if (style.Background != null)
            //     run.Background = style.Background;

            if (style.IsBold)
                run.FontWeight = FontWeights.Bold;

            if (style.IsItalic)
                run.FontStyle = FontStyle.Italic;

            if (style.IsUnderline)
                run.TextDecorations = TextDecorations.Underline;
        }

        // 匹配结果和类型的组合类
        private class MatchWithType
        {
            public Match Match { get; init; }
            public ContentDetailType Type { get; init; }
            public string Pattern { get; init; }
        }
        // 正则表达式生成器
        // 1. 时间戳
        [GeneratedRegex(@"^\[(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2}):(\d{2})\.(\d{3})\]")]
        private static partial Regex TimestampRegex();

        // 2. 日志级别
        [GeneratedRegex(@"^\s*\[([^\]]+)\]")]
        private static partial Regex LevelRegex();

        // 3. 异常类型
        [GeneratedRegex(@"^([A-Za-z0-9\.]+Exception)")]
        private static partial Regex ExceptionTypeRegex();

        // 4. 路径头部
        [GeneratedRegex(@"^[A-Za-z]:\\")]
        private static partial Regex PathHeadRegex();
    }

    // 文本样式类
    public class TextStyle
    {
        public SolidColorBrush Foreground { get; set; }
        public SolidColorBrush Background { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsUnderline { get; set; }
    }
}