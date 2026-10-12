namespace InternalsViewer.Query.Export;

internal sealed record ExportCallTree
{
    public string Name { get; init; } = string.Empty;

    public string File { get; init; } = string.Empty;

    public int RootId { get; init; }

    public bool IsResultTree { get; init; }

    public bool IsDetached { get; init; }

    public bool ActivityFromTrace { get; init; }

    public long ActivityMinUs { get; init; }

    public long ActivityMaxUs { get; init; }

    public int ActivityBuckets { get; init; }

    public int ActivityBusiest { get; init; }
}
