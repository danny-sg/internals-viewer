using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class RowLocatorParser() : ValueLocatorParser(LocatorValueType.Row)
{
    public override IReadOnlyList<LocatorTypeName> TypeNames { get; } = [new("RowId")];
}
