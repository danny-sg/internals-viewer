using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Values;

public sealed class ValueTypeRow(string name, IteratorTarget? iterator)
{
    public string Name { get; } = name;

    public IteratorTarget? Iterator { get; } = iterator;

    public string ToolTip => Iterator is null ? Name : "Show this iterator in the call tree and the plan";
}
