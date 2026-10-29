using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.Query.FullTrace.Calls;

public sealed record TraceValueReferences(string? Type, IteratorTarget? Iterator, List<TraceValueUseGroup> Groups);
