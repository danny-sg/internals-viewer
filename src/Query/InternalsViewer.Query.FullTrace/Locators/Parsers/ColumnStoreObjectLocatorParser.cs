using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class ColumnStoreObjectLocatorParser : LocatorParser
{
    public override LocatorValueType ValueType => LocatorValueType.ColumnStoreObject;

    public override IReadOnlyList<LocatorTypeName> TypeNames { get; } = [new("ColumnStoreObjectKey")];

    public override Locator? Read(ITraceReplay replay, int register, ulong address)
        => replay.TryRead(address, out ulong first)
           && replay.TryRead(address + sizeof(ulong), out ulong second)
           && replay.TryRead(address + 2 * sizeof(ulong), out ulong third)
            ? ColumnStoreObjectLocator.Create(register, first, second, third)
            : null;
}
