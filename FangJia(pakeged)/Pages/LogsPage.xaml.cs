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
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FangJia.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class LogsPage
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    // 日志数据
    private ObservableCollection<LogItem> FilteredLogs { get; set; } = [];

    // 日志时间范围
    private long? _startUnixTime;
    private readonly List<string> _level = ["DEBUG", "INFO", "WARN", "ERROR"];

    public LogsPage()
    {
        InitializeComponent();
        OptionsAllCheckBox.IsChecked = true;
        LogTime.SelectedIndex = 0;
    }

    private async void LoadLogsAsync()
    {
        try
        {
            ConcurrentQueue<LogItem> batch = [];

            await _dispatcherQueue.EnqueueAsync(() =>
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                FilteredLogs.Clear();
            });

            await foreach (var log in LogHelper.GetLogsAsync(_startUnixTime, _level))
            {
                batch.Enqueue(log);

                if (batch.Count < 40) continue;
                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    foreach (var item in batch)
                    {
                        FilteredLogs.Add(item);
                    }
                });
                batch.Clear();
            }

            if (!batch.IsEmpty)
            {
                await _dispatcherQueue.EnqueueAsync(() =>
                {
                    foreach (var item in batch)
                    {
                        FilteredLogs.Add(item);
                    }
                });
            }

            await _dispatcherQueue.EnqueueAsync(() =>
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            });
        }
        catch (Exception e)
        {
            Logger.Error($"读取日志出错：{e.Message}");
        }
    }

    private void SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        Option1CheckBox.IsChecked = Option2CheckBox.IsChecked = Option3CheckBox.IsChecked = Option4CheckBox.IsChecked = true;
    }

    private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        Option1CheckBox.IsChecked = Option2CheckBox.IsChecked = Option3CheckBox.IsChecked = Option4CheckBox.IsChecked = false;
    }

    private void SelectAll_Indeterminate(object sender, RoutedEventArgs e)
    {
        // 如果全选框被选中（所有选项都被选中），
        // 点击该框将使其变为不确定状态。
        // 相反，我们希望取消选中所有框，
        // 因此我们通过编程方式来实现。不确定状态应该
        // 仅通过编程方式设置，而不是由用户设置。

        if (Option1CheckBox.IsChecked == true &&
            Option2CheckBox.IsChecked == true &&
            Option3CheckBox.IsChecked == true &&
            Option4CheckBox.IsChecked == true)
        {
            // 这将导致执行SelectAll_Unchecked，
            // 因此我们不需要在这里取消选中其他框。
            OptionsAllCheckBox.IsChecked = false;
        }
    }

    private void SetCheckedState()
    {
        // 第一次调用时控件为 null，因此我们只需要对任意一个控件进行 null检查。
        if (Option1CheckBox == null) return;
        OptionsAllCheckBox.IsChecked = Option1CheckBox.IsChecked switch
        {
            true when Option2CheckBox.IsChecked == true &&
                      Option3CheckBox.IsChecked == true &&
                      Option4CheckBox.IsChecked == true => true,
            false when Option2CheckBox.IsChecked == false &&
                       Option3CheckBox.IsChecked == false &&
                       Option4CheckBox.IsChecked == false => false,
            _ => null
        };
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
        LogsBlock.ScrollTo(0, 0);
        Task.Run(LoadLogsAsync);
    }

    private void Option_Checked(object sender, RoutedEventArgs e)
    {
        SetCheckedState();
    }

    private void Option_Unchecked(object sender, RoutedEventArgs e)
    {
        SetCheckedState();
    }

    private void RadioButtons_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not RadioButtons rbs) return;
        var t = rbs.SelectedItem as string;
        DateTime time;
        switch (t)
        {
            case "今日":
                time = DateTime.Now.Date;
                _startUnixTime = new DateTimeOffset(time).ToUnixTimeMilliseconds();
                break;
            case "7日内":
                time = DateTime.Now.Date.AddDays(-7);
                _startUnixTime = new DateTimeOffset(time).ToUnixTimeMilliseconds();
                break;
            default:
                _startUnixTime = null;
                break;
        }

        LogsBlock.ScrollTo(0, 0);
        Task.Run(LoadLogsAsync);
    }
}