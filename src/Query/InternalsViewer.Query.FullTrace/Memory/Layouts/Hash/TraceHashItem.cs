namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

public sealed record TraceHashItem(ulong Address, List<TraceHashEntry> Entries);
