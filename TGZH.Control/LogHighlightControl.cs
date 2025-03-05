using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace TGZH.Control
{
    [TemplatePart(Name = RichTextBlockName, Type = typeof(RichTextBlock))]
    public partial class LogHighlightControl : Microsoft.UI.Xaml.Controls.Control
    {
        private const string RichTextBlockName = "PART_RichTextBlock";
        private RichTextBlock _richTextBlock;
        private readonly LogItemHighlighter _highlighter;

        // LogItem 依赖属性
        public static readonly DependencyProperty LogItemProperty =
            DependencyProperty.Register(
                nameof(LogItem),
                typeof(LogItem),
                typeof(LogHighlightControl),
                new PropertyMetadata(null, OnLogItemChanged));

        // CurrentUser 依赖属性
        public static readonly DependencyProperty CurrentUserProperty =
            DependencyProperty.Register(
                nameof(CurrentUser),
                typeof(string),
                typeof(LogHighlightControl),
                new PropertyMetadata("taigongzhaihua", OnCurrentUserChanged));

        public LogItem LogItem
        {
            get => (LogItem)GetValue(LogItemProperty);
            set => SetValue(LogItemProperty, value);
        }

        public string CurrentUser
        {
            get => (string)GetValue(CurrentUserProperty);
            set => SetValue(CurrentUserProperty, value);
        }

        public LogHighlightControl()
        {
            // WinUI 3中，直接在构造函数中设置DefaultStyleKey
            this.DefaultStyleKey = typeof(LogHighlightControl);
            _highlighter = new LogItemHighlighter();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // 获取模板中的RichTextBlock
            _richTextBlock = GetTemplateChild(RichTextBlockName) as RichTextBlock;

            // 设置当前用户
            _highlighter.SetCurrentUser(CurrentUser);

            // 更新高亮显示
            UpdateHighlighting();
        }

        private static void OnLogItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as LogHighlightControl;
            if (control?._richTextBlock != null)
                control.UpdateHighlighting();
        }

        private static void OnCurrentUserChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as LogHighlightControl;
            if (control != null)
            {
                control._highlighter.SetCurrentUser(e.NewValue as string);
                control.UpdateHighlighting();
            }
        }

        private void UpdateHighlighting()
        {
            _richTextBlock.Blocks.Clear();

            // 使用高亮器生成富文本段落
            var paragraph = _highlighter.HighlightLogItem(LogItem);
            _richTextBlock.Blocks.Add(paragraph);
        }

        // 添加这个方法帮助从父级容器获取当前日志内容的宽度
        protected override Size MeasureOverride(Size availableSize)
        {
            // 确保RichTextBlock获得正确的测量尺寸
            _richTextBlock.Measure(availableSize);
            return base.MeasureOverride(availableSize);
        }

        // 添加这个方法帮助正确布局
        protected override Size ArrangeOverride(Size finalSize)
        {
            // 确保RichTextBlock获得正确的排列区域
            _richTextBlock.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return base.ArrangeOverride(finalSize);
        }
    }
}