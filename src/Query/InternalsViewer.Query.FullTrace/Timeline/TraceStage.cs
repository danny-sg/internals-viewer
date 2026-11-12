using InternalsViewer.Query.FullTrace.Calls.Tree;

namespace InternalsViewer.Query.FullTrace.Timeline;

public sealed record TraceStage(string Name, CallTreeNode Node, uint ThreadId, TraceSpan Span, int Depth)
{
    private const string SearchSymbol = "CMemo::PerformOptimizationStage";

    private const int SearchLevelSlot = 1;

    private static readonly Dictionary<string, string> Names = new()
    {
        ["CSQLSource::FCompile"] = "Compilation",
        ["CProchdr::FNormalizeStep"] = "Algebrization",
        ["PqoBuild"] = "Plan Build",
        ["COptContext::PqteOptimizeWrapper"] = "Optimization",
        ["COptContext::PexprSimplify"] = "Simplification",
        ["COptContext::PexprSearchPlan"] = "Plan Search",
        [SearchSymbol] = "Search",
        ["CXteBuilder::Prepare"] = "Plan Objects"
    };

    public static IReadOnlyList<TraceStage> Build(TraceTimeline timeline)
    {
        var calls = timeline.GetCalls(n => Names.ContainsKey(n.Symbol))
                            .OrderBy(c => c.ThreadId)
                            .ThenBy(c => c.Span.From.Position)
                            .ThenByDescending(c => c.Span.To.Position);

        var stages = new List<TraceStage>();

        var open = new Stack<TraceStage>();

        foreach (var (threadId, span) in calls)
        {
            if (timeline.GetNode(span.CallNodeIndex) is not { } node)
            {
                continue;
            }

            while (open.TryPeek(out var outer) && (outer.ThreadId != threadId || outer.Span.To.Position <= span.From.Position))
            {
                open.Pop();
            }

            var stage = new TraceStage(GetName(timeline, node, span), node, threadId, span, open.Count);

            stages.Add(stage);

            open.Push(stage);
        }

        return stages;
    }

    private static string GetName(TraceTimeline timeline, CallTreeNode node, TraceSpan span)
    {
        var symbol = node.Symbol;

        return symbol == SearchSymbol && timeline.GetCall(node, span.CallIndex) is { } call
            ? $"{Names[symbol]} {(uint)call.GetSlot(SearchLevelSlot)}"
            : Names[symbol];
    }
}
