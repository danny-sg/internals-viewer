using InternalsViewer.Query.CallStack.TimeTravel.Locators;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record ReplayFunctionSet(ulong[] Excluded,
                                       MemoryFunction[] Memory,
                                       ulong[] InstanceMethods,
                                       LocatorRead[] Locators)
{
    public ReplayFunctionSet Rebase(ulong address)
        => new([.. Excluded.Select(f => f + address)],
               [.. Memory.Select(f => f with { Address = f.Address + address })],
               [.. InstanceMethods.Select(f => f + address)],
               [.. Locators.Select(l => l with { Function = l.Function + address })]);
}
