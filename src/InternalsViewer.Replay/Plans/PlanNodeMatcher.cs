using InternalsViewer.Replay.Events;

namespace InternalsViewer.Replay.Plans;

/// <summary>
/// Associates captured storage-engine events with the physical plan operator (<see cref="PlanNode"/>)
/// that produced them, populating <see cref="EngineEvent.PlanNodeIdentifier"/>.
/// </summary>
/// <remarks>
/// Matching uses three signals, in decreasing order of confidence:
///
/// 1. <c>query_thread_profile</c> events (<see cref="QueryThreadEvent"/>) already carry the
///    operator <c>node_id</c>, which is the showplan <c>RelOp/@NodeId</c>. These are matched
///    directly and also define a per-node execution time window.
///
/// 2. Object and index identity. A page/IO/lock event resolves to a single allocation unit, so its
///    (table, index) pair usually identifies exactly one operator - a non-clustered index seek can
///    only ever run against its named index.
///
/// 3. Timing. When object identity alone is ambiguous (e.g. the same index accessed by two
///    operators in a self-join) the node whose execution window best contains the event timestamp
///    is chosen.
/// </remarks>
public static class PlanNodeMatcher
{
    public static void Match(IReadOnlyList<EngineEvent> events, IReadOnlyList<ExecutionPlan> plans)
    {
        if (events.Count == 0 || plans.Count == 0)
        {
            return;
        }

        var plansByHandle = new Dictionary<string, ExecutionPlan>(StringComparer.OrdinalIgnoreCase);

        foreach (var plan in plans)
        {
            if (!string.IsNullOrEmpty(plan.PlanHandle))
            {
                plansByHandle[plan.PlanHandle] = plan;
            }
        }

        var singlePlan = plans.Count == 1 ? plans[0] : null;

        var eventsByPlan = new Dictionary<ExecutionPlan, List<EngineEvent>>();

        foreach (var engineEvent in events)
        {
            var plan = singlePlan;

            if (plan is null && !string.IsNullOrEmpty(engineEvent.PlanHandle))
            {
                plansByHandle.TryGetValue(engineEvent.PlanHandle, out plan);
            }

            if (plan is null)
            {
                continue;
            }

            if (!eventsByPlan.TryGetValue(plan, out var list))
            {
                list = [];
                eventsByPlan[plan] = list;
            }

            list.Add(engineEvent);
        }

        foreach (var (plan, planEvents) in eventsByPlan)
        {
            MatchPlan(plan, planEvents);
        }
    }

    private static void MatchPlan(ExecutionPlan plan, List<EngineEvent> events)
    {
        foreach (var threadEvent in events.OfType<QueryThreadEvent>())
        {
            if (plan.NodesById.ContainsKey(threadEvent.NodeId))
            {
                threadEvent.PlanNodeIdentifier = new PlanNodeIdentifier
                {
                    PlanHandle = plan.PlanHandle,
                    NodeId = threadEvent.NodeId
                };
            }
        }

        var windows = BuildNodeWindows(events);

        var dataNodes = plan.NodesById.Values
                            .Where(n => !string.IsNullOrEmpty(n.Table))
                            .ToList();

        if (dataNodes.Count == 0)
        {
            return;
        }

        foreach (var engineEvent in events)
        {
            if (engineEvent.PlanNodeIdentifier is not null || string.IsNullOrEmpty(engineEvent.TableName))
            {
                continue;
            }

            var node = ResolveNode(engineEvent, dataNodes, windows);

            if (node is not null)
            {
                if (!IsIoOperator(node.PhysicalOperator))
                {
                    node = FindIoTarget(node) ?? node;
                }

                engineEvent.PlanNodeIdentifier = new PlanNodeIdentifier
                {
                    PlanHandle = plan.PlanHandle,
                    NodeId = node.NodeId
                };
            }
        }
    }

    private static PlanNode? ResolveNode(EngineEvent engineEvent,
                                         List<PlanNode> dataNodes,
                                         Dictionary<int, NodeWindow> windows)
    {
        List<PlanNode>? strong = null;
        List<PlanNode>? weak = null;

        foreach (var node in dataNodes)
        {
            if (!NameEquals(Normalise(node.Table), engineEvent.TableName))
            {
                continue;
            }

            var nodeSchema = Normalise(node.Schema);

            if (nodeSchema.Length > 0
                && engineEvent.SchemaName.Length > 0
                && !NameEquals(nodeSchema, engineEvent.SchemaName))
            {
                continue;
            }

            var nodeIndex = Normalise(node.Index);

            if (nodeIndex.Length > 0)
            {
                // Operator reads a specific index.
                if (engineEvent.IndexName.Length > 0)
                {
                    if (NameEquals(nodeIndex, engineEvent.IndexName))
                    {
                        (strong ??= []).Add(node);
                    }
                }
                else
                {
                    // Event only resolved to table level - a possible but weaker match.
                    (weak ??= []).Add(node);
                }
            }
            else
            {
                // Operator reads a heap / the table itself.
                if (engineEvent.IndexName.Length == 0)
                {
                    (strong ??= []).Add(node);
                }
                else
                {
                    (weak ??= []).Add(node);
                }
            }
        }

        return Choose(strong, engineEvent, windows) ?? Choose(weak, engineEvent, windows);
    }

    private static bool IsIoOperator(string op)
    {
        return op.Contains("Scan") ||
               op.Contains("Seek") ||
               op.Contains("Lookup");
    }

    private static PlanNode? FindIoTarget(PlanNode node)
    {
        if (IsIoOperator(node.PhysicalOperator))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var result = FindIoTarget(child);

            if (result != null)
            {
                return result;
            }
        }

        return node;
    }


    private static PlanNode? Choose(List<PlanNode>? candidates,
                                    EngineEvent engineEvent,
                                    Dictionary<int, NodeWindow> windows)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Ambiguous on identity alone: disambiguate on the operator execution windows derived from
        // the thread-profile events. Prefer a window that contains the event, else the nearest one.
        PlanNode? best = null;
        var bestDistance = long.MaxValue;
        var bestContains = false;

        foreach (var node in candidates)
        {
            if (!windows.TryGetValue(node.NodeId, out var window))
            {
                continue;
            }

            var contains = engineEvent.Timestamp >= window.Start && engineEvent.Timestamp <= window.End;
            var distance = window.DistanceTo(engineEvent.Timestamp);

            if (contains && !bestContains)
            {
                best = node;
                bestDistance = distance;
                bestContains = true;
            }
            else if (contains == bestContains && distance < bestDistance)
            {
                best = node;
                bestDistance = distance;
            }
        }

        // If timing could not separate them, leave it unmatched rather than guess wrongly.
        return best;
    }

    /// <summary>
    /// Builds an execution window per operator from its thread-profile events. The event timestamp
    /// marks operator close; <see cref="EngineEvent.Duration"/> (elapsed time) brackets the start.
    /// Multiple events for one node (parallel threads) are merged into a single spanning window.
    /// </summary>
    private static Dictionary<int, NodeWindow> BuildNodeWindows(List<EngineEvent> events)
    {
        var windows = new Dictionary<int, NodeWindow>();

        foreach (var threadEvent in events.OfType<QueryThreadEvent>())
        {
            var end = threadEvent.Timestamp;
            var start = end - TimeSpan.FromMilliseconds(Math.Max(0, threadEvent.Duration));

            if (windows.TryGetValue(threadEvent.NodeId, out var existing))
            {
                windows[threadEvent.NodeId] = new NodeWindow(
                    start < existing.Start ? start : existing.Start,
                    end > existing.End ? end : existing.End);
            }
            else
            {
                windows[threadEvent.NodeId] = new NodeWindow(start, end);
            }
        }

        return windows;
    }

    private static string Normalise(string? planName)
        => planName?.Trim('[', ']') ?? string.Empty;

    private static bool NameEquals(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private readonly record struct NodeWindow(DateTime Start, DateTime End)
    {
        public long DistanceTo(DateTime timestamp)
        {
            if (timestamp < Start)
            {
                return (Start - timestamp).Ticks;
            }

            if (timestamp > End)
            {
                return (timestamp - End).Ticks;
            }

            return 0;
        }
    }
}
