using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed record ReplayFunctions(ulong[] ExcludedFunctions,
                                       InstanceMethod[] InstanceMethods,
                                       LocatorRead[] LocatorReads,
                                       MemoryFunction[] MemoryFunctions,
                                       MemoryMapFunction[] MemoryMapFunctions)
{
    public static ReplayFunctions Empty { get; } = new([], [], [], [], []);

    public AddressTable<PlanNodeFunctionType> PlanNodeFunctions { get; init; } = AddressTable<PlanNodeFunctionType>.Empty;

    public AddressTable<PlanObjectClass> PlanObjects { get; init; } = AddressTable<PlanObjectClass>.Empty;

    public AddressTable<OutputFunctionType> OutputFunctions { get; init; } = AddressTable<OutputFunctionType>.Empty;

    public AddressTable<OutputValueClass> OutputClasses { get; init; } = AddressTable<OutputValueClass>.Empty;

    public AddressTable<BufferPoolFunctionType> BufferPoolFunctions { get; init; } = AddressTable<BufferPoolFunctionType>.Empty;

    public AddressTable<string[]> Vtables { get; init; } = AddressTable<string[]>.Empty;

    public static ReplayFunctions Merge(IReadOnlyList<ReplayFunctions> functions)
        => new([.. functions.SelectMany(f => f.ExcludedFunctions)],
               [.. functions.SelectMany(f => f.InstanceMethods)],
               [.. functions.SelectMany(f => f.LocatorReads)],
               [.. functions.SelectMany(f => f.MemoryFunctions)],
               [.. functions.SelectMany(f => f.MemoryMapFunctions)])
        {
            PlanNodeFunctions = AddressTable<PlanNodeFunctionType>.Merge(functions.Select(f => f.PlanNodeFunctions)),
            PlanObjects = AddressTable<PlanObjectClass>.Merge(functions.Select(f => f.PlanObjects)),
            OutputFunctions = AddressTable<OutputFunctionType>.Merge(functions.Select(f => f.OutputFunctions)),
            OutputClasses = AddressTable<OutputValueClass>.Merge(functions.Select(f => f.OutputClasses)),
            BufferPoolFunctions = AddressTable<BufferPoolFunctionType>.Merge(functions.Select(f => f.BufferPoolFunctions)),
            Vtables = AddressTable<string[]>.Merge(functions.Select(f => f.Vtables))
        };

    public ReplayFunctions Rebase(ulong address)
        => new([.. ExcludedFunctions.Select(f => f + address)],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. LocatorReads.Select(l => l with { Function = l.Function + address })],
               [.. MemoryFunctions.Select(f => f with { Address = f.Address + address })],
               [.. MemoryMapFunctions.Select(f => f with { Address = f.Address + address })])
        {
            PlanNodeFunctions = PlanNodeFunctions.Rebase(address),
            PlanObjects = PlanObjects.Rebase(address),
            OutputFunctions = OutputFunctions.Rebase(address),
            OutputClasses = OutputClasses.Rebase(address),
            BufferPoolFunctions = BufferPoolFunctions.Rebase(address),
            Vtables = Vtables.Rebase(address)
        };
}
