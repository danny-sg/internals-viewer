using System.Runtime.InteropServices;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Timeline;

internal sealed class TraceOutputLineage
{
    private const ulong MaximumIteratorSize = 0x400;

    private TraceOutputLineage(Dictionary<short, TraceOutputStructure> structures,
                               Dictionary<ulong, Capture[]> captures,
                               Dictionary<ulong, ExecutionOperatorEvent> owners,
                               RowBufferStore rowBuffers,
                               BatchStore batches)
    {
        Structures = structures;

        Captures = captures;

        Owners = owners;

        RowBuffers = rowBuffers;

        Batches = batches;
    }

    public static TraceOutputLineage Empty { get; } = new([], [], [], RowBufferStore.Empty, BatchStore.Empty);

    private Dictionary<short, TraceOutputStructure> Structures { get; }

    private Dictionary<ulong, Capture[]> Captures { get; }

    private Dictionary<ulong, ExecutionOperatorEvent> Owners { get; }

    private RowBufferStore RowBuffers { get; }

    private BatchStore Batches { get; }

    public static TraceOutputLineage Build(TraceTimeline timeline,
                                           IReadOnlyList<TraceOutputStructure> structures,
                                           RowBufferStore rowBuffers,
                                           BatchStore batches)
    {
        if (structures.Count == 0)
        {
            return Empty;
        }

        var operators = timeline.GetOperatorsByInstance();

        var owners = GetOwners(operators, structures, rowBuffers, batches);

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

            (list ??= []).Add(new Capture(row.Position, mark, row));
        }

        var plans = new Dictionary<short, TraceOutputStructure>();

        foreach (var structure in structures.Select(s => GetResolved(s, operators)).OrderByDescending(s => s.Columns.Count))
        {
            if (GetPlanHandle(structure, owners, batches) is { } planHandle)
            {
                plans.TryAdd(planHandle, structure);
            }
        }

        return new TraceOutputLineage(plans,
                                      captures.ToDictionary(c => c.Key, c => c.Value.OrderBy(v => v.Position).ToArray()),
                                      owners,
                                      rowBuffers,
                                      batches);
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

        return [.. columns.Select(c => GetSource(c.Key, structure, c.Value, root.ToPosition))];
    }

    private static Dictionary<ulong, ExecutionOperatorEvent> GetOwners(Dictionary<ulong, ExecutionOperatorEvent> operators,
                                                                       IReadOnlyList<TraceOutputStructure> structures,
                                                                       RowBufferStore rowBuffers,
                                                                       BatchStore batches)
    {
        var owners = new Dictionary<ulong, ExecutionOperatorEvent>(operators);

        var iterators = structures.SelectMany(s => s.Columns.Select(c => c.Owner).Append(s.DirectSelectOwner))
                                  .Concat(rowBuffers.All.Select(r => r.Iterator));

        foreach (var iterator in iterators)
        {
            if (!owners.ContainsKey(iterator)
                && batches.GetProducer(iterator) is { } producer
                && operators.TryGetValue(producer, out var operatorEvent))
            {
                owners[iterator] = operatorEvent;
            }
        }

        return owners;
    }

    private static TraceOutputStructure GetResolved(TraceOutputStructure structure, Dictionary<ulong, ExecutionOperatorEvent> operators)
    {
        if (!structure.Columns.Any(IsUnowned))
        {
            return structure;
        }

        return structure with
        {
            Columns = [.. structure.Columns.Select(c => IsUnowned(c) ? c with { Owner = GetEnclosingIterator(c.Address, operators.Keys) } : c)]
        };
    }

    private static bool IsUnowned(TraceOutputSource source) => source is { Type: OutputSourceType.Buffer, Owner: 0, Address: not 0 };

    private static ulong GetEnclosingIterator(ulong address, IEnumerable<ulong> iterators)
        => iterators.Where(i => i <= address && address - i < MaximumIteratorSize).DefaultIfEmpty().Max();

    private static short? GetPlanHandle(TraceOutputStructure structure,
                                        Dictionary<ulong, ExecutionOperatorEvent> owners,
                                        BatchStore batches)
    {
        foreach (var owner in structure.Columns.Select(c => c.Owner).Append(structure.DirectSelectOwner))
        {
            if (GetPlanNode(owner, owners, batches) is { } identifier)
            {
                return identifier.PlanHandleId;
            }
        }

        return null;
    }

    private static PlanNodeIdentifier? GetPlanNode(ulong owner, Dictionary<ulong, ExecutionOperatorEvent> owners, BatchStore batches)
    {
        if (owners.GetValueOrDefault(owner)?.PlanNodeIdentifier is { } identifier)
        {
            return identifier;
        }

        return batches.GetProducer(owner) is { } producer ? owners.GetValueOrDefault(producer)?.PlanNodeIdentifier : null;
    }

    private IReadOnlyList<TraceOutputRowSource> GetDirectSelectSources(ulong owner, TracePosition position)
    {
        if (!Owners.TryGetValue(owner, out var operatorEvent) || GetCapture(owner, position) is not { } capture)
        {
            return [];
        }

        TraceOutputColumn[] columns = [.. capture.Row.Columns.Select(c => GetColumn(c.Ordinal, capture.Row, c))];

        return [new TraceOutputRowSource(operatorEvent, capture.Mark, columns)];
    }

    private TraceOutputRowSource GetSource(ulong owner, TraceOutputStructure structure, List<int> ordinals, TracePosition position)
    {
        var capture = GetCapture(owner, position);

        TraceOutputColumn[] columns =
            [.. ordinals.Select(o => GetColumn(o, capture?.Row, GetRowColumn(structure.Columns[o], capture?.Row)))];

        return new TraceOutputRowSource(Owners[owner], capture?.Mark, columns);
    }

    private static RowBufferColumn? GetRowColumn(TraceOutputSource source, RowBuffer? row)
    {
        if (row is null || source.Type != OutputSourceType.Buffer || source.Address < row.Address)
        {
            return null;
        }

        var ordinal = RowBufferStore.GetColumn(row, source.Address - row.Address);

        return ordinal >= 0 && ordinal < row.Columns.Count ? row.Columns[ordinal] : null;
    }

    private TraceOutputColumn GetColumn(int ordinal, RowBuffer? row, RowBufferColumn? column)
        => new(ordinal, column, row is not null && column is not null ? RowBuffers.GetSource(row, column, Batches) : null);

    private Capture? GetCapture(ulong owner, TracePosition position)
    {
        if (!Captures.TryGetValue(owner, out var captures))
        {
            return null;
        }

        var index = SortedSearch.FirstAfter(captures, position, c => c.Position) - 1;

        return index >= 0 ? captures[index] : null;
    }

    private sealed record Capture(TracePosition Position, TraceRowBufferMark Mark, RowBuffer Row);
}
