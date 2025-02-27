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
using System.Collections.ObjectModel;

namespace FangJia.ViewModel;

public partial class DrugViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsDrugSelected { get; private set; } = false;
    [ObservableProperty] public partial DrugSummary? SelectedDrugSummary { get; set; } = null;
    [ObservableProperty] public partial ObservableCollection<DrugGroup>? DrugGroups { get; set; } = [];
    [ObservableProperty] public partial Drug? SelectedDrug { get; set; } = null;

}