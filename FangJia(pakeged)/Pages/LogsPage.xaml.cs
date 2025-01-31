using FangJia.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FangJia.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class LogsPage
{

    // 全部日志数据
    private readonly ObservableCollection<LogItem> _allLogs = [];

    // 筛选后的日志
    public ObservableCollection<LogItem>? FilteredLogs { get; set; } = [];

    // 用于控制轮询线程的取消令牌
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public LogsPage()
    {
        InitializeComponent();
        OptionsAllCheckBox.IsChecked = true;

        LoadLogs();
    }

    private void LoadLogs()
    {
        var logs = LogHelper.GetLogs(null, null); // 获取全部日志
        _allLogs.Clear();
        foreach (var log in logs)
        {
            _allLogs.Add(log);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var startDate = StartDatePicker.Date?.DateTime;
        var endDate = EndDatePicker.Date?.DateTime;


        var filtered = _allLogs
            .Where(log =>
                (!startDate.HasValue || log.TimestampUtc >= startDate.Value.Date) &&
                (!endDate.HasValue || log.TimestampUtc <= endDate.Value))
            .ToList();

        FilteredLogs?.Clear();
        foreach (var log in filtered)
        {
            FilteredLogs?.Add(log);
        }
    }

    private void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        ApplyFilter();
    }

    private void SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        Option1CheckBox.IsChecked = Option2CheckBox.IsChecked = Option3CheckBox.IsChecked = true;
    }

    private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        Option1CheckBox.IsChecked = Option2CheckBox.IsChecked = Option3CheckBox.IsChecked = false;
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
            Option3CheckBox.IsChecked == true)
        {
            // 这将导致执行SelectAll_Unchecked，
            // 因此我们不需要在这里取消选中其他框。
            OptionsAllCheckBox.IsChecked = false;
        }
    }

    private void SetCheckedState()
    {
        // 第一次调用时控件为null，因此我们只需要对任意一个控件进行null检查。
        if (Option1CheckBox != null)
        {
            OptionsAllCheckBox.IsChecked = Option1CheckBox.IsChecked switch
            {
                true when Option2CheckBox.IsChecked == true && Option3CheckBox.IsChecked == true => true,
                false when Option2CheckBox.IsChecked == false && Option3CheckBox.IsChecked == false => false,
                _ => null
            };
        }
    }

    private void Option_Checked(object sender, RoutedEventArgs e)
    {
        SetCheckedState();
    }

    private void Option_Unchecked(object sender, RoutedEventArgs e)
    {
        SetCheckedState();
    }

}