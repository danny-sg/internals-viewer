using System.Text.Json.Serialization;

namespace InternalsViewer.Query.Export;

internal sealed record ExportedNode
{
    public int Id { get; init; }

    public int? Parent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Detached { get; init; }

    public string Key { get; init; } = string.Empty;

    public string? Function { get; init; }

    public int Order { get; init; }

    public int? Frame { get; init; }

    public uint[]? Rvas { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Calls { get; init; }

    public int[]? CallActivity { get; init; }

    public int[]? ActivityCounts { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long AllocatedBytes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Allocations { get; init; }

    public ExportedLocators? Locators { get; init; }

    public int[]? Events { get; init; }
}
