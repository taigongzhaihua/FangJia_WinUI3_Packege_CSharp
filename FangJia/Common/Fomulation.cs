using System;
using System.Collections.ObjectModel;

namespace FangJia.Common;

public class FormulationCategory(int id, string name, bool isCategory)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public bool IsCategory { get; set; } = isCategory;
    public ObservableCollection<FormulationCategory> Children { get; set; } = [];
}

public class Formulation
{
    public int Id { get; set; } // 方剂ID
    public string? Name { get; set; } // 方剂名称
    public int CategoryId { get; set; } // 分类ID（外键）
    public ObservableCollection<FormulationComposition>? Compositions { get; set; } = []; // 组成
    public string? Usage { get; set; }       // 用法
    public string? Effect { get; set; }       // 功效
    public string? Indication { get; set; }       // 适应症
    public string? Disease { get; set; }       // 疾病
    public string? Application { get; set; }       // 应用
    public string? Supplement { get; set; }       // 辅助
    public string? Song { get; set; }       // 歌诀
    public string? Notes { get; set; }       // 备注
    public string? Source { get; set; }       // 来源
    public FormulationImage FormulationImage { get; set; } = new();
}

[Serializable] // 可序列化标记
public class FormulationComposition
{
    public string? Position { get; set; } // 君臣佐使
    public int Id { get; set; } // 组成ID
    public int FormulationId { get; set; } // 方剂ID（外键）
    public int DrugId { get; set; } // 药物ID（外键）
    public string? DrugName { get; set; } // 药物名称
    public string? Effect { get; set; } // 方中功效
    public string? Notes { get; set; } // 备注
    public override string ToString()
    {
        return $"{DrugName}";
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