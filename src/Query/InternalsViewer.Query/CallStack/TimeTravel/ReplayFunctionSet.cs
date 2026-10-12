using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.CallStack.TimeTravel;

internal sealed record ReplayFunctionSet(ulong[] Excluded,
                                         MemoryFunction[] Memory,
                                         InstanceMethod[] InstanceMethods,
                                         LocatorRead[] Locators)
{
    public ReplayFunctionSet Rebase(ulong address)
        => new([.. Excluded.Select(f => f + address)],
               [.. Memory.Select(f => f with { Address = f.Address + address })],
               [.. InstanceMethods.Select(m => m with { Address = m.Address + address })],
               [.. Locators.Select(l => l with { Function = l.Function + address })]);
}
