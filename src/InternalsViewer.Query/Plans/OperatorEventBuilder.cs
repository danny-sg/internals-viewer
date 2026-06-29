using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Plans;

/// <summary>
/// Builds the operator events shown on the timeline from an execution plan and its matched engine events.
/// </summary>
/// <remarks>
/// The plan describes the operators but not when they ran; the engine events carry the timing. Timings
/// are resolved in a single bottom-up pass (children before parents):
///
/// 1. I/O - a leaf operator (scan/seek/lookup) is positioned by its own I/O events: it starts at the
///    first page access and runs for its measured duration, never ending before its last access.
///
/// 2. Blocking vs streaming - a parent is placed relative to its children by how it consumes each input:
///    <list type="bullet">
///    <item><b>Streaming</b> inputs flow through, so the operator runs concurrently with them.</item>
///    <item><b>Blocking</b> inputs must be fully consumed before output, forming a consume phase.</item>
///    </list>
///    A hash join is the hybrid case - a blocking build and a streaming probe - modelled as one operator
///    with a build (consume) phase and a probe (emit) phase.
///
/// Duration and per-thread counters (rows, elapsed) come from the plan's run-time information, so they
/// are read straight off <see cref="PlanNode"/>; the engine events only supply absolute start positions.
/// </remarks>
internal sealed class OperatorEventBuilder
{
    private readonly ExecutionPlan _plan;
    private readonly List<EngineEvent> _allEvents;
    private readonly Dictionary<int, List<EngineEvent>> _eventsByNode;
    private readonly Dictionary<int, int> _parentByNode = new();
    private readonly Dictionary<int, OperatorTiming> _timings = new();

    public OperatorEventBuilder(ExecutionPlan plan, List<EngineEvent> events)
    {
        _plan = plan;
        _allEvents = events;

        _eventsByNode = events
            .Where(e => e.PlanNodeIdentifier is { } id && id.PlanHandle == plan.PlanHandle)
            .GroupBy(e => e.PlanNodeIdentifier!.NodeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var root in plan.Root)
        {
            MapParents(root);
        }
    }

    private void MapParents(PlanNode node)
    {
        foreach (var child in node.Children)
        {
            _parentByNode[child.NodeId] = node.NodeId;
            MapParents(child);
        }
    }

    public List<ExecutionOperatorEvent> Build()
    {
        var events = new List<ExecutionOperatorEvent>(_plan.NodesById.Count);

        var sequenceOffset = 1;

        foreach (var node in _plan.NodesById.Values)
        {
            events.Add(BuildEvent(node, sequenceOffset++));
        }

        return events;
    }

    private readonly record struct OperatorTiming(long StartUs, long EmitStartUs, long EndUs);

    private OperatorTiming Timing(PlanNode node)
    {
        if (_timings.TryGetValue(node.NodeId, out var cached))
        {
            return cached;
        }

        var timing = ComputeTiming(node);
        _timings[node.NodeId] = timing;
        return timing;
    }

    private OperatorTiming ComputeTiming(PlanNode node)
    {
        var nodeEvents = EventsFor(node);

        long start;

        if (node.Children.Count == 0)
        {
            // A leaf is positioned by its own I/O (the first page access), falling back to its activity.
            start = FirstIo(nodeEvents) ?? FirstActivity(nodeEvents) ?? 0;
        }
        else
        {
            // A parent opens when its driving input opens (hash build / loop outer), else its earliest child.
            var driver = OperatorClassifier.DrivingChild(node);

            start = driver is not null
                ? Timing(driver).StartUs
                : node.Children.Min(c => Timing(c).StartUs);
        }

        // The operator runs for its measured wall-clock duration, but never ends before its own I/O, its
        // log writes, its children, or its threads.
        var end = start + node.DurationUs;

        if (LastIo(nodeEvents) is { } lastIo)
        {
            end = Math.Max(end, lastIo);
        }

        if (LastLog(nodeEvents) is { } lastLog)
        {
            end = Math.Max(end, lastLog);
        }

        foreach (var child in node.Children)
        {
            end = Math.Max(end, Timing(child).EndUs);
        }

        var longestThread = node.CountersByThread.Values.Select(c => c.ElapsedUs).DefaultIfEmpty(0).Max();

        end = Math.Max(end, start + longestThread);

        if (end <= start)
        {
            end = start + 1;
        }

        var emitStart = Math.Clamp(ComputeEmitStart(node, start, end), start, end);

        return new OperatorTiming(start, emitStart, end);
    }

    /// <summary>
    /// When rows first leave the operator. A leaf emits as it reads; a streaming operator emits once its
    /// inputs are emitting (so it inherits a blocking descendant's delay); a blocking operator emits only
    /// after consuming its blocking input(s).
    /// </summary>
    private long ComputeEmitStart(PlanNode node, long start, long end)
    {
        if (node.Children.Count == 0)
        {
            return start;
        }

        var blocking = node.Children.Where(c => OperatorClassifier.RoleOf(node, c) == InputRole.Blocking).ToList();

        if (blocking.Count == 0)
        {
            // Streaming: cannot emit before its inputs do (the latest, so all inputs are flowing).
            return node.Children.Max(c => Timing(c).EmitStartUs);
        }

        var consumeEnd = blocking.Max(c => Timing(c).EndUs);

        var streaming = node.Children.Where(c => OperatorClassifier.RoleOf(node, c) == InputRole.Streaming).ToList();

        // Emit begins when the streaming (probe) input opens, or when consumption finishes if there is none.
        return streaming.Count > 0 ? streaming.Min(c => Timing(c).StartUs) : consumeEnd;
    }

    private ExecutionOperatorEvent BuildEvent(PlanNode node, int sequenceOffset)
    {
        var (start, emitStart, end) = Timing(node);

        var operatorEvent = new ExecutionOperatorEvent
        {
            Name = node.PhysicalOperator,
            Category = OperatorClassifier.GetCategory(node),
            PlanHandle = _plan.PlanHandle,
            NodeLevel = node.NodeLevel,
            ParentNodeId = _parentByNode.TryGetValue(node.NodeId, out var parent) ? parent : null,
            Cost = OwnCost(node),
            RowsProcessed = node.RowsProcessed,
            ObjectName = ObjectName(node),
            SchemaName = node.Schema ?? string.Empty,
            TableName = node.Table ?? string.Empty,
            IndexName = node.Index ?? string.Empty,
            PlanNodeIdentifier = new PlanNodeIdentifier { NodeId = node.NodeId, PlanHandle = _plan.PlanHandle },
            TimeUs = start,
            EmitStartUs = emitStart,
            DurationUs = end - start,
            Threads = BuildThreads(node, start),
            SequenceId = NearestSequenceId(start) - sequenceOffset,
        };

        ApplyPhases(operatorEvent, node, emitStart, end);

        return operatorEvent;
    }

    /// <summary>
    /// Splits a blocking/hybrid operator into a consume (build) phase and an emit (probe) phase, around
    /// <paramref name="emitStart"/> - the consume phase is what the timeline dims. Streaming-only
    /// operators emit from their start and so have no phases.
    /// </summary>
    private void ApplyPhases(ExecutionOperatorEvent operatorEvent, PlanNode node, long emitStart, long end)
    {
        var blocking = node.Children
                           .Where(c => OperatorClassifier.RoleOf(node, c) == InputRole.Blocking)
                           .ToList();

        if (blocking.Count == 0)
        {
            return;
        }

        var consumeStart = blocking.Min(c => Timing(c).StartUs);

        operatorEvent.BuildPhaseTimeUs = consumeStart;
        operatorEvent.BuildPhaseDurationUs = Math.Max(0, emitStart - consumeStart);

        operatorEvent.ProbePhaseTimeUs = emitStart;
        operatorEvent.ProbePhaseDurationUs = Math.Max(0, end - emitStart);
    }

    /// <summary>
    /// Per-thread lanes from the plan's run-time counters. Each thread is anchored to the operator start
    /// (a thread must be running to perform the first I/O) and runs for its measured elapsed time.
    /// </summary>
    private static IReadOnlyList<OperatorThread> BuildThreads(PlanNode node, long start) =>
        node.CountersByThread
            .OrderBy(kv => kv.Key)
            .Select(kv => new OperatorThread(kv.Key, start, kv.Value.ElapsedUs, kv.Value.RowsProcessed))
            .ToList();

    // The sequence id of the latest event before the operator's start, less an offset so co-located
    // operators keep a stable order; lets the operators slot into the event grid by time.
    private int NearestSequenceId(long startUs) =>
        _allEvents.LastOrDefault(e => e.TimeUs < startUs)?.SequenceId ?? 0;

    private static double? OwnCost(PlanNode node)
    {
        if (node.EstimatedCost is not { } subtree)
        {
            return null;
        }

        // The operator's own cost: its subtree cost less its children's, so a parent doesn't
        // double-count the work feeding it. Clamped at zero to guard against rounding.
        var childCost = node.Children.Sum(c => c.EstimatedCost ?? 0);
        return Math.Max(0, subtree - childCost);
    }

    private static string ObjectName(PlanNode node)
    {
        if (string.IsNullOrEmpty(node.Schema))
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(node.Index)
            ? $"{node.Schema}.{node.Table}"
            : $"{node.Schema}.{node.Table}.{node.Index}";
    }

    private List<EngineEvent> EventsFor(PlanNode node) =>
        _eventsByNode.TryGetValue(node.NodeId, out var list) ? list : [];

    private static long? FirstIo(List<EngineEvent> events) =>
        events.Where(e => e is IoEvent).Select(e => (long?)e.TimeUs).Min();

    private static long? LastIo(List<EngineEvent> events) =>
        events.Where(e => e is IoEvent).Select(e => (long?)e.TimeUs + e.DurationUs).Max();

    private static long? LastLog(List<EngineEvent> events) =>
        events.Where(e => e is TransactionLogEvent).Select(e => (long?)e.TimeUs + e.DurationUs).Max();

    private static long? FirstActivity(List<EngineEvent> events) =>
        events.Where(e => e is QueryThreadEvent || e.Name == "sql_batch_starting")
              .Select(e => (long?)e.TimeUs)
              .Min();
}
