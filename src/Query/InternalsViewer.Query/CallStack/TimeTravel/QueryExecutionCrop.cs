namespace InternalsViewer.Query.CallStack.TimeTravel;

public static class QueryExecutionCrop
{
    private const string Module = "sqllang";

    private const string Function = "CMsqlExecContext::FExecute";

    public static bool IsQueryExecution(CallStackNode node)
        => node.Frame is { } frame
           && string.Equals(frame.Module, Module, StringComparison.OrdinalIgnoreCase)
           && node.Symbol == Function;

    public static (CallStackTree Tree, Dictionary<CallStackNode, CallStackNode> Projected)? Crop(CallStackTree tree)
    {
        var executing = tree.Nodes()
                            .Where(IsQueryExecution)
                            .Select(n => n.Ancestors().Last())
                            .ToHashSet(ReferenceEqualityComparer.Instance);

        if (executing.Count == 0)
        {
            return null;
        }

        var projected = new Dictionary<CallStackNode, CallStackNode>(ReferenceEqualityComparer.Instance);

        var cropped = tree.Project(cutAt: n => IsOutermostExecution(n) || (n.Parent is { IsRoot: true } && !executing.Contains(n)),
                                   repoint: true,
                                   collapsed: (original, node) => projected[original] = node);

        return (cropped, projected);
    }

    private static bool IsOutermostExecution(CallStackNode node)
        => IsQueryExecution(node) && node.Parent?.Ancestors().Any(IsQueryExecution) != true;
}
