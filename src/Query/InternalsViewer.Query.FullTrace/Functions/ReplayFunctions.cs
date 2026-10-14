using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.PublishedEvents;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed record ReplayFunctions(ulong[] ExcludedFunctions,
                                       InstanceMethod[] InstanceMethods,
                                       LocatorRead[] LocatorReads,
                                       PublishedEventFunctions PublishedEvents,
                                       MemoryFunction[] MemoryFunctions,
                                       MemoryMapFunction[] MemoryMapFunctions)
{
    public static ReplayFunctions Empty { get; } = new([], [], [], PublishedEventFunctions.Empty, [], []);

    public static ReplayFunctions Merge(IReadOnlyList<ReplayFunctions> functions)
        => new([.. functions.SelectMany(f => f.ExcludedFunctions)],
               [.. functions.SelectMany(f => f.InstanceMethods)],
               [.. functions.SelectMany(f => f.LocatorReads)],
               PublishedEventFunctions.Merge(functions.Select(f => f.PublishedEvents)),
               [.. functions.SelectMany(f => f.MemoryFunctions)],
               [.. functions.SelectMany(f => f.MemoryMapFunctions)]);

    public ReplayFunctions Rebase(ulong address)
        => new([.. ExcludedFunctions.Select(f => f + address)],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. LocatorReads.Select(l => l with { Function = l.Function + address })],
               PublishedEvents.Rebase(address),
               [.. MemoryFunctions.Select(f => f with { Address = f.Address + address })],
               [.. MemoryMapFunctions.Select(f => f with { Address = f.Address + address })]);
}
