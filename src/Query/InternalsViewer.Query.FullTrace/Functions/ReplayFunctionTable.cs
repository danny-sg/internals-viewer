using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Output.Enums;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed record ReplayFunctionTable(ulong[] ExcludedFunctions,
                                           InstanceMethod[] InstanceMethods,
                                           LocatorRead[] LocatorReads,
                                           AllocationFunction[] AllocationFunctions,
                                           AddressMapFunction[] AddressMapFunctions)
{
    public static ReplayFunctionTable Empty { get; } = new([], [], [], [], []);

    public AddressTable<PlanNodeFunctionType> PlanNodeFunctions { get; init; } = AddressTable<PlanNodeFunctionType>.Empty;

    public AddressTable<PlanObjectClass> PlanObjects { get; init; } = AddressTable<PlanObjectClass>.Empty;

    public AddressTable<OutputFunctionType> OutputFunctions { get; init; } = AddressTable<OutputFunctionType>.Empty;

    public AddressTable<OutputValueClass> OutputClasses { get; init; } = AddressTable<OutputValueClass>.Empty;

    public AddressTable<BufferPoolFunctionType> BufferPoolFunctions { get; init; } = AddressTable<BufferPoolFunctionType>.Empty;

    public AddressTable<string[]> Vtables { get; init; } = AddressTable<string[]>.Empty;

    public AddressTable<int> StackArguments { get; init; } = AddressTable<int>.Empty;

    public static ReplayFunctionTable Merge(IReadOnlyList<ReplayFunctionTable> functions)
        => new([.. functions.SelectMany(f => f.ExcludedFunctions)],
               [.. functions.SelectMany(f => f.InstanceMethods)],
               [.. functions.SelectMany(f => f.LocatorReads)],
               [.. functions.SelectMany(f => f.AllocationFunctions)],
               [.. functions.SelectMany(f => f.AddressMapFunctions)])
        {
            PlanNodeFunctions = AddressTable<PlanNodeFunctionType>.Merge(functions.Select(f => f.PlanNodeFunctions)),
            PlanObjects = AddressTable<PlanObjectClass>.Merge(functions.Select(f => f.PlanObjects)),
            OutputFunctions = AddressTable<OutputFunctionType>.Merge(functions.Select(f => f.OutputFunctions)),
            OutputClasses = AddressTable<OutputValueClass>.Merge(functions.Select(f => f.OutputClasses)),
            BufferPoolFunctions = AddressTable<BufferPoolFunctionType>.Merge(functions.Select(f => f.BufferPoolFunctions)),
            Vtables = AddressTable<string[]>.Merge(functions.Select(f => f.Vtables)),
            StackArguments = AddressTable<int>.Merge(functions.Select(f => f.StackArguments))
        };

    public ReplayFunctionTable Rebase(ulong address)
        => new([.. ExcludedFunctions.Select(f => f + address)],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. LocatorReads.Select(l => l with { Function = l.Function + address })],
               [.. AllocationFunctions.Select(f => f with { Address = f.Address + address })],
               [.. AddressMapFunctions.Select(f => f with { Address = f.Address + address })])
        {
            PlanNodeFunctions = PlanNodeFunctions.Rebase(address),
            PlanObjects = PlanObjects.Rebase(address),
            OutputFunctions = OutputFunctions.Rebase(address),
            OutputClasses = OutputClasses.Rebase(address),
            BufferPoolFunctions = BufferPoolFunctions.Rebase(address),
            Vtables = Vtables.Rebase(address),
            StackArguments = StackArguments.Rebase(address)
        };
}
