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
using CommunityToolkit.Mvvm.Input;
using FangJia.DataAccess;
using System;
using System.Collections.ObjectModel;
using WinRT;
using static FangJia.Helpers.Locator;

namespace FangJia.Common;

[GeneratedBindableCustomProperty]
public partial class FormulationCategory(int id, string name, bool isCategory) : ObservableObject
{
    public int Id { get; set; } = id;
    [ObservableProperty] public partial string Name { get; set; } = name;
    public bool IsCategory { get; set; } = isCategory;

    public ObservableCollection<FormulationCategory> Children { get; set; } = [];
    [ObservableProperty] public partial bool IsExpanded { get; set; } = false;
    [ObservableProperty] public partial bool IsSelected { get; set; } = false;
}

public partial class Formulation : ObservableObject
{
    public int Id { get; set; } // 方剂ID
    public string? Name { get; set; } // 方剂名称
    public int CategoryId { get; set; } // 分类ID（外键）
    [ObservableProperty] public partial ObservableCollection<FormulationComposition>? Compositions { get; set; } = []; // 组成
    public string? Usage { get; set; } // 用法
    public string? Effect { get; set; } // 功效
    public string? Indication { get; set; } // 适应症
    public string? Disease { get; set; } // 疾病
    public string? Application { get; set; } // 应用
    public string? Supplement { get; set; } // 辅助
    public string? Song { get; set; } // 歌诀
    public string? Notes { get; set; } // 备注
    public string? Source { get; set; } // 来源
    [ObservableProperty] public partial FormulationImage FormulationImage { get; set; } = new();
}

[Serializable]
public partial class FormulationComposition : ObservableObject
{
    [ObservableProperty] public partial string? Position { get; set; } // 君臣佐使
    [ObservableProperty] public partial int Id { get; set; } // 组成ID
    [ObservableProperty] public partial int FormulationId { get; set; } // 方剂ID（外键）
    [ObservableProperty] public partial int DrugId { get; set; } // 药物ID（外键）
    [ObservableProperty] public partial string? DrugName { get; set; } // 药物名称
    [ObservableProperty] public partial string? Effect { get; set; } // 方中功效
    [ObservableProperty] public partial string? Notes { get; set; } // 备注
    private static readonly FormulationManager FormulationManager = GetService<FormulationManager>();
    public override string ToString() => DrugName ?? string.Empty;
    [RelayCommand]
    public void UpdateFormulationComposition(object key) => _ = (key as string) switch
    {
        "Position" => FormulationManager.UpdateFormulationComposition(Id, ("Position", Position)),
        "DrugName" => FormulationManager.UpdateFormulationComposition(Id, ("DrugName", DrugName)),
        "Effect" => FormulationManager.UpdateFormulationComposition(Id, ("Effect", Effect)),
        "Notes" => FormulationManager.UpdateFormulationComposition(Id, ("Notes", Notes)),
        _ => null
    };
}

public class FormulationImage
{
    public int Id { get; set; } // 图片ID
    public int FormulationId { get; set; } // 方剂ID（外键）
    public byte[]? Image { get; set; } // 图片内容
}
