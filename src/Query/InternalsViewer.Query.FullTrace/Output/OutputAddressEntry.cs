namespace InternalsViewer.Query.FullTrace.Output;

internal sealed record OutputAddressEntry(ulong OutputRow, IReadOnlyList<ulong> Columns, ulong DirectSelect, TracePosition Position);
