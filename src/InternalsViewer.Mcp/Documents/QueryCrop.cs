using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.Mcp.Documents;

public static class QueryCrop
{
    public const string OptionKey = "crop-to-query";

    public static bool IsHidden(CallTreeNode node, HashSet<CallTreeNode> roots) => !node.GetAncestors().Any(roots.Contains);

    public static bool IsHidden(TracePosition position, TracePosition start, TracePosition end) => position < start || position > end;

    public static string GetPhase(TracePosition? position, TracePosition start, TracePosition end) => position switch
    {
        { } before when before < start => "before the query starts executing, during compilation and setup",
        { } after when after > end => "after the query has finished executing",
        _ => "outside the query's execution, in compilation, setup or teardown"
    };
}
