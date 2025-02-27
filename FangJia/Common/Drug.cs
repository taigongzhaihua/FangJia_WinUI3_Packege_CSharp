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
using System.Collections.ObjectModel;

namespace FangJia.Common;

public partial class DrugGroup : ObservableObject
{
    public string? Category { get; set; } // 分类
    public ObservableCollection<DrugSummary> Drugs { get; set; } = []; // 药物列表
}

public class DrugSummary
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
}

public class Drug : DrugSummary
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