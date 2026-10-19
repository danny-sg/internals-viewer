namespace InternalsViewer.Query.FullTrace.Output;

internal sealed partial record TraceOutputStructure(ulong OutputRow, IReadOnlyList<ulong> Owners, ulong DirectSelectOwner);
