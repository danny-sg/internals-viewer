using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class LogSequenceNumberLocatorParser : LocatorParser
{
    public override LocatorValueType ValueType => LocatorValueType.LogSequenceNumber;

    public override IReadOnlyList<LocatorTypeName> TypeNames { get; } = [new("LSN")];

    public override Locator? Read(ITraceReplay replay, int register, ulong address)
        => replay.TryRead(address, out ulong first) && replay.TryRead(address + sizeof(ulong), out ulong second)
            ? LogSequenceNumberLocator.Create(register, first, second)
            : null;
}
