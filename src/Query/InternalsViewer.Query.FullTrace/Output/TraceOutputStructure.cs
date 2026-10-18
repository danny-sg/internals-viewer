namespace InternalsViewer.Query.FullTrace.Output;

public sealed partial record TraceOutputStructure(ulong OutputRow, IReadOnlyList<TraceOutputSource> Columns, ulong DirectSelectBuffer)
{
    internal ulong DirectSelectOwner { get; init; }
}
