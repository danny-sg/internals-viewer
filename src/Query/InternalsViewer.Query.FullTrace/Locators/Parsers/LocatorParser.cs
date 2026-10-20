using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal abstract class LocatorParser
{
    public abstract LocatorValueType ValueType { get; }

    public virtual IReadOnlyList<LocatorTypeName> TypeNames => [];

    public virtual bool IsConstPointerOnly => false;

    public virtual bool IsReadAtReturn => false;

    public abstract Locator? Read(ITraceReplay replay, int register, ulong address);
}
