using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Windows.Input;
using Windows.System;

// ICommand

namespace TGZH.Control;

/// <summary>
/// 定义交互模式：按钮模式 或 双击模式
/// </summary>
public enum EditableTextBlockInteractionMode
{
    Button,
    DoubleClick
}

public partial class EditableTextBlock : Microsoft.UI.Xaml.Controls.Control
{
    #region 依赖属性

    // 显示或编辑的文本内容
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(EditableTextBlock), new PropertyMetadata(string.Empty));
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 编辑命令
    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(nameof(EditCommand), typeof(ICommand), typeof(EditableTextBlock), new PropertyMetadata(null));
    public ICommand EditCommand
    {
        get => (ICommand)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    // 保存命令
    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(nameof(SaveCommand), typeof(ICommand), typeof(EditableTextBlock), new PropertyMetadata(null));
    public ICommand SaveCommand
    {
        get => (ICommand)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    // 取消命令
    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(EditableTextBlock), new PropertyMetadata(null));
    public ICommand CancelCommand
    {
        get => (ICommand)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    // 是否必须输入（验证用）
    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false));
    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    // 错误提示信息（验证失败时显示）
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(EditableTextBlock), new PropertyMetadata(string.Empty));
    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    // 是否启用多行编辑
    public static readonly DependencyProperty IsMultiLineProperty =
        DependencyProperty.Register(nameof(IsMultiLine), typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false));
    public bool IsMultiLine
    {
        get => (bool)GetValue(IsMultiLineProperty);
        set => SetValue(IsMultiLineProperty, value);
    }

    // 占位符文本
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(EditableTextBlock), new PropertyMetadata(string.Empty));
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    // 交互模式：按钮模式或双击模式，默认按钮模式
    public static readonly DependencyProperty InteractionModeProperty =
        DependencyProperty.Register(nameof(InteractionMode), typeof(EditableTextBlockInteractionMode), typeof(EditableTextBlock),
            new PropertyMetadata(EditableTextBlockInteractionMode.Button, OnInteractionModeChanged));
    public EditableTextBlockInteractionMode InteractionMode
    {
        get => (EditableTextBlockInteractionMode)GetValue(InteractionModeProperty);
        set => SetValue(InteractionModeProperty, value);
    }
    private static void OnInteractionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as EditableTextBlock;
        if (control == null) return;
        switch (control.InteractionMode)
        {
            // 按钮模式下使用鼠标进入/离开切换视觉状态
            case EditableTextBlockInteractionMode.Button:
                control.PointerEntered -= control.EditableTextBlock_PointerEntered;
                control.PointerExited -= control.EditableTextBlock_PointerExited;
                control.DoubleTapped -= control.EditableTextBlock_DoubleTapped;
                control.PointerEntered += control.EditableTextBlock_PointerEntered;
                control.PointerExited += control.EditableTextBlock_PointerExited;
                break;
            // 双击模式下使用双击切换编辑状态
            case EditableTextBlockInteractionMode.DoubleClick:
                control.PointerEntered -= control.EditableTextBlock_PointerEntered;
                control.PointerExited -= control.EditableTextBlock_PointerExited;
                control.DoubleTapped -= control.EditableTextBlock_DoubleTapped;
                control.DoubleTapped += control.EditableTextBlock_DoubleTapped;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(d));
        }
    }

    /// <summary>
    /// 控件的标题或说明内容，可以是字符串或者任意 UI 元素。
    /// </summary>
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(object),
            typeof(EditableTextBlock),
            new PropertyMetadata(null));

    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    #endregion

    #region 模板部件名称

    private const string PartDisplayTextBlock = "PART_DisplayTextBlock";
    private const string PartEditTextBox = "PART_EditTextBox";
    private const string PartEditButton = "PART_EditButton";
    private const string PartSaveButton = "PART_SaveButton";
    private const string PartCancelButton = "PART_CancelButton";
    private const string PartErrorTextBlock = "PART_ErrorTextBlock";
    private const string PartHeaderContentPresenter = "PART_HeaderContentPresenter";

    #endregion

    #region 模板部件引用

    private TextBlock _displayTextBlock;
    private TextBox _editTextBox;
    private Button _editButton;
    private Button _saveButton;
    private Button _cancelButton;
    private TextBlock _errorTextBlock;
    private ContentPresenter _headerContentPresenter;

    // 标识当前是否处于编辑状态
    private bool _isEditing;

    #endregion

    #region 编辑状态事件

    public event RoutedEventHandler EditingStarted;
    public event RoutedEventHandler EditingCompleted;

    #endregion

    public EditableTextBlock()
    {
        DefaultStyleKey = typeof(EditableTextBlock);

        switch (InteractionMode)
        {
            // 按钮模式下使用鼠标进入/离开切换视觉状态
            case EditableTextBlockInteractionMode.Button:
                PointerEntered += EditableTextBlock_PointerEntered;
                PointerExited += EditableTextBlock_PointerExited;
                break;
            // 双击模式下使用双击切换编辑状态
            case EditableTextBlockInteractionMode.DoubleClick:
                DoubleTapped += EditableTextBlock_DoubleTapped;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #region 视觉状态切换（鼠标事件）

    private void EditableTextBlock_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // 仅在按钮模式下处理
        if (InteractionMode == EditableTextBlockInteractionMode.Button)
        {
            VisualStateManager.GoToState(this, !_isEditing ? "PointerOverDisplay" : "PointerOverEdit", true);
        }
    }

    private void EditableTextBlock_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (InteractionMode == EditableTextBlockInteractionMode.Button)
        {
            VisualStateManager.GoToState(this, !_isEditing ? "DisplayState" : "EditState", true);
        }
    }

    // 双击模式下，双击进入编辑状态
    private void EditableTextBlock_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (InteractionMode == EditableTextBlockInteractionMode.DoubleClick)
        {
            EnterEditMode();
        }
    }
    #endregion

    #region 模板加载

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
        if (_editTextBox != null)
            _editTextBox.LostFocus -= EditTextBox_LostFocus;

        // 获取模板部件引用
        _displayTextBlock = GetTemplateChild(PartDisplayTextBlock) as TextBlock;
        _editTextBox = GetTemplateChild(PartEditTextBox) as TextBox;
        _editButton = GetTemplateChild(PartEditButton) as Button;
        _saveButton = GetTemplateChild(PartSaveButton) as Button;
        _cancelButton = GetTemplateChild(PartCancelButton) as Button;
        _errorTextBlock = GetTemplateChild(PartErrorTextBlock) as TextBlock;
        _headerContentPresenter = GetTemplateChild(PartHeaderContentPresenter) as ContentPresenter;

        // 同步显示文本与占位符、换行等属性
        if (_displayTextBlock != null)
            _displayTextBlock.Text = Text;
        if (_editTextBox != null)
        {
            _editTextBox.Text = Text;
            _editTextBox.PlaceholderText = Placeholder;
            _editTextBox.KeyDown += EditTextBox_KeyDown;
        }
        if (_errorTextBlock != null)
        {
            _errorTextBlock.Text = ErrorMessage;
        }

        // 按钮事件仅在按钮模式下有效
        if (InteractionMode == EditableTextBlockInteractionMode.Button)
        {
            if (_editButton != null)
                _editButton.Click += EditButton_Click;
            if (_saveButton != null)
                _saveButton.Click += SaveButton_Click;
            if (_cancelButton != null)
                _cancelButton.Click += CancelButton_Click;
        }
        else  // 双击模式下隐藏所有按钮，并添加失焦事件
        {
            if (_editButton != null)
                _editButton.Visibility = Visibility.Collapsed;
            if (_saveButton != null)
                _saveButton.Visibility = Visibility.Collapsed;
            if (_cancelButton != null)
                _cancelButton.Visibility = Visibility.Collapsed;

            DoubleTapped -= EditableTextBlock_DoubleTapped;
            DoubleTapped += EditableTextBlock_DoubleTapped;

            if (_editTextBox != null)
            {
                _editTextBox.LostFocus -= EditTextBox_LostFocus;
                _editTextBox.LostFocus += EditTextBox_LostFocus;
            }
        }

        // 初始进入默认验证状态
        VisualStateManager.GoToState(this, "Valid", false);
        UpdateVisualStates(false);
    }

    #endregion

    #region 按钮与键盘事件处理

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        EnterEditMode();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveEdit();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelEdit();
    }

    // 键盘处理：Enter 保存，Escape 取消
    private void EditTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                SaveEdit();
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                CancelEdit();
                e.Handled = true;
                break;
            case VirtualKey.None:
            case VirtualKey.LeftButton:
            case VirtualKey.RightButton:
            case VirtualKey.Cancel:
            case VirtualKey.MiddleButton:
            case VirtualKey.XButton1:
            case VirtualKey.XButton2:
            case VirtualKey.Back:
            case VirtualKey.Tab:
            case VirtualKey.Clear:
            case VirtualKey.Shift:
            case VirtualKey.Control:
            case VirtualKey.Menu:
            case VirtualKey.Pause:
            case VirtualKey.CapitalLock:
            case VirtualKey.Kana:
            case VirtualKey.Junja:
            case VirtualKey.Final:
            case VirtualKey.Hanja:
            case VirtualKey.Convert:
            case VirtualKey.NonConvert:
            case VirtualKey.Accept:
            case VirtualKey.ModeChange:
            case VirtualKey.Space:
            case VirtualKey.PageUp:
            case VirtualKey.PageDown:
            case VirtualKey.End:
            case VirtualKey.Home:
            case VirtualKey.Left:
            case VirtualKey.Up:
            case VirtualKey.Right:
            case VirtualKey.Down:
            case VirtualKey.Select:
            case VirtualKey.Print:
            case VirtualKey.Execute:
            case VirtualKey.Snapshot:
            case VirtualKey.Insert:
            case VirtualKey.Delete:
            case VirtualKey.Help:
            case VirtualKey.Number0:
            case VirtualKey.Number1:
            case VirtualKey.Number2:
            case VirtualKey.Number3:
            case VirtualKey.Number4:
            case VirtualKey.Number5:
            case VirtualKey.Number6:
            case VirtualKey.Number7:
            case VirtualKey.Number8:
            case VirtualKey.Number9:
            case VirtualKey.A:
            case VirtualKey.B:
            case VirtualKey.C:
            case VirtualKey.D:
            case VirtualKey.E:
            case VirtualKey.F:
            case VirtualKey.G:
            case VirtualKey.H:
            case VirtualKey.I:
            case VirtualKey.J:
            case VirtualKey.K:
            case VirtualKey.L:
            case VirtualKey.M:
            case VirtualKey.N:
            case VirtualKey.O:
            case VirtualKey.P:
            case VirtualKey.Q:
            case VirtualKey.R:
            case VirtualKey.S:
            case VirtualKey.T:
            case VirtualKey.U:
            case VirtualKey.V:
            case VirtualKey.W:
            case VirtualKey.X:
            case VirtualKey.Y:
            case VirtualKey.Z:
            case VirtualKey.LeftWindows:
            case VirtualKey.RightWindows:
            case VirtualKey.Application:
            case VirtualKey.Sleep:
            case VirtualKey.NumberPad0:
            case VirtualKey.NumberPad1:
            case VirtualKey.NumberPad2:
            case VirtualKey.NumberPad3:
            case VirtualKey.NumberPad4:
            case VirtualKey.NumberPad5:
            case VirtualKey.NumberPad6:
            case VirtualKey.NumberPad7:
            case VirtualKey.NumberPad8:
            case VirtualKey.NumberPad9:
            case VirtualKey.Multiply:
            case VirtualKey.Add:
            case VirtualKey.Separator:
            case VirtualKey.Subtract:
            case VirtualKey.Decimal:
            case VirtualKey.Divide:
            case VirtualKey.F1:
            case VirtualKey.F2:
            case VirtualKey.F3:
            case VirtualKey.F4:
            case VirtualKey.F5:
            case VirtualKey.F6:
            case VirtualKey.F7:
            case VirtualKey.F8:
            case VirtualKey.F9:
            case VirtualKey.F10:
            case VirtualKey.F11:
            case VirtualKey.F12:
            case VirtualKey.F13:
            case VirtualKey.F14:
            case VirtualKey.F15:
            case VirtualKey.F16:
            case VirtualKey.F17:
            case VirtualKey.F18:
            case VirtualKey.F19:
            case VirtualKey.F20:
            case VirtualKey.F21:
            case VirtualKey.F22:
            case VirtualKey.F23:
            case VirtualKey.F24:
            case VirtualKey.NavigationView:
            case VirtualKey.NavigationMenu:
            case VirtualKey.NavigationUp:
            case VirtualKey.NavigationDown:
            case VirtualKey.NavigationLeft:
            case VirtualKey.NavigationRight:
            case VirtualKey.NavigationAccept:
            case VirtualKey.NavigationCancel:
            case VirtualKey.NumberKeyLock:
            case VirtualKey.Scroll:
            case VirtualKey.LeftShift:
            case VirtualKey.RightShift:
            case VirtualKey.LeftControl:
            case VirtualKey.RightControl:
            case VirtualKey.LeftMenu:
            case VirtualKey.RightMenu:
            case VirtualKey.GoBack:
            case VirtualKey.GoForward:
            case VirtualKey.Refresh:
            case VirtualKey.Stop:
            case VirtualKey.Search:
            case VirtualKey.Favorites:
            case VirtualKey.GoHome:
            case VirtualKey.GamepadA:
            case VirtualKey.GamepadB:
            case VirtualKey.GamepadX:
            case VirtualKey.GamepadY:
            case VirtualKey.GamepadRightShoulder:
            case VirtualKey.GamepadLeftShoulder:
            case VirtualKey.GamepadLeftTrigger:
            case VirtualKey.GamepadRightTrigger:
            case VirtualKey.GamepadDPadUp:
            case VirtualKey.GamepadDPadDown:
            case VirtualKey.GamepadDPadLeft:
            case VirtualKey.GamepadDPadRight:
            case VirtualKey.GamepadMenu:
            case VirtualKey.GamepadView:
            case VirtualKey.GamepadLeftThumbstickButton:
            case VirtualKey.GamepadRightThumbstickButton:
            case VirtualKey.GamepadLeftThumbstickUp:
            case VirtualKey.GamepadLeftThumbstickDown:
            case VirtualKey.GamepadLeftThumbstickRight:
            case VirtualKey.GamepadLeftThumbstickLeft:
            case VirtualKey.GamepadRightThumbstickUp:
            case VirtualKey.GamepadRightThumbstickDown:
            case VirtualKey.GamepadRightThumbstickRight:
            case VirtualKey.GamepadRightThumbstickLeft:
            default:
                break;
        }
    }

    // 双击模式下，编辑框失去焦点时自动保存
    private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (InteractionMode == EditableTextBlockInteractionMode.DoubleClick && _isEditing)
        {
            SaveEdit();
        }
    }
    #endregion

    #region 状态切换与验证

    private void EnterEditMode()
    {
        _isEditing = true;
        if (_editTextBox != null)
        {
            _editTextBox.Text = Text;
            _editTextBox.Focus(FocusState.Programmatic);
            _editTextBox.Select(_editTextBox.Text.Length, 0);
        }
        // 重置错误信息
        ErrorMessage = string.Empty;
        VisualStateManager.GoToState(this, "Valid", true);

        UpdateVisualStates(true);

        EditingStarted?.Invoke(this, new RoutedEventArgs());
        if (EditCommand != null && EditCommand.CanExecute(null))
            EditCommand.Execute(null);
    }

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
            Text = _editTextBox.Text;
        }
        if (_displayTextBlock != null)
        {
            _displayTextBlock.Text = Text;
        }
        // 验证通过后恢复为有效状态
        VisualStateManager.GoToState(this, "Valid", true);
        UpdateVisualStates(true);

        EditingCompleted?.Invoke(this, new RoutedEventArgs());
        if (SaveCommand != null && SaveCommand.CanExecute(null))
            SaveCommand.Execute(null);
    }

    private void CancelEdit()
    {
        _isEditing = false;
        VisualStateManager.GoToState(this, "Valid", true);
        UpdateVisualStates(true);

        EditingCompleted?.Invoke(this, new RoutedEventArgs());
        if (CancelCommand != null && CancelCommand.CanExecute(null))
            CancelCommand.Execute(null);
    }

    /// <summary>
    /// 验证输入的文本，默认：如果 IsRequired 为 true，则文本不能为空。子类可重写实现更复杂的验证。
    /// </summary>
    /// <returns>如果验证通过返回 true，否则返回 false</returns>
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

    /// <summary>
    /// 根据当前状态切换视觉状态
    /// </summary>
    /// <param name="useTransitions">是否使用过渡动画</param>
    private void UpdateVisualStates(bool useTransitions)
    {
        if (InteractionMode == EditableTextBlockInteractionMode.Button)
        {
            VisualStateManager.GoToState(this, !_isEditing ? "DisplayState" : "EditState", useTransitions);
        }
        else
        {
            VisualStateManager.GoToState(this, !_isEditing ? "DisplayState" : "DoubleClickModeEdit", useTransitions);
        }
    }

    #endregion
}