using CommunityToolkit.WinUI;
using FangJia.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TGZH.Control; // 自定义控件命名空间
using Windows.ApplicationModel.DataTransfer;

namespace FangJia.Pages;

/// <summary>
/// 日志查看页面 - 用于显示、筛选和搜索系统日志
/// </summary>
public sealed partial class LogsPage
{
    #region 字段和属性

    // NLog 日志记录器
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // UI 线程调度器
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    // 加载操作的信号量，防止并发加载
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    // 日志数据集合 - 绑定到UI的源
    private ObservableCollection<LogItem> FilteredLogs { get; } = [];

    // 当前用户信息 - 用于高亮显示当前用户相关日志
    private string _currentUser = "taigongzhaihua"; // 2025-03-05 提供的当前用户

    // 当前UTC时间 - 用于状态栏显示
    private string _currentUtcTime = "2025-03-05 06:36:39"; // 2025-03-05 提供的当前UTC时间

    // 日志筛选的起始时间戳（Unix毫秒）
    private long? _startUnixTime =
        new DateTimeOffset(DateTime.Now.Date.ToUniversalTime().AddHours(8)).ToUnixTimeMilliseconds();

    // 要显示的日志级别列表
    private readonly List<string> _level = ["DEBUG", "INFO", "WARN", "ERROR"];

    // 搜索关键词
    private string _searchKeyword = string.Empty;

    #endregion

    #region 初始化和构造

    /// <summary>
    /// 构造函数 - 初始化页面和控件
    /// </summary>
    public LogsPage()
    {
        InitializeComponent();

        // 默认选中"全部"级别
        OptionsAllCheckBox.IsChecked = true;

        // 初始化底部状态栏
        UpdateStatusBar();

        // 设置周期性更新时间（每分钟更新一次）
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += (s, e) => UpdateCurrentTime();
        timer.Start();
    }

    #endregion

    #region 日志加载与处理

    /// <summary>
    /// 异步加载日志数据
    /// </summary>
    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    private async void LoadLogsAsync()
    {
        // 尝试进入信号量，如果没有获取到则表示已有加载操作正在进行
        if (!await _loadSemaphore.WaitAsync(0))
        {
            // 如果需要，可以在这里提示用户"加载正在进行中"
            return;
        }

        try
        {
            // 批量处理队列，减少UI更新次数提高性能
            ConcurrentQueue<LogItem> batch = [];

            // 切换到UI线程，显示加载状态并清空现有日志
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                FilteredLogs.Clear();
            });

            // 异步迭代获取日志
            await foreach (var log in LogHelper.GetLogsAsync(_startUnixTime, _level))
            {
                // 如果有搜索关键词，过滤不匹配的日志
                if (!string.IsNullOrEmpty(_searchKeyword))
                {
                    bool containsKeyword =
                        (log.Message?.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (log.Logger?.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (log.Exception?.Contains(_searchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);

                    if (!containsKeyword)
                        continue;
                }

                // 添加到批处理队列
                batch.Enqueue(log);

                // 当批处理队列达到一定大小时，更新UI
                if (batch.Count < 40) continue;

                // 复制批处理内容到临时列表，防止并发修改
                var tempBatch = batch.ToList();
                batch = []; // 重置队列

                // 在UI线程添加日志项
                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    foreach (var item in tempBatch)
                    {
                        FilteredLogs.Add(item);
                    }

                    // 更新状态栏统计信息
                    UpdateStatusBar();
                });
            }

            // 处理最后一批剩余日志（如果有）
            if (!batch.IsEmpty)
            {
                var tempBatch = batch.ToList();

                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    foreach (var item in tempBatch)
                    {
                        FilteredLogs.Add(item);
                    }

                    // 更新状态栏统计信息
                    UpdateStatusBar();
                });
            }
        }
        catch (Exception e)
        {
            // 记录错误日志
            Logger.Error($"读取日志出错：{e.Message}");

            // 在UI线程显示错误提示对话框
            await _dispatcherQueue.EnqueueAsync(async () =>
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "读取日志错误",
                    Content = $"读取日志时出现错误: {e.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                await errorDialog.ShowAsync();
            });
        }
        finally
        {
            // 隐藏加载指示器并更新统计信息
            await _dispatcherQueue.EnqueueAsync(() =>
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
                UpdateStatusBar();
            });

            // 释放信号量，允许下一次加载操作
            _loadSemaphore.Release();
        }
    }

    #endregion

    #region 状态更新方法

    /// <summary>
    /// 更新底部状态栏信息，包括日志计数和当前时间/用户
    /// </summary>
    private void UpdateStatusBar()
    {
        // 统计各级别日志数量
        int totalCount = FilteredLogs.Count;
        int debugCount = FilteredLogs.Count(l => l.Level == "DEBUG");
        int infoCount = FilteredLogs.Count(l => l.Level == "INFO");
        int warnCount = FilteredLogs.Count(l => l.Level == "WARN" || l.Level == "WARNING");
        int errorCount = FilteredLogs.Count(l => l.Level == "ERROR");

        // 更新日志计数显示
        LogCountTextBlock.Text = $"总数: {totalCount}  |  DEBUG: {debugCount}  |  INFO: {infoCount}  |  WARN: {warnCount}  |  ERROR: {errorCount}";

        // 更新当前用户和时间信息
        CurrentTimeTextBlock.Text = $"{_currentUtcTime} UTC  |  用户: {_currentUser}";
    }

    /// <summary>
    /// 更新当前时间显示
    /// </summary>
    private void UpdateCurrentTime()
    {
        // 实际应用中这里会获取系统当前时间
        // 但在此示例中我们使用指定的时间
        _currentUtcTime = DateTime.Now.ToString(format: "yyyy-mm-dd hh:mm:ss");
        UpdateStatusBar();
    }

    #endregion

    #region 筛选器相关事件处理

    /// <summary>
    /// 全选按钮被选中时，选中所有级别选项
    /// </summary>
    private void SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        Option1CheckBox.IsChecked =
            Option2CheckBox.IsChecked = Option3CheckBox.IsChecked = Option4CheckBox.IsChecked = true;
    }

    /// <summary>
    /// 全选按钮被取消选中时，取消所有级别选项
    /// </summary>
    private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        Option1CheckBox.IsChecked =
            Option2CheckBox.IsChecked = Option3CheckBox.IsChecked = Option4CheckBox.IsChecked = false;
    }

    /// <summary>
    /// 全选按钮进入不确定状态的处理
    /// </summary>
    private void SelectAll_Indeterminate(object sender, RoutedEventArgs e)
    {
        // 如果所有选项都被选中但用户点击全选框，
        // 应该取消选中所有选项而不是进入不确定状态
        if (Option1CheckBox.IsChecked == true &&
            Option2CheckBox.IsChecked == true &&
            Option3CheckBox.IsChecked == true &&
            Option4CheckBox.IsChecked == true)
        {
            // 这将触发 SelectAll_Unchecked 方法
            OptionsAllCheckBox.IsChecked = false;
        }
    }

    /// <summary>
    /// 根据各级别选项的状态更新全选框状态并重新加载日志
    /// </summary>
    private void SetCheckedState()
    {
        // 防止初始化阶段空引用
        if (Option1CheckBox == null) return;

        // 设置全选框状态
        OptionsAllCheckBox.IsChecked = Option1CheckBox.IsChecked switch
        {
            // 如果所有选项都选中，全选框为选中状态
            true when Option2CheckBox.IsChecked == true &&
                      Option3CheckBox.IsChecked == true &&
                      Option4CheckBox.IsChecked == true => true,
            // 如果所有选项都未选中，全选框为未选中状态
            false when Option2CheckBox.IsChecked == false &&
                       Option3CheckBox.IsChecked == false &&
                       Option4CheckBox.IsChecked == false => false,
            // 部分选中时，全选框为不确定状态
            _ => null
        };

        // 更新要显示的日志级别列表
        _level.Clear();
        if (Option1CheckBox.IsChecked == true)
        {
            _level.Add("DEBUG");
        }

        if (Option2CheckBox.IsChecked == true)
        {
            _level.Add("INFO");
        }

        if (Option3CheckBox.IsChecked == true)
        {
            _level.Add("WARN");
        }

        if (Option4CheckBox.IsChecked == true)
        {
            _level.Add("ERROR");
        }

        // 滚动到顶部并重新加载日志
        LogsBlock.ScrollTo(0, 0);
        Task.Run(LoadLogsAsync);
    }

    /// <summary>
    /// 级别选项被选中时的处理
    /// </summary>
    private void Option_Checked(object sender, RoutedEventArgs e)
    {
        SetCheckedState();
    }

    /// <summary>
    /// 级别选项被取消选中时的处理
    /// </summary>
    private void Option_Unchecked(object sender, RoutedEventArgs e)
    {
        SetCheckedState();
    }

    /// <summary>
    /// 时间范围选择变化时的处理
    /// </summary>
    private void RadioButtons_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not RadioButtons rbs) return;

        var t = rbs.SelectedItem as string;
        DateTime time;

        // 根据选择的时间范围设置起始时间戳
        switch (t)
        {
            case "今日":
                // 当天 00:00:00 的时间戳（东八区时间）
                time = DateTime.Now.Date.ToUniversalTime().AddHours(8);
                _startUnixTime = new DateTimeOffset(time).ToUnixTimeMilliseconds();
                break;
            case "7日内":
                // 7天前 00:00:00 的时间戳（东八区时间）
                time = DateTime.Now.Date.AddDays(-7).ToUniversalTime().AddHours(8);
                _startUnixTime = new DateTimeOffset(time).ToUnixTimeMilliseconds();
                break;
            default:
                // "全部"选项 - 不限制起始时间
                _startUnixTime = null;
                break;
        }

        // 滚动到顶部并重新加载日志
        LogsBlock.ScrollTo(0, 0);
        Task.Run(LoadLogsAsync);
    }

    #endregion

    #region 搜索与操作功能

    /// <summary>
    /// 搜索框提交查询时的处理
    /// </summary>
    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // 更新搜索关键词并重新加载日志
        _searchKeyword = args.QueryText ?? string.Empty;
        LogsBlock.ScrollTo(0, 0);
        Task.Run(LoadLogsAsync);
    }

    /// <summary>
    /// 搜索框文本变更事件处理
    /// </summary>
    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        // 只有当用户输入导致文本变更时才处理
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            // 可以在这里根据输入提供搜索建议
            // 例如: 常用搜索词、历史搜索记录等
            var suggestions = new List<string>();

            // 示例：根据常见日志内容提供建议
            var inputText = sender.Text.ToLower();
            if ("error".Contains(inputText)) suggestions.Add("error");
            if ("warning".Contains(inputText)) suggestions.Add("warning");
            if ("exception".Contains(inputText)) suggestions.Add("exception");
            if ("failed".Contains(inputText)) suggestions.Add("failed");
            if (_currentUser.ToLower().Contains(inputText)) suggestions.Add(_currentUser);

            // 更新搜索建议
            sender.ItemsSource = suggestions;
        }
    }

    /// <summary>
    /// 复制当前选中日志
    /// </summary>
    private void CopyLogMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is LogItem logItem)
        {
            // 创建数据包并复制日志文本到剪贴板
            var dataPackage = new DataPackage();
            dataPackage.SetText(logItem.ToString());
            Clipboard.SetContent(dataPackage);
        }
    }

    /// <summary>
    /// 清除所有显示的日志
    /// </summary>
    private void ClearLogsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        FilteredLogs.Clear();
        UpdateStatusBar();
    }

    /// <summary>
    /// 重新加载日志
    /// </summary>
    private void ReloadLogsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(LoadLogsAsync);
    }

    #endregion
}