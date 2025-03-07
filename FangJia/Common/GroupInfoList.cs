using System.Collections.Generic;

namespace FangJia.Common;

public partial class GroupInfoList(object key, IEnumerable<object> items) : List<object>(items)
{
    public object Key { get; set; } = key;

    public override string ToString()
    {
        return "Group " + Key;
    }
}