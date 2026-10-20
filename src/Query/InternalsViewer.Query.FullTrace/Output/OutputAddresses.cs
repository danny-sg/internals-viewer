namespace InternalsViewer.Query.FullTrace.Output;

internal sealed record OutputAddresses(ulong OutputRow, IReadOnlyList<ulong> Columns, ulong DirectSelect, TracePosition Position);
