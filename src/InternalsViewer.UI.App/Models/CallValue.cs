using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.UI.App.Models;

public sealed record CallValue(CallTreeNode Node, int CallIndex)
{
    public override string ToString() => $"{CallIndex + 1}";
}
