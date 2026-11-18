using System.Text.Json.Serialization;

namespace InternalsViewer.Query.FullTrace.Export;

internal sealed record ExportedNode
{
    public int Id { get; init; }

    public int? Parent { get; init; }

    public string? Function { get; init; }

    public int Order { get; init; }

    public int? Frame { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Calls { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long AllocatedBytes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Allocations { get; init; }

    public ExportedLocatorSet? Locators { get; init; }
}
