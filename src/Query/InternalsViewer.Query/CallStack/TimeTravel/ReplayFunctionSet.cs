using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record ReplayFunctionSet(ulong[] Excluded,
                                       MemoryFunction[] Memory,
                                       IReadOnlyDictionary<ulong, string> Publishers,
                                       ulong[] BufferReserves,
                                       ulong[] InstanceMethods,
                                       LocatorRead[] Locators)
{
    public ulong[] Markers { get; } = [.. Publishers.Keys, .. BufferReserves];

    public ReplayFunctionSet Rebase(ulong address)
        => new([.. Excluded.Select(f => f + address)],
               [.. Memory.Select(f => f with { Address = f.Address + address })],
               Publishers.ToDictionary(p => p.Key + address, p => p.Value),
               [.. BufferReserves.Select(f => f + address)],
               [.. InstanceMethods.Select(f => f + address)],
               [.. Locators.Select(l => l with { Function = l.Function + address })]);
}
