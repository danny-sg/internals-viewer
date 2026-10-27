using System.ComponentModel;
using System.Globalization;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class TimelineTools(McpDocumentResolver documents)
{
    private const string DocumentDescription = "Document id from list_documents; the active document when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "timeline", ReadOnly = true)]
    [Description("Full Trace timeline: the threads with their span counts and instruction totals, the longest spans, or one call tree node's spans and instructions")]
    public async Task<string> Timeline([Description("List the N longest spans")] int? top = null,
                                       [Description("Length axis for the longest spans: instructions or position")] string axis = "instructions",
                                       [Description("Only spans on this thread id")] int? thread = null,
                                       [Description("Only spans at this call depth")] int? depth = null,
                                       [Description("Only spans of this call tree node id")] int? node = null,
                                       [Description(DocumentDescription)] string? document = null,
                                       CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        var timeline = trace.Timeline;

        var useInstructions = axis.ToLowerInvariant() switch
        {
            "instructions" => true,
            "position" => false,
            _ => throw new McpException($"axis must be instructions or position, not '{axis}'.")
        };

        var builder = new StringBuilder();

        builder.AppendLine($"timeline: {timeline.Scale.StepsPerSequence.ToString("F0", CultureInfo.InvariantCulture)} steps per sequence, " +
                           $"start {Format.Position(timeline.Start)} end {Format.Position(timeline.End)}, " +
                           $"{timeline.NodeCount} replay nodes mapped to the call tree");

        var threads = new TextTable("Thread", "Calls", "Spans", "Depth", "First", "Last", "Instructions");

        foreach (var timelineThread in timeline.Threads)
        {
            var (first, last) = GetBounds(timelineThread);

            threads.Add(timelineThread.ThreadId,
                        timelineThread.Calls,
                        timelineThread.Rows.Sum(r => (long)r.Count),
                        timelineThread.Rows.Count,
                        first is { } firstPosition ? Format.Position(firstPosition) : string.Empty,
                        last is { } lastPosition ? Format.Position(lastPosition) : string.Empty,
                        last is { } known ? Format.Number(timelineThread.Clock.GetInstructions(known)) : string.Empty);
        }

        threads.Write(builder);

        if (top is { } count)
        {
            AppendLongest(builder, scope, timeline, count, useInstructions, thread, depth, node);
        }
        else if (node is { } nodeId)
        {
            AppendNodeTotals(builder, scope, timeline, nodeId);
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "plannodes", ReadOnly = true)]
    [Description("Full Trace: the iterator objects linked to plan node ids, with their plan objects, query and construction position")]
    public async Task<string> PlanNodes([Description(DocumentDescription)] string? document = null,
                                        CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        if (trace.PlanNodes.Count == 0)
        {
            return "no plan nodes in the trace";
        }

        var operators = scope.Result.EngineEvents
                             .OfType<ExecutionOperatorEvent>()
                             .Where(o => o.PlanNodeIdentifier is not null)
                             .GroupBy(o => o.PlanNodeIdentifier!.NodeId)
                             .ToDictionary(g => g.Key, g => g.First().OperatorDescription);

        var builder = new StringBuilder();

        builder.AppendLine($"plan nodes: {trace.PlanNodes.Count} iterator objects linked to plan nodes");

        var table = new TextTable("Node", "Operator", "Iterator", "Plan object", "Batch plan object", "Query", "Constructed", "Batch helper");

        foreach (var planNode in trace.PlanNodes.OrderBy(p => p.NodeId).ThenBy(p => p.Iterator))
        {
            table.Add(planNode.NodeId,
                      operators.GetValueOrDefault(planNode.NodeId, string.Empty),
                      Format.Hex(planNode.Iterator),
                      Format.Hex(planNode.PlanObject),
                      planNode.BatchPlanObject == 0 ? string.Empty : Format.Hex(planNode.BatchPlanObject),
                      Format.Hex(planNode.Query),
                      planNode.Constructed is { } constructed ? Format.Position(constructed) : string.Empty,
                      planNode.IsBatchHelper);
        }

        table.Write(builder);

        return builder.ToString();
    }

    [McpServerTool(Name = "teardowns", ReadOnly = true)]
    [Description("Full Trace: where the query's execution ended on each thread")]
    public async Task<string> Teardowns([Description(DocumentDescription)] string? document = null,
                                        CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        if (trace.Teardowns.Count == 0)
        {
            return "no teardowns in the trace";
        }

        var builder = new StringBuilder();

        builder.AppendLine("teardowns: where the query's execution ended on each thread");

        var table = new TextTable("Thread", "Position", "Instructions");

        foreach (var teardown in trace.Teardowns.OrderBy(t => t.Thread))
        {
            table.Add(teardown.Thread, Format.Position(teardown.End), Format.Number(teardown.End.Instructions));
        }

        table.Write(builder);

        return builder.ToString();
    }

    [McpServerTool(Name = "output", ReadOnly = true)]
    [Description("Full Trace: the output row structures with the iterator object owning each column, and the plan operator each column comes from")]
    public async Task<string> Output([Description("Maximum output rows to list")] int limit = 50,
                                     [Description(DocumentDescription)] string? document = null,
                                     CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var trace = scope.RequireTrace();

        if (trace.OutputStructures.Count == 0)
        {
            return "no output structures in the trace";
        }

        var builder = new StringBuilder();

        builder.AppendLine($"output structures: {trace.OutputStructures.Count} output rows, each column's owning iterator object");

        var table = new TextTable("Output row", "Direct select owner", "Column owners");

        foreach (var structure in trace.OutputStructures.Take(limit))
        {
            table.Add(Format.Hex(structure.OutputRow),
                      Format.Hex(structure.DirectSelectOwner),
                      string.Join(", ", structure.Owners.Select(Format.Hex)));
        }

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(trace.OutputStructures.Count, limit, "raise limit"));

        if (trace.OutputSources.Count > 0)
        {
            builder.AppendLine();

            builder.AppendLine("sources: the plan operator that produced each output column");

            foreach (var source in trace.OutputSources)
            {
                var columns = source.IsWholeRow ? "whole row" : $"columns {string.Join(", ", source.Columns)}";

                builder.AppendLine($"  node {source.Operator.PlanNodeIdentifier?.NodeId.ToString(CultureInfo.InvariantCulture) ?? "?"} " +
                                   $"{source.Operator.OperatorDescription}: {columns}");
            }
        }

        return builder.ToString();
    }

    private static void AppendLongest(StringBuilder builder,
                                      McpDocumentScope scope,
                                      TraceTimeline timeline,
                                      int top,
                                      bool useInstructions,
                                      int? thread,
                                      int? depth,
                                      int? node)
    {
        if (top <= 0)
        {
            throw new McpException("top must be at least 1.");
        }

        var longest = new PriorityQueue<Candidate, double>();

        var total = 0L;

        foreach (var timelineThread in timeline.Threads)
        {
            if (thread is { } wantedThread && timelineThread.ThreadId != (uint)wantedThread)
            {
                continue;
            }

            for (var rowIndex = 0; rowIndex < timelineThread.Rows.Count; rowIndex++)
            {
                if (depth is { } wantedDepth && rowIndex != wantedDepth)
                {
                    continue;
                }

                var row = timelineThread.Rows[rowIndex];

                for (var index = 0; index < row.Count; index++)
                {
                    var callNodeIndex = row.GetCallNodeIndex(index);

                    if (node is { } wantedNode && GetNodeId(scope, timeline, callNodeIndex) != wantedNode)
                    {
                        continue;
                    }

                    var to = row.GetTo(index);

                    if (!to.Position.IsKnown)
                    {
                        continue;
                    }

                    var from = row.GetFrom(index);

                    var length = useInstructions
                        ? (double)(to.Instructions - from.Instructions)
                        : timeline.Scale.GetValue(to.Position) - timeline.Scale.GetValue(from.Position);

                    total++;

                    longest.Enqueue(new Candidate(length, timelineThread.ThreadId, rowIndex, from, to, callNodeIndex, row.GetCallIndex(index), row.FlagsAt(index)),
                                    length);

                    if (longest.Count > top)
                    {
                        longest.Dequeue();
                    }
                }
            }
        }

        builder.AppendLine();

        builder.AppendLine($"longest {Math.Min(top, total)} of {Format.Number(total)} spans by {(useInstructions ? "instructions" : "position")}:");

        var table = new TextTable("Length", "Thread", "Depth", "From", "To", "Call", "Flags", "Node", "Function");

        var ranked = new List<Candidate>();

        while (longest.Count > 0)
        {
            ranked.Add(longest.Dequeue());
        }

        ranked.Reverse();

        foreach (var candidate in ranked)
        {
            var nodeId = GetNodeId(scope, timeline, candidate.CallNodeIndex);

            table.Add(useInstructions
                          ? candidate.Length.ToString("N0", CultureInfo.InvariantCulture)
                          : candidate.Length.ToString("N3", CultureInfo.InvariantCulture),
                      candidate.Thread,
                      candidate.Depth,
                      Format.Position(candidate.From),
                      Format.Position(candidate.To),
                      candidate.CallIndex < 0 ? string.Empty : candidate.CallIndex,
                      candidate.Flags.HasFlag(TraceSpanFlags.Returned) ? "ret" : string.Empty,
                      nodeId is { } id ? id : string.Empty,
                      GetFunctionName(scope, timeline, candidate.CallNodeIndex));
        }

        table.Write(builder);
    }

    private static void AppendNodeTotals(StringBuilder builder, McpDocumentScope scope, TraceTimeline timeline, int nodeId)
    {
        var count = 0L;

        var total = 0UL;

        foreach (var timelineThread in timeline.Threads)
        {
            foreach (var row in timelineThread.Rows)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    if (GetNodeId(scope, timeline, row.GetCallNodeIndex(index)) != nodeId)
                    {
                        continue;
                    }

                    var to = row.GetTo(index);

                    if (!to.Position.IsKnown)
                    {
                        continue;
                    }

                    count++;

                    total += to.Instructions - row.GetFrom(index).Instructions;
                }
            }
        }

        builder.AppendLine();

        builder.AppendLine($"node {nodeId} {scope.Context.GetFunctionName(nodeId)}: {Format.Number(count)} spans, " +
                           $"{Format.Number(total)} instructions including callees");
    }

    private static (TracePosition? First, TracePosition? Last) GetBounds(TraceTimelineThread thread)
    {
        TracePosition? first = null;

        TracePosition? last = null;

        foreach (var row in thread.Rows)
        {
            if (row.Count == 0)
            {
                continue;
            }

            var from = row.GetFromPosition(0);

            if (first is null || from < first.Value)
            {
                first = from;
            }

            var to = row.GetToPosition(row.Count - 1);

            if (to.IsKnown && (last is null || to > last.Value))
            {
                last = to;
            }
        }

        return (first, last);
    }

    private static int? GetNodeId(McpDocumentScope scope, TraceTimeline timeline, int callNodeIndex)
        => timeline.NodeOf(callNodeIndex) is { } node ? scope.Context.Nodes.GetId(node) : null;

    private static string GetFunctionName(McpDocumentScope scope, TraceTimeline timeline, int callNodeIndex)
        => timeline.NodeOf(callNodeIndex) is { } node ? scope.Context.GetFunctionName(node) : $"raw node {callNodeIndex}";

    private readonly record struct Candidate(double Length,
                                             uint Thread,
                                             int Depth,
                                             TracePoint From,
                                             TracePoint To,
                                             int CallNodeIndex,
                                             int CallIndex,
                                             TraceSpanFlags Flags);
}
