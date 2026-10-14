namespace InternalsViewer.Query.FullTrace.Arguments;

internal sealed record ObjectFunctions(IReadOnlyDictionary<ulong, TraceMember> Members,
                                       IReadOnlySet<ulong> Allocators,
                                       IReadOnlyDictionary<ulong, int> Frees)
{
    public static ObjectFunctions Empty { get; } = new(new Dictionary<ulong, TraceMember>(),
                                                       new HashSet<ulong>(),
                                                       new Dictionary<ulong, int>());
}
