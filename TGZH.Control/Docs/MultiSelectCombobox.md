# MultiSelectComboBox 说明文档
**注**：本文档由**ChatGPT**AI大语言模型自动生成，可能存在语言表达不准确或不通顺的情况，仅供参考。
详情请自行查看源码。

# 目录

# MultiSelectComboBox 说明文档
**注**：本文档由**ChatGPT**AI大语言模型自动生成，可能存在语言表达不准确或不通顺的情况，仅供参考。
详情请自行查看源码。

# 目录
- [1. 概述](#1-概述)
- [2. 控件特性](#2-控件特性)
- [3. 模板结构说明](#3-模板结构说明)
  - [3.1 ToggleSplitButton](#31-togglesplitbutton)
  - [3.2 Flyout 与 ListView](#32-flyout-与-listview)
- [4. 依赖属性说明](#4-依赖属性说明)
  - [4.1 SelectedItems](#41-selecteditems)
  - [4.2 SelectedItemsText](#42-selecteditemstext)
  - [4.3 IsSelectAllEnabled](#43-isselectallenabled)
  - [4.4 IsInvertSelectionEnabled](#44-isinvertselectionenabled)
  - [4.5 MaxTextWidth](#45-maxtextwidth)
  - [4.6 Separator](#46-separator)
- [5. 事件和内部逻辑](#5-事件和内部逻辑)
  - [5.1 模板加载及控件初始化](#51-模板加载及控件初始化)
  - [5.2 下拉面板显示与关闭](#52-下拉面板显示与关闭)
  - [5.3 列表选择与文本更新](#53-列表选择与文本更新)
  - [5.4 全选与反选按钮](#54-全选与反选按钮)
- [6. 使用示例](#6-使用示例)
  - [6.1 XAML 引用](#61-xaml-引用)
  - [6.2 后台代码示例](#62-后台代码示例)
- [7. 扩展与自定义](#7-扩展与自定义)
- [8. 注意事项](#8-注意事项)

## 1. 概述

**MultiSelectComboBox** 是一个继承自 **ComboBox** 的自定义控件，用于在下拉面板中实现多选功能。与传统的 ComboBox 不同，该控件采用了多选列表的方式，并通过内置的“全选”与“反选”按钮，方便用户对选项进行批量操作。同时，它使用了 **ToggleSplitButton** 与 **Flyout** 结合的模式，实现下拉列表的显示和关闭状态的同步绑定.

---

## 2. 控件特性

- **多选支持**：内置 ListView 控件支持多个选项同时选中.
- **选中项显示**：选中项以字符串形式展示，可通过分隔符自定义显示格式.
- **全选/反选按钮**：可选功能，允许开发者根据需求启用或禁用全选和反选按钮.
- **自定义样式和模板**：控件默认样式采用 ResourceDictionary 定义，并可根据项目需求进行扩展和修改.
- **状态同步**：使用绑定将 ToggleSplitButton 的 IsChecked 与控件的 IsDropDownOpen 进行双向绑定，实现下拉面板打开时按钮呈选中状态、关闭时取消选中.

---

## 3. 模板结构说明

在 XAML 中，控件模板主要由以下部分构成:

### 3.1 ToggleSplitButton

- **作用**：作为主按钮，当用户点击时触发下拉面板（Flyout）的显示.
- **属性绑定**：其 **IsChecked** 属性绑定到了控件的 **IsDropDownOpen** 属性，实现状态同步.
- **内容显示**：内部使用 **TextBlock** 显示当前选中项的文本（通过绑定 **SelectedItemsText**）.### 3.2 Flyout 与 ListView

- **Flyout**：作为下拉面板承载控件，设置了 **Placement** 属性为 `BottomEdgeAlignedLeft`，确保在按钮下方显示.
- **ListView**：用于显示所有可选项，设置 **SelectionMode="Multiple"** 以支持多选，并通过模板绑定获取 **ItemsSource**.---

## 4. 依赖属性说明

### 4.1 SelectedItems

- **类型**：`IList<object>`
- **说明**：用于存储当前选中的项。控件内部通过 ListView 的 **SelectionChanged** 事件更新该属性，并同步更新显示文本.
- **默认值**：`ObservableCollection<object>`

### 4.2 SelectedItemsText

- **类型**：`string`
- **说明**：根据 **SelectedItems** 拼接生成的文本，用于在主按钮中显示当前选中的项。如果没有选中项，默认显示“请选择...”.
- **更新方式**：每当选中项发生变化，内部方法 **UpdateSelectedItemsText** 会被调用以更新该属性.

### 4.3 IsSelectAllEnabled

- **类型**：`bool`
- **说明**：是否启用“全选”按钮的开关。默认为 `false`。在模板中，通过绑定和 **BoolToVisibilityConverter** 控制按钮的可见性.

### 4.4 IsInvertSelectionEnabled

- **类型**：`bool`
- **说明**：是否启用“反选”按钮的开关。默认为 `false`。与 **IsSelectAllEnabled** 逻辑相同.

### 4.5 MaxTextWidth

- **类型**：`double`
- **说明**：主按钮中文本的最大宽度。默认值为 `200.0`.

### 4.6 Separator

- **类型**：`string`
- **说明**：选中项文本拼接时使用的分隔符，默认为“, ”。当分隔符改变时，会自动更新 **SelectedItemsText**.

---

## 5. 事件和内部逻辑

### 5.1 模板加载及控件初始化

- **OnApplyTemplate**：在模板加载时，该方法会查找模板中的子控件（例如 ListView、ToggleSplitButton、Flyout、全选按钮和反选按钮），并为相应控件注册事件.### 5.2 下拉面板显示与关闭

- **Flyout_Opened / Flyout_Closed**  
  在 Flyout 打开和关闭时，分别将控件的 **IsDropDownOpen** 设置为 `true` 或 `false`。由于 **ToggleSplitButton.IsChecked** 与 **IsDropDownOpen** 双向绑定，所以按钮的选中状态会自动同步.- **DropDownOpened / DropDownClosed**  
  控件在收到下拉打开或关闭的事件时，调用对应的 Flyout 显示或隐藏方法，确保 Flyout 的状态与控件一致.### 5.3 列表选择与文本更新

- **MultiSelectListView_SelectionChanged**  
  每当 ListView 中选中的项发生变化时，将更新 **SelectedItems** 属性，并调用 **UpdateSelectedItemsText** 方法拼接显示文本.- **UpdateSelectedItemsText**  
  根据 **SelectedItems** 和 **Separator** 拼接字符串。如果未选中任何项，则显示默认文本“请选择...”.

### 5.4 全选与反选按钮

- **全选**：点击 **SelectAllButton** 后，调用 ListView 的 **SelectAll** 方法，将所有选项标记为选中.- **反选**：点击 **InvertSelectionButton** 后，遍历所有项，对于已经选中的项取消选择，对于未选中的项进行选择，从而实现反选效果.---

## 6. 使用示例

### 6.1 XAML 引用

在项目的 XAML 文件中，通过引用包含 MultiSelectComboBox 样式的 ResourceDictionary 即可使用该控件。例如:### 6.2 后台代码示例

在 ViewModel 或后台代码中设置 ItemsSource、绑定数据等，就可以使用该控件进行多项选择，并且通过 **SelectedItems** 属性获取用户的选择.

---

## 7. 扩展与自定义

- **样式定制**：开发者可以基于 ResourceDictionary 修改控件的默认样式，例如更改 Flyout 的背景色、边框样式或动画效果.
- **功能扩展**：可进一步扩展控件逻辑，如增加搜索功能、选中项排序、显示图标等.
- **行为定制**：利用 **Microsoft.Xaml.Interactivity** 和 **CommunityToolkit.WinUI.Behaviors** 提供的行为，可对控件 Header 区域进行更多定制，例如 Sticky Header 行为.

---

## 8. 注意事项

- **数据同步**：由于内部采用了 ListView 的 SelectedItems 与控件的 **SelectedItems** 属性双向同步，请注意外部修改 **SelectedItems** 时可能会影响 ListView 的状态，确保数据一致性.
- **事件处理**：Flyout 的打开/关闭事件以及 ComboBox 的 DropDownOpened/DropDownClosed 事件需正确注册，防止出现状态不一致的问题.
- **依赖属性默认值**：在注册依赖属性时提供的默认值可能需要根据项目实际需求进行调整，例如 **Separator** 与 **MaxTextWidth**.
## 1. 概述

**MultiSelectComboBox** 是一个继承自 **ComboBox** 的自定义控件，用于在下拉面板中实现多选功能。与传统的 ComboBox 不同，该控件采用了多选列表的方式，并通过内置的“全选”与“反选”按钮，方便用户对选项进行批量操作。同时，它使用了 **ToggleSplitButton** 与 **Flyout** 结合的模式，实现下拉列表的显示和关闭状态的同步绑定.

---

## 2. 控件特性

- **多选支持**：内置 ListView 控件支持多个选项同时选中.
- **选中项显示**：选中项以字符串形式展示，可通过分隔符自定义显示格式.
- **全选/反选按钮**：可选功能，允许开发者根据需求启用或禁用全选和反选按钮.
- **自定义样式和模板**：控件默认样式采用 ResourceDictionary 定义，并可根据项目需求进行扩展和修改.
- **状态同步**：使用绑定将 ToggleSplitButton 的 IsChecked 与控件的 IsDropDownOpen 进行双向绑定，实现下拉面板打开时按钮呈选中状态、关闭时取消选中.

---

## 3. 模板结构说明

在 XAML 中，控件模板主要由以下部分构成:

### 3.1 ToggleSplitButton

- **作用**：作为主按钮，当用户点击时触发下拉面板（Flyout）的显示.
- **属性绑定**：其 **IsChecked** 属性绑定到了控件的 **IsDropDownOpen** 属性，实现状态同步.
- **内容显示**：内部使用 **TextBlock** 显示当前选中项的文本（通过绑定 **SelectedItemsText**）.

```xml
<ToggleSplitButton x:Name="SplitButton"
                   IsTabStop="false"
                   IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"
                   Background="Transparent">
    <!-- 主按钮显示选中项文本 -->
    <ToggleSplitButton.Content>
        <TextBlock Text="{TemplateBinding SelectedItemsText}"
                   VerticalAlignment="Center"
                   TextTrimming="CharacterEllipsis"
                   MaxWidth="{TemplateBinding MaxTextWidth}" />
    </ToggleSplitButton.Content>
    <!-- … -->
</ToggleSplitButton>
```

### 3.2 Flyout 与 ListView

- **Flyout**：作为下拉面板承载控件，设置了 **Placement** 属性为 `BottomEdgeAlignedLeft`，确保在按钮下方显示.
- **ListView**：用于显示所有可选项，设置 **SelectionMode="Multiple"** 以支持多选，并通过模板绑定获取 **ItemsSource**.

```xml
<ToggleSplitButton.Flyout>
    <Flyout Placement="BottomEdgeAlignedLeft" x:Name="Flyout">
        <!-- 选项列表及操作按钮 -->
        <ListView x:Name="MultiSelectListView"
                  ItemsSource="{TemplateBinding ItemsSource}"
                  SelectionMode="Multiple"
                  MaxHeight="{ThemeResource MultiSelectListViewMaxHeight}">
            <ListView.Header>
                <!-- 操作按钮区域 -->
                <Grid Background="{ThemeResource AcrylicInAppFillColorDefaultBrush}">
                    <interactivity:Interaction.Behaviors>
                        <behaviors:StickyHeaderBehavior />
                    </interactivity:Interaction.Behaviors>
                    <StackPanel Orientation="Horizontal" Grid.Row="0">
                        <Button x:Name="SelectAllButton"
                                Content="全选"
                                Margin="4"
                                Visibility="{Binding IsSelectAllEnabled, Converter={StaticResource BoolToVisibilityConverter}, RelativeSource={RelativeSource TemplatedParent}}" />
                        <Button x:Name="InvertSelectionButton"
                                Content="反选"
                                Margin="4"
                                Visibility="{Binding IsInvertSelectionEnabled, Converter={StaticResource BoolToVisibilityConverter}, RelativeSource={RelativeSource TemplatedParent}}" />
                    </StackPanel>
                </Grid>
            </ListView.Header>
        </ListView>
    </Flyout>
</ToggleSplitButton.Flyout>
```

---

## 4. 依赖属性说明

### 4.1 SelectedItems

- **类型**：`IList<object>`
- **说明**：用于存储当前选中的项。控件内部通过 ListView 的 **SelectionChanged** 事件更新该属性，并同步更新显示文本.
- **默认值**：`ObservableCollection<object>`

### 4.2 SelectedItemsText

- **类型**：`string`
- **说明**：根据 **SelectedItems** 拼接生成的文本，用于在主按钮中显示当前选中的项。如果没有选中项，默认显示“请选择...”.
- **更新方式**：每当选中项发生变化，内部方法 **UpdateSelectedItemsText** 会被调用以更新该属性.

### 4.3 IsSelectAllEnabled

- **类型**：`bool`
- **说明**：是否启用“全选”按钮的开关。默认为 `false`。在模板中，通过绑定和 **BoolToVisibilityConverter** 控制按钮的可见性.

### 4.4 IsInvertSelectionEnabled

- **类型**：`bool`
- **说明**：是否启用“反选”按钮的开关。默认为 `false`。与 **IsSelectAllEnabled** 逻辑相同.

### 4.5 MaxTextWidth

- **类型**：`double`
- **说明**：主按钮中文本的最大宽度。默认值为 `200.0`.

### 4.6 Separator

- **类型**：`string`
- **说明**：选中项文本拼接时使用的分隔符，默认为“, ”。当分隔符改变时，会自动更新 **SelectedItemsText**.

---

## 5. 事件和内部逻辑

### 5.1 模板加载及控件初始化

- **OnApplyTemplate**：在模板加载时，该方法会查找模板中的子控件（例如 ListView、ToggleSplitButton、Flyout、全选按钮和反选按钮），并为相应控件注册事件.

```csharp
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
    
    _selectAllButton = GetTemplateChild("SelectAllButton") as Button;
    if (_selectAllButton != null)
    {
        _selectAllButton.Click += SelectAllButton_Click;
    }

    _invertSelectionButton = GetTemplateChild("InvertSelectionButton") as Button;
    if (_invertSelectionButton != null)
    {
        _invertSelectionButton.Click += InvertSelectionButton_Click;
    }
}
```

### 5.2 下拉面板显示与关闭

- **Flyout_Opened / Flyout_Closed**  
  在 Flyout 打开和关闭时，分别将控件的 **IsDropDownOpen** 设置为 `true` 或 `false`。由于 **ToggleSplitButton.IsChecked** 与 **IsDropDownOpen** 双向绑定，所以按钮的选中状态会自动同步.

```csharp
private void Flyout_Opened(object sender, object e)
{
    this.IsDropDownOpen = true;
}

private void Flyout_Closed(object sender, object e)
{
    this.IsDropDownOpen = false;
}
```

- **DropDownOpened / DropDownClosed**  
  控件在收到下拉打开或关闭的事件时，调用对应的 Flyout 显示或隐藏方法，确保 Flyout 的状态与控件一致.

```csharp
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
```

### 5.3 列表选择与文本更新

- **MultiSelectListView_SelectionChanged**  
  每当 ListView 中选中的项发生变化时，将更新 **SelectedItems** 属性，并调用 **UpdateSelectedItemsText** 方法拼接显示文本.

```csharp
private void MultiSelectListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (_multiSelectListView == null) return;
    SelectedItems = _multiSelectListView.SelectedItems;
    UpdateSelectedItemsText();
}
```

- **UpdateSelectedItemsText**  
  根据 **SelectedItems** 和 **Separator** 拼接字符串。如果未选中任何项，则显示默认文本“请选择...”.

### 5.4 全选与反选按钮

- **全选**：点击 **SelectAllButton** 后，调用 ListView 的 **SelectAll** 方法，将所有选项标记为选中.

```csharp
private void SelectAllButton_Click(object sender, RoutedEventArgs e)
{
    _multiSelectListView?.SelectAll();
}
```

- **反选**：点击 **InvertSelectionButton** 后，遍历所有项，对于已经选中的项取消选择，对于未选中的项进行选择，从而实现反选效果.

```csharp
private void InvertSelectionButton_Click(object sender, RoutedEventArgs e)
{
    if (_multiSelectListView == null)
        return;

    var currentlySelected = _multiSelectListView.SelectedItems.ToList();
    foreach (var item in _multiSelectListView.Items)
    {
        if (currentlySelected.Contains(item))
        {
            _multiSelectListView.SelectedItems.Remove(item);
        }
        else
        {
            _multiSelectListView.SelectedItems.Add(item);
        }
    }
}
```

---

## 6. 使用示例

### 6.1 XAML 引用

在项目的 XAML 文件中，通过引用包含 MultiSelectComboBox 样式的 ResourceDictionary 即可使用该控件。例如:

```xaml
<Page
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:TGZH.Control">

    <Grid>
        <local:MultiSelectComboBox Width="300"
                                   ItemsSource="{Binding YourItems}"
                                   IsSelectAllEnabled="True"
                                   IsInvertSelectionEnabled="True"
                                   Separator="; " />
    </Grid>
</Page>
```

### 6.2 后台代码示例

在 ViewModel 或后台代码中设置 ItemsSource、绑定数据等，就可以使用该控件进行多项选择，并且通过 **SelectedItems** 属性获取用户的选择.

---

## 7. 扩展与自定义

- **样式定制**：开发者可以基于 ResourceDictionary 修改控件的默认样式，例如更改 Flyout 的背景色、边框样式或动画效果.
- **功能扩展**：可进一步扩展控件逻辑，如增加搜索功能、选中项排序、显示图标等.
- **行为定制**：利用 **Microsoft.Xaml.Interactivity** 和 **CommunityToolkit.WinUI.Behaviors** 提供的行为，可对控件 Header 区域进行更多定制，例如 Sticky Header 行为.

---

## 8. 注意事项

- **数据同步**：由于内部采用了 ListView 的 SelectedItems 与控件的 **SelectedItems** 属性双向同步，请注意外部修改 **SelectedItems** 时可能会影响 ListView 的状态，确保数据一致性.
- **事件处理**：Flyout 的打开/关闭事件以及 ComboBox 的 DropDownOpened/DropDownClosed 事件需正确注册，防止出现状态不一致的问题.
- **依赖属性默认值**：在注册依赖属性时提供的默认值可能需要根据项目实际需求进行调整，例如 **Separator** 与 **MaxTextWidth**.

