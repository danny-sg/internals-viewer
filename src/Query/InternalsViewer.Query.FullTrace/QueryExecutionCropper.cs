using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace;

internal static class QueryExecutionCropper
{
    private const string Module = "sqllang";

    private const string Function = "CXStmtQuery::ErsqExecuteQuery";

    public static bool IsQueryExecution(CallTreeNode node)
        => node.Frame is { } frame
           && string.Equals(frame.Module.Name, Module, StringComparison.OrdinalIgnoreCase)
           && node.Symbol == Function;

    public static IReadOnlyList<CallTreeNode> GetRoots(CallTree tree)
    {
        var executing = GetRoots(tree, IsQueryExecution);

        if (executing.Count == 0)
        {
            return [];
        }

        var working = GetRoots(tree, IsIterator);

        return [.. tree.GetNodes().Where(n => IsOutermostExecution(n) || IsWorker(n, executing, working)).OrderBy(n => n.Order)];
    }

    private static HashSet<CallTreeNode> GetRoots(CallTree tree, Func<CallTreeNode, bool> predicate)
        => tree.GetNodes().Where(predicate).Select(n => n.GetAncestors().Last()).ToHashSet<CallTreeNode>(ReferenceEqualityComparer.Instance);

    private static bool IsWorker(CallTreeNode node, HashSet<CallTreeNode> executing, HashSet<CallTreeNode> working)
        => node.Parent is { IsRoot: true } && !executing.Contains(node) && working.Contains(node);

    private static bool IsIterator(CallTreeNode node)
        => node.Frame?.Function?.SymbolCategory is SymbolCategory.QueryOperator or SymbolCategory.BatchOperator;

    private static bool IsOutermostExecution(CallTreeNode node)
        => IsQueryExecution(node) && node.Parent?.GetAncestors().Any(IsQueryExecution) != true;
}
