using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.UI.App.Helpers;

public static class IteratorText
{
    public static string? GetDetail(IteratorTarget? target)
        => target is { Operator.PlanNodeIdentifier: not null } ? $"{target.TypeName} › {target.Label}" : null;
}
