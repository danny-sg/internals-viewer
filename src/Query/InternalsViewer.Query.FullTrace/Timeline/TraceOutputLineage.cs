using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Timeline;

internal sealed class TraceOutputLineage
{
    private TraceOutputLineage(Dictionary<short, TraceOutputStructure> structures,
                               Dictionary<ulong, Capture[]> captures,
                               Dictionary<ulong, ExecutionOperatorEvent> owners)
    {
        Structures = structures;

        Captures = captures;

        Owners = owners;
    }

    public static TraceOutputLineage Empty { get; } = new([], [], []);

    private Dictionary<short, TraceOutputStructure> Structures { get; }

    private Dictionary<ulong, Capture[]> Captures { get; }

    private Dictionary<ulong, ExecutionOperatorEvent> Owners { get; }

    public static TraceOutputLineage Build(TraceTimeline timeline,
                                           IReadOnlyList<TraceOutputStructure> structures,
                                           RowBufferStore rowBuffers)
    {
        if (structures.Count == 0)
        {
            return Empty;
        }

        var owners = timeline.GetOperatorsByInstance();

        var captures = new Dictionary<ulong, List<Capture>>();

        foreach (var row in rowBuffers.All)
        {
            if (!owners.TryGetValue(row.Iterator, out var owner)
                || timeline.FindCall(row.Function, row.Instance, row.Call) is not { } location)
            {
                continue;
            }

            var mark = new TraceRowBufferMark(owner,
                                              location.Node,
                                              location.Call,
                                              location.EndOf(TraceTimelineAxis.Position),
                                              location.EndOf(TraceTimelineAxis.Instructions));

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(captures, row.Iterator, out _);

            (list ??= []).Add(new Capture(row.Position, mark, row.Columns.Count));
        }

        var plans = new Dictionary<short, TraceOutputStructure>();

        foreach (var structure in structures.OrderByDescending(s => s.Columns.Count))
        {
            if (GetPlanHandle(structure, owners) is { } planHandle)
            {
                plans.TryAdd(planHandle, structure);
            }
        }

        return new TraceOutputLineage(plans, captures.ToDictionary(c => c.Key, c => c.Value.OrderBy(v => v.Position).ToArray()), owners);
    }

    public IReadOnlyList<TraceOutputRowSource> GetSources(TraceRowReturn root)
    {
        if (root.Operator.PlanNodeIdentifier is not { } identifier || !Structures.TryGetValue(identifier.PlanHandleId, out var structure))
        {
            return [];
        }

        if (structure.Columns.Count == 0)
        {
            return GetDirectSelectSources(structure.DirectSelectOwner, root.ToPosition);
        }

        var columns = new Dictionary<ulong, List<int>>();

        for (var ordinal = 0; ordinal < structure.Columns.Count; ordinal++)
        {
            var owner = structure.Columns[ordinal].Owner;

            if (!Owners.ContainsKey(owner))
            {
                continue;
            }

            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(columns, owner, out _);

            (list ??= []).Add(ordinal);
        }

        return [.. columns.Select(c => new TraceOutputRowSource(Owners[c.Key], GetCapture(c.Key, root.ToPosition)?.Mark, c.Value))];
    }

    private static short? GetPlanHandle(TraceOutputStructure structure, Dictionary<ulong, ExecutionOperatorEvent> owners)
    {
        foreach (var owner in structure.Columns.Select(c => c.Owner).Append(structure.DirectSelectOwner))
        {
            if (owners.TryGetValue(owner, out var operatorEvent) && operatorEvent.PlanNodeIdentifier is { } identifier)
            {
                return identifier.PlanHandleId;
            }
        }

        return null;
    }

    private IReadOnlyList<TraceOutputRowSource> GetDirectSelectSources(ulong owner, TracePosition position)
    {
        if (!Owners.TryGetValue(owner, out var operatorEvent) || GetCapture(owner, position) is not { } capture)
        {
            return [];
        }

        return [new TraceOutputRowSource(operatorEvent, capture.Mark, [.. Enumerable.Range(0, capture.ColumnCount)])];
    }

    private Capture? GetCapture(ulong owner, TracePosition position)
    {
        if (!Captures.TryGetValue(owner, out var captures))
        {
            return null;
        }

        var index = SortedSearch.FirstAfter(captures, position, c => c.Position) - 1;

        return index >= 0 ? captures[index] : null;
    }

    private sealed record Capture(TracePosition Position, TraceRowBufferMark Mark, int ColumnCount);
}
