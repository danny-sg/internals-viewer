using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed record ReplayFunctions(ulong[] ExcludedFunctions,
                                       InstanceMethod[] InstanceMethods,
                                       LocatorRead[] LocatorReads,
                                       MemoryFunction[] MemoryFunctions,
                                       MemoryMapFunction[] MemoryMapFunctions,
                                       PlanNodeFunctions PlanNodeFunctions,
                                       OutputFunctions Output)
{
    public static ReplayFunctions Empty { get; } = new([],
                                                       [],
                                                       [],
                                                       [],
                                                       [],
                                                       PlanNodeFunctions.Empty,
                                                       OutputFunctions.Empty);

    public IReadOnlyDictionary<ulong, string[]> Vtables { get; init; } = new Dictionary<ulong, string[]>();

    public static ReplayFunctions Merge(IReadOnlyList<ReplayFunctions> functions)
        => new([.. functions.SelectMany(f => f.ExcludedFunctions)],
               [.. functions.SelectMany(f => f.InstanceMethods)],
               [.. functions.SelectMany(f => f.LocatorReads)],
               [.. functions.SelectMany(f => f.MemoryFunctions)],
               [.. functions.SelectMany(f => f.MemoryMapFunctions)],
               PlanNodeFunctions.Merge(functions.Select(f => f.PlanNodeFunctions)),
               OutputFunctions.Merge(functions.Select(f => f.Output)))
        {
            Vtables = AddressMapHelper.Merge(functions.Select(f => f.Vtables))
        };

    public ReplayFunctions Rebase(ulong address)
        => new([.. ExcludedFunctions.Select(f => f + address)],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. LocatorReads.Select(l => l with { Function = l.Function + address })],
               [.. MemoryFunctions.Select(f => f with { Address = f.Address + address })],
               [.. MemoryMapFunctions.Select(f => f with { Address = f.Address + address })],
               PlanNodeFunctions.Rebase(address),
               Output.Rebase(address))
        {
            Vtables = AddressMapHelper.Rebase(Vtables, address)
        };
}
