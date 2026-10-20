using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal class ValueLocatorParser(LocatorValueType valueType) : LocatorParser
{
    public override LocatorValueType ValueType => valueType;

    public override Locator? Read(ITraceReplay replay, int register, ulong address)
        => replay.TryRead(address, out ulong value) ? new Locator(register, ValueType, value) : null;
}
