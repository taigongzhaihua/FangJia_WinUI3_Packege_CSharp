//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------
using FangJia.Helpers;
using FangJia.ViewModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FangJia.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DrugPage : Page
    {
        internal readonly DrugViewModel ViewModel = Locator.GetService<DrugViewModel>();


        public DrugPage()
        {
            this.InitializeComponent();
        }

        private void SearchBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {

        }

        private void SearchBox_OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {

        }

        private void SearchBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {

        }

        private void PaneOpenOrCloseButton_OnClick(object _, RoutedEventArgs _1)
        {
            SplitView.IsPaneOpen = !SplitView.IsPaneOpen;
        }

        private void OnAdaptiveStatesCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {

        }
    }
}
