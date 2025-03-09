using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Windows.System;

namespace TGZH.Control;

/// <summary>
/// 可编辑文本块控件，支持只读显示和编辑两种状态
/// </summary>
[TemplatePart(Name = "PART_DisplayContainer", Type = typeof(Grid))]
[TemplatePart(Name = "PART_DisplayTextBlock", Type = typeof(TextBlock))]
[TemplatePart(Name = "PART_EditTextBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_EditButton", Type = typeof(Button))]
[TemplatePart(Name = "PART_SaveButton", Type = typeof(Button))]
[TemplatePart(Name = "PART_CancelButton", Type = typeof(Button))]
[TemplatePart(Name = "PART_ErrorTextBlock", Type = typeof(TextBlock))]
public partial class EditableTextBlock : Microsoft.UI.Xaml.Controls.Control
{
    #region 私有成员

    private Grid _displayContainer;
    private TextBlock _displayTextBlock;
    private TextBox _editTextBox;
    private Button _editButton;
    private Button _saveButton;
    private Button _cancelButton;
    private TextBlock _errorTextBlock;
    private string _originalText;
    private bool _isInEditMode;
    private bool _templateApplied;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化 EditableTextBlock 的新实例
    /// </summary>
    public EditableTextBlock()
    {
        DefaultStyleKey = typeof(EditableTextBlock);
        Loaded += EditableTextBlock_Loaded;
    }

    #endregion


    #region 依赖属性

    /// <summary>
    /// 定义 Text 依赖属性
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(EditableTextBlock),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// 获取或设置控件的文本内容
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// 定义 IsRequired 依赖属性
    /// </summary>
    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(EditableTextBlock),
            new PropertyMetadata(false));

    /// <summary>
    /// 获取或设置一个值，该值指示文本是否为必填项
    /// </summary>
    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    /// <summary>
    /// 定义 ErrorMessage 依赖属性
    /// </summary>
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(EditableTextBlock),
            new PropertyMetadata("此字段为必填项。"));

    /// <summary>
    /// 获取或设置当输入验证失败时显示的错误提示信息
    /// </summary>
    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <summary>
    /// 定义 IsMultiLine 依赖属性
    /// </summary>
    public static readonly DependencyProperty IsMultiLineProperty =
        DependencyProperty.Register(nameof(IsMultiLine), typeof(bool), typeof(EditableTextBlock),
            new PropertyMetadata(false, OnIsMultiLinePropertyChanged));

    private static void OnIsMultiLinePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EditableTextBlock control && control._editTextBox != null)
        {
            control.UpdateMultiLineSettings();
        }
    }

    /// <summary>
    /// 获取或设置一个值，该值指示编辑区域是否支持多行输入
    /// </summary>
    public bool IsMultiLine
    {
        get => (bool)GetValue(IsMultiLineProperty);
        set => SetValue(IsMultiLineProperty, value);
    }

    /// <summary>
    /// 定义 Placeholder 依赖属性
    /// </summary>
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(EditableTextBlock),
            new PropertyMetadata(string.Empty, OnPlaceholderPropertyChanged));

    private static void OnPlaceholderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EditableTextBlock control && control._editTextBox != null)
        {
            control._editTextBox.PlaceholderText = (string)e.NewValue;
        }
    }

    /// <summary>
    /// 获取或设置在编辑状态下显示的占位符文本
    /// </summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// 定义 InteractionMode 依赖属性
    /// </summary>
    public static readonly DependencyProperty InteractionModeProperty =
        DependencyProperty.Register(nameof(InteractionMode), typeof(EditableTextBlockInteractionMode),
            typeof(EditableTextBlock), new PropertyMetadata(EditableTextBlockInteractionMode.Button,
                OnInteractionModePropertyChanged));

    private static void OnInteractionModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EditableTextBlock { _templateApplied: true } control)
        {
            control.UpdateInteractionMode();
        }
    }

    /// <summary>
    /// 获取或设置控件的交互模式
    /// </summary>
    public EditableTextBlockInteractionMode InteractionMode
    {
        get => (EditableTextBlockInteractionMode)GetValue(InteractionModeProperty);
        set => SetValue(InteractionModeProperty, value);
    }

    /// <summary>
    /// 定义 Header 依赖属性
    /// </summary>
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置控件的标题内容
    /// </summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// 定义 HeaderTemplate 依赖属性
    /// </summary>
    public static readonly DependencyProperty HeaderTemplateProperty =
        DependencyProperty.Register(nameof(HeaderTemplate), typeof(DataTemplate), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置标题的数据模板
    /// </summary>
    public DataTemplate HeaderTemplate
    {
        get => (DataTemplate)GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>
    /// 定义 HeaderVisibility 依赖属性
    /// </summary>
    public static readonly DependencyProperty HeaderVisibilityProperty =
        DependencyProperty.Register(nameof(HeaderVisibility), typeof(Visibility), typeof(EditableTextBlock),
            new PropertyMetadata(Visibility.Visible));

    /// <summary>
    /// 获取或设置标题的可见性
    /// </summary>
    public Visibility HeaderVisibility
    {
        get => (Visibility)GetValue(HeaderVisibilityProperty);
        set => SetValue(HeaderVisibilityProperty, value);
    }

    /// <summary>
    /// 定义 EditButtonStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty EditButtonStyleProperty =
        DependencyProperty.Register(nameof(EditButtonStyle), typeof(Style), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置编辑按钮的样式
    /// </summary>
    public Style EditButtonStyle
    {
        get => (Style)GetValue(EditButtonStyleProperty);
        set => SetValue(EditButtonStyleProperty, value);
    }

    /// <summary>
    /// 定义 SaveButtonStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty SaveButtonStyleProperty =
        DependencyProperty.Register(nameof(SaveButtonStyle), typeof(Style), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置保存按钮的样式
    /// </summary>
    public Style SaveButtonStyle
    {
        get => (Style)GetValue(SaveButtonStyleProperty);
        set => SetValue(SaveButtonStyleProperty, value);
    }

    /// <summary>
    /// 定义 CancelButtonStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty CancelButtonStyleProperty =
        DependencyProperty.Register(nameof(CancelButtonStyle), typeof(Style), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置取消按钮的样式
    /// </summary>
    public Style CancelButtonStyle
    {
        get => (Style)GetValue(CancelButtonStyleProperty);
        set => SetValue(CancelButtonStyleProperty, value);
    }

    /// <summary>
    /// 定义 ErrorTextBlockStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty ErrorTextBlockStyleProperty =
        DependencyProperty.Register(nameof(ErrorTextBlockStyle), typeof(Style), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置错误提示文本块的样式
    /// </summary>
    public Style ErrorTextBlockStyle
    {
        get => (Style)GetValue(ErrorTextBlockStyleProperty);
        set => SetValue(ErrorTextBlockStyleProperty, value);
    }

    /// <summary>
    /// 定义 DisplayTextBlockStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty DisplayTextBlockStyleProperty =
        DependencyProperty.Register(nameof(DisplayTextBlockStyle), typeof(Style), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置显示文本块的样式
    /// </summary>
    public Style DisplayTextBlockStyle
    {
        get => (Style)GetValue(DisplayTextBlockStyleProperty);
        set => SetValue(DisplayTextBlockStyleProperty, value);
    }

    /// <summary>
    /// 定义 EditTextBoxStyle 依赖属性
    /// </summary>
    public static readonly DependencyProperty EditTextBoxStyleProperty =
        DependencyProperty.Register(nameof(EditTextBoxStyle), typeof(Style), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置编辑文本框的样式
    /// </summary>
    public Style EditTextBoxStyle
    {
        get => (Style)GetValue(EditTextBoxStyleProperty);
        set => SetValue(EditTextBoxStyleProperty, value);
    }

    /// <summary>
    /// 定义 SaveCommand 依赖属性
    /// </summary>
    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(nameof(SaveCommand), typeof(ICommand), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置保存操作的命令
    /// </summary>
    public ICommand SaveCommand
    {
        get => (ICommand)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    /// <summary>
    /// 定义 CancelCommand 依赖属性
    /// </summary>
    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置取消操作的命令
    /// </summary>
    public ICommand CancelCommand
    {
        get => (ICommand)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    /// <summary>
    /// 定义 EditCommand 依赖属性
    /// </summary>
    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(nameof(EditCommand), typeof(ICommand), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置进入编辑状态的命令
    /// </summary>
    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    /// <summary>
    /// 定义 SaveCommandParameter 依赖属性
    /// </summary>
    public static readonly DependencyProperty SaveCommandParameterProperty =
        DependencyProperty.Register(nameof(SaveCommandParameter), typeof(object), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置保存命令的参数
    /// </summary>
    public object SaveCommandParameter
    {
        get => GetValue(SaveCommandParameterProperty);
        set => SetValue(SaveCommandParameterProperty, value);
    }

    /// <summary>
    /// 定义 CancelCommandParameter 依赖属性
    /// </summary>
    public static readonly DependencyProperty CancelCommandParameterProperty =
        DependencyProperty.Register(nameof(CancelCommandParameter), typeof(object), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置取消命令的参数
    /// </summary>
    public object CancelCommandParameter
    {
        get => GetValue(CancelCommandParameterProperty);
        set => SetValue(CancelCommandParameterProperty, value);
    }

    /// <summary>
    /// 定义 EditCommandParameter 依赖属性
    /// </summary>
    public static readonly DependencyProperty EditCommandParameterProperty =
        DependencyProperty.Register(nameof(EditCommandParameter), typeof(object), typeof(EditableTextBlock),
            new PropertyMetadata(null));

    /// <summary>
    /// 获取或设置编辑命令的参数
    /// </summary>
    public object EditCommandParameter
    {
        get => GetValue(EditCommandParameterProperty);
        set => SetValue(EditCommandParameterProperty, value);
    }

    #endregion

    #region 事件

    /// <summary>
    /// 编辑开始事件
    /// </summary>
    public event RoutedEventHandler EditingStarted;

    /// <summary>
    /// 编辑完成事件
    /// </summary>
    public event RoutedEventHandler EditingCompleted;

    #endregion

    #region 重写方法

    /// <summary>
    /// 当应用模板时调用
    /// </summary>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 清除之前的事件处理程序
        UnregisterEvents();

        // 获取模板部件
        _displayContainer = GetTemplateChild("PART_DisplayContainer") as Grid;
        _displayTextBlock = GetTemplateChild("PART_DisplayTextBlock") as TextBlock;
        _editTextBox = GetTemplateChild("PART_EditTextBox") as TextBox;
        _editButton = GetTemplateChild("PART_EditButton") as Button;
        _saveButton = GetTemplateChild("PART_SaveButton") as Button;
        _cancelButton = GetTemplateChild("PART_CancelButton") as Button;
        _errorTextBlock = GetTemplateChild("PART_ErrorTextBlock") as TextBlock;

        // 确保找到了所有必要的元素
        if (_editButton == null)
        {
            Debug.WriteLine("警告: 无法找到编辑按钮 PART_EditButton");
        }

        // 注册事件
        RegisterEvents();

        // 初始化控件状态
        UpdateMultiLineSettings();
        UpdateInteractionMode();
        VisualStateManager.GoToState(this, "ReadOnlyState", false);

        _templateApplied = true;
    }

    #endregion

    #region 私有方法

    private void EditableTextBlock_Loaded(object sender, RoutedEventArgs e)
    {
        // 如果控件已经加载过模板，确保状态正确
        if (!_templateApplied) return;
        UpdateMultiLineSettings();
        UpdateInteractionMode();
    }

    private void UpdateMultiLineSettings()
    {
        if (_editTextBox == null) return;

        _editTextBox.AcceptsReturn = IsMultiLine;
        _editTextBox.TextWrapping = IsMultiLine ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void UpdateInteractionMode()
    {
        if (_displayTextBlock == null) return;

        // 根据交互模式设置事件处理
        if (InteractionMode == EditableTextBlockInteractionMode.DoubleClick)
        {
            _displayTextBlock.DoubleTapped += DisplayTextBlock_DoubleTapped;
            if (_editButton != null) _editButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            _displayTextBlock.DoubleTapped -= DisplayTextBlock_DoubleTapped;
            if (_editButton != null) _editButton.Visibility = Visibility.Visible;
        }
    }

    private void RegisterEvents()
    {
        if (_displayTextBlock != null)
        {
            UpdateInteractionMode();
        }

        if (_displayContainer != null)
        {
            // 添加鼠标悬停事件处理到 DisplayContainer
            _displayContainer.PointerEntered += DisplayContainer_PointerEntered;
            _displayContainer.PointerExited += DisplayContainer_PointerExited;
            EditingStarted += OnEditingStarted;
        }

        if (_editTextBox != null)
        {
            _editTextBox.LostFocus += EditTextBox_LostFocus;
            _editTextBox.KeyDown += EditTextBox_KeyDown;
            _editTextBox.PlaceholderText = Placeholder;
        }

        if (_editButton != null)
        {
            // 先确保没有重复注册事件
            _editButton.Click -= EditButton_Click;
            _editButton.Click += EditButton_Click;

            // 为编辑按钮也添加鼠标事件
            _editButton.PointerEntered += EditButton_PointerEntered;
        }

        if (_saveButton != null)
        {
            _saveButton.Click -= SaveButton_Click;
            _saveButton.Click += SaveButton_Click;
        }

        if (_cancelButton != null)
        {
            _cancelButton.Click -= CancelButton_Click;
            _cancelButton.Click += CancelButton_Click;
        }
    }

    private void UnregisterEvents()
    {
        if (_displayTextBlock != null)
        {
            _displayTextBlock.DoubleTapped -= DisplayTextBlock_DoubleTapped;
        }

        if (_displayContainer != null)
        {
            _displayContainer.PointerEntered -= DisplayContainer_PointerEntered;
            _displayContainer.PointerExited -= DisplayContainer_PointerExited;
            EditingStarted -= OnEditingStarted;
        }

        if (_editTextBox != null)
        {
            _editTextBox.LostFocus -= EditTextBox_LostFocus;
            _editTextBox.KeyDown -= EditTextBox_KeyDown;
        }

        if (_editButton != null)
        {
            _editButton.Click -= EditButton_Click;
            _editButton.PointerEntered -= EditButton_PointerEntered;
        }

        if (_saveButton != null)
        {
            _saveButton.Click -= SaveButton_Click;
        }

        if (_cancelButton != null)
        {
            _cancelButton.Click -= CancelButton_Click;
        }
    }

    // 编辑开始事件处理
    private void OnEditingStarted(object sender, RoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    // 鼠标悬停事件处理
    private void DisplayContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void DisplayContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        // 使用一个小延迟来检查是否移到了编辑按钮上
        // 这里不能直接切换状态，因为可能是移到了编辑按钮上
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            // 检查鼠标是否在编辑按钮上
            if (_editButton != null && _editButton.IsPointerOver)
            {
                return;
            }

            VisualStateManager.GoToState(this, "Normal", true);
        });
    }

    private void EditButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // 确保按钮保持显示状态
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void DisplayTextBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        EnterEditMode();
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        EnterEditMode();

        // 执行编辑命令
        if (EditCommand?.CanExecute(EditCommandParameter) == true)
        {
            EditCommand.Execute(EditCommandParameter);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveChanges();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelEdit();

        // 执行取消命令
        if (CancelCommand?.CanExecute(CancelCommandParameter) == true)
        {
            CancelCommand.Execute(CancelCommandParameter);
        }
    }

    private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // 如果是DoubleClick模式且编辑框失去焦点，自动保存
        if (InteractionMode == EditableTextBlockInteractionMode.DoubleClick && _isInEditMode)
        {
            SaveChanges();
        }
    }

    [SuppressMessage("ReSharper", "SwitchStatementHandlesSomeKnownEnumValuesWithDefault")]
    private void EditTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Escape:
                CancelEdit();
                break;
            case VirtualKey.Enter when !IsMultiLine:
                SaveChanges();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void EnterEditMode()
    {
        if (_isInEditMode) return;

        _originalText = Text;
        if (_editTextBox != null)
        {
            _editTextBox.Text = _originalText;
        }

        // 直接管理容器的可见性，而不是依赖 VisualStateManager
        if (_displayTextBlock != null && _editTextBox != null)
        {
            if (_displayTextBlock.Parent is Grid displayContainer)
            {
                displayContainer.Visibility = Visibility.Collapsed;
            }

            if (_editTextBox.Parent is Grid editContainer)
            {
                editContainer.Visibility = Visibility.Visible;
            }
        }

        // 尝试直接应用视觉状态到当前控件
        _ = VisualStateManager.GoToState(this, "EditState", true);

        _isInEditMode = true;

        if (_errorTextBlock != null)
        {
            _errorTextBlock.Visibility = Visibility.Collapsed;
        }

        // 设置焦点到文本框
        _editTextBox?.Focus(FocusState.Programmatic);

        // 触发编辑开始事件
        EditingStarted?.Invoke(this, new RoutedEventArgs());
    }


    private void SaveChanges()
    {
        if (!_isInEditMode || _editTextBox == null) return;

        var newText = _editTextBox.Text;

        // 验证必填项
        if (IsRequired && string.IsNullOrWhiteSpace(newText))
        {
            if (_errorTextBlock == null) return;
            _errorTextBlock.Text = ErrorMessage;
            _errorTextBlock.Visibility = Visibility.Visible;

            return;
        }

        // 更新文本
        Text = newText;
        ExitEditMode();

        // 执行保存命令
        if (SaveCommand?.CanExecute(SaveCommandParameter) == true)
        {
            SaveCommand.Execute(SaveCommandParameter);
        }
    }

    private void CancelEdit()
    {
        if (!_isInEditMode) return;

        // 恢复原始文本
        if (_editTextBox != null)
        {
            _editTextBox.Text = _originalText;
        }

        ExitEditMode();
    }

    private void ExitEditMode()
    {
        // 直接管理容器的可见性，而不是依赖 VisualStateManager
        if (_displayTextBlock != null && _editTextBox != null)
        {
            if (_displayTextBlock.Parent is Grid displayContainer)
            {
                displayContainer.Visibility = Visibility.Visible;
            }

            if (_editTextBox.Parent is Grid editContainer)
            {
                editContainer.Visibility = Visibility.Collapsed;
            }
        }

        // 尝试直接应用视觉状态到当前控件
        VisualStateManager.GoToState(this, "ReadOnlyState", true);

        _isInEditMode = false;

        if (_errorTextBlock != null)
        {
            _errorTextBlock.Visibility = Visibility.Collapsed;
        }

        // 触发编辑完成事件
        EditingCompleted?.Invoke(this, new RoutedEventArgs());
    }

    #endregion
}