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
    public ObservableCollection<FormulationComposition>? Compositions { get; set; } = []; // 组成
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

[Serializable] // 可序列化标记
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

    public override string ToString()
    {
        return $"{DrugName}";
    }

    [RelayCommand]
    public void UpdateFormulationComposition(object key)
    {
        var k = key as string;
        _ = k switch
        {
            "Position" => FormulationManager.UpdateFormulationComposition(Id, ("Position", Position)),
            "DrugName" => FormulationManager.UpdateFormulationComposition(Id, ("DrugName", DrugName)),
            "Effect" => FormulationManager.UpdateFormulationComposition(Id, ("Effect", Effect)),
            "Notes" => FormulationManager.UpdateFormulationComposition(Id, ("Notes", Notes)),
            _ => null
        };
    }
}

public class FormulationImage
{
    public int Id { get; set; } // 图片ID
    public int FormulationId { get; set; } // 方剂ID（外键）
    public byte[]? Image { get; set; } // 图片内容
}

public class Drug
{
    public int Id { get; set; } // 药物ID
    public string? Name { get; set; } // 药物名称
    public string? EnglishName { get; set; } // 英文名称
    public string? LatinName { get; set; } // 拉丁名称
    public string? Category { get; set; } // 分类
    public string? Origin { get; set; } // 产地
    public string? Properties { get; set; } // 性状
    public string? Quality { get; set; } // 品质
    public string? Taste { get; set; } // 性味
    public string? Meridian { get; set; } // 归经
    public string? Effect { get; set; } // 药物功效
    public string? Notes { get; set; } // 备注
    public string? Processed { get; set; } // 炮制品类
    public string? Source { get; set; } // 来源
    public DrugImage DrugImage { get; set; } = new();
}

public class DrugImage
{
    public int Id { get; set; } // 图片ID
    public int DrugId { get; set; } // 药物ID（外键）
    public byte[]? Image { get; set; } // 图片内容
}