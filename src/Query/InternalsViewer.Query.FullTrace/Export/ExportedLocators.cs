namespace InternalsViewer.Query.FullTrace.Export;

internal sealed record ExportedLocators
{
    public IReadOnlyList<string>? Pages { get; init; }

    public IReadOnlyList<string>? Rows { get; init; }

    public IReadOnlyList<ulong[]>? Objects { get; init; }
}
