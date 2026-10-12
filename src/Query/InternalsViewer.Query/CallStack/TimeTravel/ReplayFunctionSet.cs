using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Markers;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.CallStack.TimeTravel;

internal sealed record ReplayFunctionSet(ulong[] Excluded,
                                         MemoryFunction[] Memory,
                                         InstanceMethod[] InstanceMethods,
                                         LocatorRead[] Locators,
                                         EventPublisher[] Publishers,
                                         ulong[] Reserves)
{
    public ReplayFunctionSet Rebase(ulong address)
        => new([.. Excluded.Select(f => f + address)],
               [.. Memory.Select(f => f with { Address = f.Address + address })],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. Locators.Select(l => l with { Function = l.Function + address })],
               [.. Publishers.Select(p => p with { Address = p.Address + address })],
               [.. Reserves.Select(f => f + address)]);
}
