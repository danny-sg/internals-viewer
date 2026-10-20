using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Locators.Enums;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class VariantLocatorParser : LocatorParser
{
    public override LocatorValueType ValueType => LocatorValueType.Variant;

    public override IReadOnlyList<LocatorTypeName> TypeNames { get; } = [new("CXVariant")];

    public override bool IsReadAtReturn => true;

    public override Locator? Read(ITraceReplay replay, int register, ulong address)
    {
        if (!replay.TryRead(address + VariantLayout.StatusOffset, out byte status)
            || !replay.TryRead(address + VariantLayout.ValueOffset, out ulong value))
        {
            return null;
        }

        var length = replay.TryRead(address + VariantLayout.LengthOffset, out uint found) ? found : 0;

        return VariantLocator.Create(register, (VariantStatus)status, value, length);
    }
}
