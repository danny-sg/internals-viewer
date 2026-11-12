using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;

using InternalsViewer.Query.FullTrace.Symbols;

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

    public AddressTable<BufferPoolFunctionType> BufferPoolFunctions { get; init; } = AddressTable<BufferPoolFunctionType>.Empty;

    public AddressTable<VtableSymbol[]> Vtables { get; init; } = AddressTable<VtableSymbol[]>.Empty;

    public AddressTable<int> StackArguments { get; init; } = AddressTable<int>.Empty;

    public static ReplayFunctionTable Merge(IReadOnlyList<(ReplayFunctionTable Table, ulong Address)> modules)
        => new(Merge(modules, t => t.ExcludedFunctions, static (f, address) => f + address),
               Merge(modules, t => t.InstanceMethods, static (m, address) => m with { Address = m.Address + address }),
               Merge(modules, t => t.LocatorReads, static (l, address) => l with { Function = l.Function + address }),
               Merge(modules, t => t.AllocationFunctions, static (f, address) => f with { Address = f.Address + address }),
               Merge(modules, t => t.AddressMapFunctions, static (f, address) => f with { Address = f.Address + address }))
        {
            PlanNodeFunctions = AddressTable<PlanNodeFunctionType>.Merge(modules.Select(m => (m.Table.PlanNodeFunctions, m.Address))),
            PlanObjects = AddressTable<PlanObjectClass>.Merge(modules.Select(m => (m.Table.PlanObjects, m.Address))),
            BufferPoolFunctions = AddressTable<BufferPoolFunctionType>.Merge(modules.Select(m => (m.Table.BufferPoolFunctions, m.Address))),
            Vtables = AddressTable<VtableSymbol[]>.Merge(modules.Select(m => (m.Table.Vtables, m.Address))),
            StackArguments = AddressTable<int>.Merge(modules.Select(m => (m.Table.StackArguments, m.Address)))
        };

    private static T[] Merge<T>(IReadOnlyList<(ReplayFunctionTable Table, ulong Address)> modules,
                                Func<ReplayFunctionTable, T[]> select,
                                Func<T, ulong, T> rebase)
    {
        var merged = new T[modules.Sum(m => select(m.Table).Length)];

        var at = 0;

        foreach (var (table, address) in modules)
        {
            foreach (var item in select(table))
            {
                merged[at++] = rebase(item, address);
            }
        }

        return merged;
    }
}
