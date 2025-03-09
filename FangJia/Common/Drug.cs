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

namespace FangJia.Common;

public partial class DrugSummary : ObservableObject
{
    [ObservableProperty] public partial int Id { get; set; }
    [ObservableProperty] public partial string? Name { get; set; }
    [ObservableProperty] public partial string? Category { get; set; }
}

public partial class Drug : DrugSummary
{
    [ObservableProperty] public partial string? EnglishName { get; set; } // 英文名称
    [ObservableProperty] public partial string? LatinName { get; set; } // 拉丁名称
    [ObservableProperty] public partial string? Origin { get; set; } // 产地
    [ObservableProperty] public partial string? Properties { get; set; } // 性状
    [ObservableProperty] public partial string? Quality { get; set; } // 品质
    [ObservableProperty] public partial string? Taste { get; set; } // 性味
    [ObservableProperty] public partial string? Meridian { get; set; } // 归经
    [ObservableProperty] public partial string? Effect { get; set; } // 药物功效
    [ObservableProperty] public partial string? Notes { get; set; } // 备注
    [ObservableProperty] public partial string? Processed { get; set; } // 炮制品类
    [ObservableProperty] public partial string? Source { get; set; } // 来源
    [ObservableProperty] public partial DrugImage DrugImage { get; set; } = new();
}

public class DrugImage
{
    public int Id { get; set; } // 图片ID
    public int DrugId { get; set; } // 药物ID（外键）
    public byte[]? Image { get; set; } // 图片内容
}
