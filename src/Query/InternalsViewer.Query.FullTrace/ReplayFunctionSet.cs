using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Markers;

namespace InternalsViewer.Query.FullTrace;

internal sealed record ReplayFunctionSet(ulong[] Excluded,
                                         InstanceMethod[] InstanceMethods,
                                         LocatorRead[] Locators,
                                         EventPublisher[] Publishers,
                                         ulong[] Reserves)
{
    public static ReplayFunctionSet Empty { get; } = new([], [], [], [], []);

    public static ReplayFunctionSet Merge(IReadOnlyList<ReplayFunctionSet> sets)
        => new([.. sets.SelectMany(s => s.Excluded)],
               [.. sets.SelectMany(s => s.InstanceMethods)],
               [.. sets.SelectMany(s => s.Locators)],
               [.. sets.SelectMany(s => s.Publishers)],
               [.. sets.SelectMany(s => s.Reserves)]);

    public ReplayFunctionSet Rebase(ulong address)
        => new([.. Excluded.Select(f => f + address)],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. Locators.Select(l => l with { Function = l.Function + address })],
               [.. Publishers.Select(p => p with { Address = p.Address + address })],
               [.. Reserves.Select(f => f + address)]);
}
