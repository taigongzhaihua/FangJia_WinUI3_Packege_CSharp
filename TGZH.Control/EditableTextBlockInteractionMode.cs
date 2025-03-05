namespace TGZH.Control
{
    /// <summary>
    /// 定义 EditableTextBlock 控件的交互模式
    /// </summary>
    public enum EditableTextBlockInteractionMode
    {
        /// <summary>
        /// 双击文本区域进入编辑，失焦后自动保存，按Esc键取消
        /// </summary>
        DoubleClick,

        /// <summary>
        /// 通过按钮进入编辑、保存及取消
        /// </summary>
        Button
    }
}