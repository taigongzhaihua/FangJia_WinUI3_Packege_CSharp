#nullable enable
using System;
using WinRT;

namespace TGZH.Control;


[GeneratedBindableCustomProperty]
public partial class LogItem
{
    public DateTime TimestampUtc { get; set; }
    public string? Application { get; set; }
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? Exception { get; set; }
    public string? Logger { get; set; }
    public int EventId { get; set; }

    public override string ToString()
    {
        var l = $"[{TimestampUtc:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Logger}\t：{Message}";
        if (!string.IsNullOrWhiteSpace(Exception)) l += $" - {Exception}";
        return l;
    }
}