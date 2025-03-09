//------------------------------------------------------------------------
// 本应用部分代码参考了以下开源项目：
// 1.WinUi3 Gallery
// 2.WinUI Community Toolkit
// 3.部分代码由 ChatGPT 、DeepSeek、Copilot 生成
// 版权归原作者所有
// FangJia 仅做学习交流使用
// 转载请注明出处
//------------------------------------------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;
using FangJia.Common;
using FangJia.Helpers;
using System.Collections.ObjectModel;

namespace FangJia.ViewModel;

public partial class DataViewModel : ObservableObject
{
    [ObservableProperty] public partial ObservableCollection<Category> Data { get; set; } = [];

    public DataViewModel()
    {
        Data =
        [
            (NavigationHelper.Categorizes["Formulation"] as Category)!,
            (NavigationHelper.Categorizes["Drug"] as Category)!,
            (NavigationHelper.Categorizes["Classic"] as Category)!,
            (NavigationHelper.Categorizes["Case"] as Category)!
        ];
    }
}