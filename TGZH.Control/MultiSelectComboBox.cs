// MultiSelectComboBox.cs

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TGZH.Control;

public partial class MultiSelectComboBox : ComboBox
{
    public MultiSelectComboBox()
    {
        DefaultStyleKey = typeof(MultiSelectComboBox);
        SelectedItems = [];
        DropDownClosed += MultiSelectComboBox_DropDownClosed;
        DropDownOpened += MultiSelectComboBox_DropDownOpened;
    }



    #region 依赖属性

    // 选中项集合
    public IList<object> SelectedItems
    {
        get => (IList<object>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(IList<object>),
            typeof(MultiSelectComboBox),
            new PropertyMetadata(new ObservableCollection<object>(), OnSelectedItemsChanged));

    // 用于显示选中项文本的属性
    public string SelectedItemsText
    {
        get => (string)GetValue(SelectedItemsTextProperty);
        private set => SetValue(SelectedItemsTextProperty, value);
    }

    public static readonly DependencyProperty SelectedItemsTextProperty =
        DependencyProperty.Register(
            nameof(SelectedItemsText),
            typeof(string),
            typeof(MultiSelectComboBox),
            new PropertyMetadata("请选择..."));

    // 全选功能开关，默认为 false（关闭）
    public bool IsSelectAllEnabled
    {
        get => (bool)GetValue(IsSelectAllEnabledProperty);
        set => SetValue(IsSelectAllEnabledProperty, value);
    }

    public static readonly DependencyProperty IsSelectAllEnabledProperty =
        DependencyProperty.Register(
            nameof(IsSelectAllEnabled),
            typeof(bool),
            typeof(MultiSelectComboBox),
            new PropertyMetadata(false));

    // 反选功能开关，默认为 false（关闭）
    public bool IsInvertSelectionEnabled
    {
        get => (bool)GetValue(IsInvertSelectionEnabledProperty);
        set => SetValue(IsInvertSelectionEnabledProperty, value);
    }

    public static readonly DependencyProperty IsInvertSelectionEnabledProperty =
        DependencyProperty.Register(
            nameof(IsInvertSelectionEnabled),
            typeof(bool),
            typeof(MultiSelectComboBox),
            new PropertyMetadata(false));

    // 最大文字宽度
    public double MaxTextWidth
    {
        get => (double)GetValue(MaxTextWidthProperty);
        set => SetValue(MaxTextWidthProperty, value);
    }

    public static readonly DependencyProperty MaxTextWidthProperty =
        DependencyProperty.Register(
            nameof(MaxTextWidth),
            typeof(double),
            typeof(MultiSelectComboBox),
            new PropertyMetadata(200.0));

    // 默认分隔符为逗号
    public static readonly DependencyProperty SeparatorProperty =
        DependencyProperty.Register(
            nameof(Separator),
            typeof(string),
            typeof(MultiSelectComboBox),
            new PropertyMetadata(", ", OnSeparatorChanged));

    public string Separator
    {
        get => (string)GetValue(SeparatorProperty);
        set => SetValue(SeparatorProperty, value);
    }

    #endregion

    private ListView _multiSelectListView;
    private Button _selectAllButton;
    private Button _invertSelectionButton;
    private Flyout _flyout;
    private ToggleSplitButton _splitButton;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _multiSelectListView = GetTemplateChild("MultiSelectListView") as ListView;
        if (_multiSelectListView != null)
        {
            _multiSelectListView.SelectionMode = ListViewSelectionMode.Multiple;
            _multiSelectListView.SelectionChanged += MultiSelectListView_SelectionChanged;
        }

        _splitButton = GetTemplateChild("SplitButton") as ToggleSplitButton;
        _flyout = GetTemplateChild("Flyout") as Flyout;
        if (_flyout != null)
        {
            _flyout.Opened += Flyout_Opened;
            _flyout.Closed += Flyout_Closed;
        }
        // 查找全选按钮并挂接事件
        _selectAllButton = GetTemplateChild("SelectAllButton") as Button;
        if (_selectAllButton != null)
        {
            _selectAllButton.Click += SelectAllButton_Click;
        }

        // 查找反选按钮并挂接事件
        _invertSelectionButton = GetTemplateChild("InvertSelectionButton") as Button;
        if (_invertSelectionButton != null)
        {
            _invertSelectionButton.Click += InvertSelectionButton_Click;
        }
    }

    #region 辅助函数

    // 用于更新 SelectedItemsText 属性
    private void UpdateSelectedItemsText()
    {
        if (SelectedItems != null && SelectedItems.Any())
        {
            SelectedItemsText = string.Join(Separator, SelectedItems.Select(x => x.ToString()));
        }
        else
        {
            SelectedItemsText = "请选择...";
        }
    }

    #endregion

    #region 属性改变事件处理

    // 列表选中项改变时的处理
    private void MultiSelectListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_multiSelectListView == null) return;
        // 更新 SelectedItems（此处直接将 ListView 的 SelectedItems 转换为 List<object>）
        SelectedItems = _multiSelectListView.SelectedItems;
        UpdateSelectedItemsText();
    }

    // 选中项属性改变时的处理
    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as MultiSelectComboBox;
        var listView = control?._multiSelectListView;
        control?.UpdateSelectedItemsText();
        if (Equals(control?.SelectedItems, listView?.SelectedItems)) return;
        listView?.SelectedItems.Clear();
        if (control.SelectedItems == null) return;
        foreach (var item in control.SelectedItems)
        {
            listView?.SelectedItems.Add(item);
        }
    }

    // 分隔符改变时的处理
    private static void OnSeparatorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as MultiSelectComboBox;
        // 当分隔符改变时，更新显示文本
        control?.UpdateSelectedItemsText();
    }
    private void Flyout_Opened(object sender, object e)
    {
        // 当 Flyout 打开时，设置 IsDropDownOpen 为 true
        // 此时绑定会使 SplitButton.IsChecked 也为 true
        if (this is { } comboBox)
        {
            comboBox.IsDropDownOpen = true;
        }
    }

    private void Flyout_Closed(object sender, object e)
    {
        // 当 Flyout 关闭时，设置 IsDropDownOpen 为 false，
        // SplitButton 的 IsChecked 随之自动更新为 false
        if (this is { } comboBox)
        {
            comboBox.IsDropDownOpen = false;
        }
    }
    private void MultiSelectComboBox_DropDownOpened(object sender, object e)
    {
        if (!_flyout.IsOpen)
        {
            _splitButton.Flyout.ShowAt(_splitButton, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
        }

    }

    private void MultiSelectComboBox_DropDownClosed(object sender, object e)
    {
        if (_flyout.IsOpen)
            _flyout.Hide();
    }
    #endregion

    #region 按钮事件处理

    // 全选按钮点击事件
    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        _multiSelectListView?.SelectAll();
    }

    // 反选按钮点击事件
    private void InvertSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_multiSelectListView == null)
            return;

        // 先复制当前的选中项
        var currentlySelected = _multiSelectListView.SelectedItems.ToList();
        // 遍历所有项
        foreach (var item in _multiSelectListView.Items)
        {
            if (currentlySelected.Contains(item))
            {
                // 已选中项取消选择
                _multiSelectListView.SelectedItems.Remove(item);
            }
            else
            {
                // 未选中项添加选择
                _multiSelectListView.SelectedItems.Add(item);
            }
        }
    }

    #endregion
}

public partial class SelectableItem : INotifyPropertyChanged
{
    public bool IsSelected
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName { get; set; }

    public override string ToString()
    {
        return DisplayName;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}