using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;

namespace InternalsViewer.Query.FullTrace;

internal static class QueryExecutionCropper
{
    private const string Module = "sqllang";

    private const string Function = "CMsqlExecContext::FExecute";

    public static bool IsQueryExecution(CallStackNode node)
        => node.Frame is { } frame
           && string.Equals(frame.Module, Module, StringComparison.OrdinalIgnoreCase)
           && node.Symbol == Function;

    public static (CallStackTree Tree, Dictionary<CallStackNode, CallStackNode> Projected)? Crop(CallStackTree tree)
    {
        var executing = RootsOf(tree, IsQueryExecution);

        if (executing.Count == 0)
        {
            return null;
        }

        var working = RootsOf(tree, IsIterator);

        var projected = new Dictionary<CallStackNode, CallStackNode>(ReferenceEqualityComparer.Instance);

        var cropped = tree.Project(cutAt: n => IsOutermostExecution(n) || IsWorker(n, executing, working),
                                   repoint: true,
                                   collapsed: (original, node) => projected[original] = node);

        return (cropped, projected);
    }

    private static HashSet<CallStackNode> RootsOf(CallStackTree tree, Func<CallStackNode, bool> predicate)
        => tree.Nodes().Where(predicate).Select(n => n.Ancestors().Last()).ToHashSet<CallStackNode>(ReferenceEqualityComparer.Instance);

    private static bool IsWorker(CallStackNode node, HashSet<CallStackNode> executing, HashSet<CallStackNode> working)
        => node.Parent is { IsRoot: true } && !executing.Contains(node) && working.Contains(node);

    private static bool IsIterator(CallStackNode node)
        => node.Frame?.Resolved?.SymbolCategory is SymbolCategory.QueryOperator or SymbolCategory.PhysicalOperator;

    private static bool IsOutermostExecution(CallStackNode node)
        => IsQueryExecution(node) && node.Parent?.Ancestors().Any(IsQueryExecution) != true;
}
