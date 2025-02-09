# EditableTextBlock 说明文档
**注**：本文档由 **ChatGPT** AI 大语言模型自动生成，可能存在语言表达不准确或不通顺的情况，仅供参考。详情请自行查看源码。

# 目录
- [1. 概述](#1-概述)
- [2. 控件特性](#2-控件特性)
- [3. 模板结构说明](#3-模板结构说明)
  - [3.1 只读与编辑显示区域](#31-只读与编辑显示区域)
- [4. 依赖属性说明](#4-依赖属性说明)
  - [4.1 Text](#41-text)
  - [4.2 IsRequired](#42-isrequired)
  - [4.3 ErrorMessage](#43-errormessage)
  - [4.4 IsMultiLine](#44-ismultiline)
  - [4.5 Placeholder](#45-placeholder)
- [5. 事件和内部逻辑](#5-事件和内部逻辑)
  - [5.1 模板加载及控件初始化](#51-模板加载及控件初始化)
  - [5.2 状态切换与编辑流程](#52-状态切换与编辑流程)
  - [5.3 键盘快捷键处理](#53-键盘快捷键处理)
- [6. 使用示例](#6-使用示例)
  - [6.1 XAML 引用](#61-xaml-引用)
  - [6.2 后台代码示例](#62-后台代码示例)
- [7. 扩展与自定义](#7-扩展与自定义)
- [8. 注意事项](#8-注意事项)

## 1. 概述

**EditableTextBlock** 是一个继承自 **Control** 的自定义控件，用于在只读状态下显示文本，并在需要时切换到编辑状态以修改文本。控件内部通过 VisualStateManager 管理只读与编辑状态的切换，同时支持输入验证、键盘快捷键、多行编辑和占位符功能。

---

## 2. 控件特性

- **双状态显示**：在只读状态下通过 TextBlock 显示文本，在编辑状态下通过 TextBox 提供文本编辑功能。
- **输入验证**：支持必填项验证，验证失败时显示错误提示信息。
- **键盘快捷键**：在编辑状态下，按 Enter 键保存文本，按 Escape 键取消编辑。
- **多行编辑**：可通过设置 IsMultiLine 属性启用多行输入及自动换行。
- **占位符支持**：在编辑状态下显示占位符文本，提示用户输入。

---

## 3. 模板结构说明

控件模板定义在 Themes/generic.xaml 中，主要由以下部分构成：

### 3.1 只读与编辑显示区域

- **只读区域**：由 `PART_DisplayTextBlock` 定义，使用 TextBlock 显示当前文本，仅在只读状态下可见。
- **编辑区域**：由 `PART_EditTextBox` 定义，使用 TextBox 提供文本输入，仅在编辑状态下可见。
- **辅助按钮**：包括 `PART_EditButton`（进入编辑状态）、`PART_SaveButton` 与 `PART_CancelButton`（分别用于保存和取消编辑）。
- **错误提示**：由 `PART_ErrorTextBlock` 定义，用于显示输入验证失败时的错误信息。

---

## 4. 依赖属性说明

### 4.1 Text
- **类型**：`string`
- **说明**：用于绑定和存储控件显示或编辑的文本内容。

### 4.2 IsRequired
- **类型**：`bool`
- **说明**：指示文本是否为必填项。当设置为 true 时，保存操作会验证文本不为空。

### 4.3 ErrorMessage
- **类型**：`string`
- **说明**：当输入验证失败时显示的错误提示信息。

### 4.4 IsMultiLine
- **类型**：`bool`
- **说明**：指示编辑区域是否支持多行输入。设置为 true 时，TextBox 将启用自动换行。

### 4.5 Placeholder
- **类型**：`string`
- **说明**：在编辑状态下，TextBox 显示的占位符文本，用于提示用户输入。

### 4.6 InteractionMode
- **类型**：`EditableTextBlockInteractionMode` 枚举
- **说明**：指示控件的交互模式，指定进入编辑状态的方式。可选值包括 `DoubleClick`（双击文本区域进入编辑，失焦后自动保存，按`Esc`键取消）和 `Button`（通过按钮进入编辑、保存及取消）。

---

## 5. 事件和内部逻辑

### 5.1 模板加载及控件初始化
- **OnApplyTemplate() 方法**  
  在模板加载时，控件通过此方法获取模板中定义的各部件（例如 `PART_DisplayTextBlock`、`PART_EditTextBox` 等），并为按钮和键盘事件注册处理程序。同时，将依赖属性的初始值同步到各控件部件。
```csharp
protected override void OnApplyTemplate()
{
    base.OnApplyTemplate();

    // 解除旧模板的事件绑定
    if (_editButton != null)
        _editButton.Click -= EditButton_Click;
    if (_saveButton != null)
        _saveButton.Click -= SaveButton_Click;
    if (_cancelButton != null)
        _cancelButton.Click -= CancelButton_Click;
    if (_editTextBox != null)
        _editTextBox.KeyDown -= EditTextBox_KeyDown;

    // 获取模板部件引用
    _displayTextBlock = GetTemplateChild(PartDisplayTextBlock) as TextBlock;
    _editTextBox = GetTemplateChild(PartEditTextBox) as TextBox;
    _editButton = GetTemplateChild(PartEditButton) as Button;
    _saveButton = GetTemplateChild(PartSaveButton) as Button;
    _cancelButton = GetTemplateChild(PartCancelButton) as Button;
    _errorTextBlock = GetTemplateChild(PartErrorTextBlock) as TextBlock;

    // 同步显示文本
    if (_displayTextBlock != null)
        _displayTextBlock.Text = this.Text;
    if (_editTextBox != null)
    {
        _editTextBox.Text = this.Text;
        _editTextBox.PlaceholderText = this.Placeholder;
        _editTextBox.AcceptsReturn = IsMultiLine;
        _editTextBox.TextWrapping = IsMultiLine ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _editTextBox.KeyDown += EditTextBox_KeyDown;
    }
    if (_errorTextBlock != null)
    {
        _errorTextBlock.Text = this.ErrorMessage;
    }

    // 绑定按钮点击事件
    if (_editButton != null)
        _editButton.Click += EditButton_Click;
    if (_saveButton != null)
        _saveButton.Click += SaveButton_Click;
    if (_cancelButton != null)
        _cancelButton.Click += CancelButton_Click;

    // 初始进入默认状态（并验证）
    VisualStateManager.GoToState(this, "Valid", false);
    UpdateVisualStates(false);
}
```

### 5.2 状态切换与编辑流程
- **进入编辑模式**：调用 `EnterEditMode()` 方法，设置内部标志 `_isEditing = true`，同步 TextBox 中的文本、设置焦点并清除错误信息；随后，通过 VisualStateManager 切换至编辑状态，并触发 `EditingStarted` 事件。
```csharp
private void EnterEditMode()
{
    _isEditing = true;
    if (_editTextBox != null)
    {
        _editTextBox.Text = this.Text;
        _editTextBox.Focus(FocusState.Programmatic);
        _editTextBox.Select(_editTextBox.Text.Length, 0);
    }
    // 重置错误信息
    ErrorMessage = string.Empty;
    VisualStateManager.GoToState(this, "Valid", true);

    UpdateVisualStates(true);

    EditingStarted?.Invoke(this, new RoutedEventArgs());
}
```
- **保存操作**：调用 `SaveEdit()` 方法，首先执行 `ValidateText()` 进行输入验证；验证通过后，同步 TextBox 内容到控件的 Text 属性，更新只读显示区域，切换回只读状态并触发 `EditingCompleted` 事件；若验证失败，则保持编辑状态，并显示验证错误状态。
```csharp
private void SaveEdit()
{
    if (_editTextBox != null)
    {
        // 先验证输入
        if (!ValidateText())
        {
            VisualStateManager.GoToState(this, "ValidationError", true);
            return; // 验证失败则保持编辑状态
        }
    }

    _isEditing = false;
    if (_editTextBox != null)
    {
        this.Text = _editTextBox.Text;
    }
    if (_displayTextBlock != null)
    {
        _displayTextBlock.Text = this.Text;
    }
    // 验证通过后恢复为有效状态
    VisualStateManager.GoToState(this, "Valid", true);
    UpdateVisualStates(true);

    EditingCompleted?.Invoke(this, new RoutedEventArgs());
}
```
- **取消操作**：调用 `CancelEdit()` 方法，直接退出编辑状态，不更新 Text 属性，并触发 `EditingCompleted` 事件。
```csharp
private void CancelEdit()
{
    _isEditing = false;
    VisualStateManager.GoToState(this, "Valid", true);
    UpdateVisualStates(true);

    EditingCompleted?.Invoke(this, new RoutedEventArgs());
}
```

- **输入验证**：在 `ValidateText()` 方法中，根据 IsRequired 属性判断是否为必填项，若为空则设置错误信息并返回 false；否则清空错误信息并返回 true。
```csharp
protected virtual bool ValidateText()
{
    if (IsRequired && string.IsNullOrWhiteSpace(_editTextBox?.Text))
    {
        ErrorMessage = "此项为必填项。";
        if (_errorTextBlock != null)
            _errorTextBlock.Text = ErrorMessage;
        return false;
    }
    ErrorMessage = string.Empty;
    if (_errorTextBlock != null)
        _errorTextBlock.Text = ErrorMessage;
    return true;
}
```

### 5.3 键盘快捷键处理
- 在 TextBox 的 KeyDown 事件中：
  - 按 **Enter** 键触发 `SaveEdit()` 保存操作；
  - 按 **Escape** 键触发 `CancelEdit()` 取消编辑。

---

## 6. 使用示例

### 6.1 XAML 引用

```xml
<Page
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:YourNamespace">
  <Grid>
    <local:EditableTextBlock 
        Text="{Binding MyText}"
        IsRequired="True"
        Placeholder="请输入文本..."
        IsMultiLine="True" />
  </Grid>
</Page>
```
### 6.2 后台代码示例
```csharp
复制
// 示例：在保存操作前调用 ValidateText() 方法进行输入验证
if (editableTextBlock.ValidateText())
{
    editableTextBlock.SaveEdit();
}
else
{
    // 输入验证失败，提示用户修改错误
}
```
## 7. 扩展与自定义
样式定制：开发者可通过修改 ResourceDictionary 中的默认样式，定制控件外观（如按钮样式、文本颜色、背景等）。
功能扩展：可扩展输入验证逻辑，通过重写 ValidateText() 方法实现更复杂的验证规则。
行为定制：根据业务需求，可添加额外的交互逻辑或视觉状态，进一步丰富控件功能。
## 8. 注意事项
事件绑定：确保在 OnApplyTemplate 中正确绑定各部件的事件，防止因重复绑定而导致内存泄露或逻辑错误。
状态同步：使用 VisualStateManager 管理状态切换，确保只读与编辑状态平滑过渡且状态一致。
输入验证：默认验证逻辑仅检查必填项，若需更复杂的验证，请重写 ValidateText() 方法。
数据绑定：在使用控件时，注意确保 Text 属性与外部数据的双向绑定正常工作，以避免显示与数据不一致的情况。