using System.Xml.Linq;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Plans;

public static class ExecutionPlanParser
{
    public static ExecutionPlan Parse(string xml)
    {
        var doc = XDocument.Parse(xml);

        var queryPlan = doc.Descendants()
                           .FirstOrDefault(e => e.Name.LocalName == "QueryPlan");

        if (queryPlan == null)
        {
            throw new InvalidOperationException("QueryPlan element not found.");
        }

        var planHandle = GetPlanHandle(doc);

        var plan = new ExecutionPlan(planHandle);

        var rootRelationalOperators = queryPlan.Elements()
                                               .Where(e => e.Name.LocalName == "RelOp")
                                               .Select(e => ParseRelationalOperator(e, 1))
                                               .ToList();

        var statementNode = BuildStatementNode(queryPlan.Parent, rootRelationalOperators);

        plan.Root.Add(statementNode);

        foreach (var root in plan.Root)
        {
            IndexNodes(root, plan.NodesById);
        }

        return plan;
    }

    private static PlanNode BuildStatementNode(XElement? statementElement, List<PlanNode> rootRelOps)
    {
        var statementType = statementElement is null
            ? string.Empty
            : GetStringAttribute(statementElement, "StatementType");

        var subtreeCost = (statementElement is null
                           ? null 
                           : GetDoubleAttribute(statementElement, "StatementSubTreeCost"))
                          ?? rootRelOps.Sum(r => r.EstimatedCost ?? 0);

        return new PlanNode
        {
            NodeId = -1,
            IsStatement = true,
            PhysicalOperator = string.IsNullOrEmpty(statementType) ? "Statement" : statementType,
            EstimatedCost = subtreeCost,
            Children = rootRelOps
        };
    }


    public static string GetPlanHandle(XDocument doc)
    {
        var action = doc
            .Descendants()
            .FirstOrDefault(e =>
                e.Name.LocalName == "action" &&
                (string?)e.Attribute("name") == "plan_handle");

        return action?.Element("value")?.Value ?? string.Empty;
    }

    private static PlanNode ParseRelationalOperator(XElement element, int level = 1)
    {
        var node = new PlanNode
        {
            NodeId = GetIntAttribute(element, "NodeId"),
            PhysicalOperator = GetStringAttribute(element, "PhysicalOp"),
            LogicalOperator = GetStringAttribute(element, "LogicalOp"),
            NodeLevel = level
        };

        node.EstimatedCost = GetDoubleAttribute(element, "EstimatedTotalSubtreeCost");

        node.CountersByThread = ExtractThreadCounters(element);

        ExtractObjectInfo(element, node);

        node.Outputs = ExtractTables(element);

        var children = GetChildRelationalOperators(element);

        if (OperatorClassifier.IsHash(node))
        {
            node.HashInfo = ParseHashInfo(element);
        }

        foreach (var child in children)
        {
            node.Children.Add(ParseRelationalOperator(child, level + 1));
        }

        return node;
    }

    private static void IndexNodes(PlanNode node, Dictionary<int, PlanNode> dict)
    {
        dict[node.NodeId] = node;

        foreach (var child in node.Children)
        {
            IndexNodes(child, dict);
        }
    }

    private static IEnumerable<XElement> GetChildRelationalOperators(XElement element)
    {
        return element
            .Elements()
            .SelectMany(e =>
                    e.Name.LocalName == "RelOp"
                        ? new[] { e }
                        : e.Elements().Where(c => c.Name.LocalName == "RelOp")
            );
    }

    private static Dictionary<int, ThreadRuntime> ExtractThreadCounters(XElement relOp)
    {
        var counters = new Dictionary<int, ThreadRuntime>();

        var runtime = relOp.Elements().FirstOrDefault(e => e.Name.LocalName == "RunTimeInformation");

        if (runtime == null)
        {
            return counters;
        }

        foreach (var counter in runtime.Elements().Where(e => e.Name.LocalName == "RunTimeCountersPerThread"))
        {
            var thread = GetIntAttribute(counter, "Thread");
            var read = GetLongAttribute(counter, "ActualRowsRead") ?? 0;
            var output = GetLongAttribute(counter, "ActualRows") ?? 0;
            var elapsedMs = GetDoubleAttribute(counter, "ActualElapsedms") ?? 0;

            counters[thread] = new ThreadRuntime(read > 0 ? read : output, (long)(elapsedMs * 1000));
        }

        return counters;
    }

    private static int GetIntAttribute(XElement e, string name)
        => (int?)e.Attribute(name) ?? 0;

    private static long? GetLongAttribute(XElement e, string name)
        => (long?)e.Attribute(name);

    private static string GetStringAttribute(XElement e, string name)
        => (string?)e.Attribute(name) ?? string.Empty;

    private static double? GetDoubleAttribute(XElement e, string name)
        => (double?)e.Attribute(name);

    private static void ExtractObjectInfo(XElement element, PlanNode node)
    {
        var objectElement = element
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Object");

        if (objectElement == null)
        {
            return;
        }

        node.Schema = GetAttribute("Schema", objectElement);
        node.Table = GetAttribute("Table", objectElement);
        node.Index = GetAttribute("Index", objectElement);
    }

    private static string? GetAttribute(string attributeName, XElement element)
    {
        return ((string?)element.Attribute(attributeName))?.Trim('[', ']');
    }

    /// <summary>
    /// Use Query Thread Profile events to set node duration
    /// </summary>
    public static void SetNodeDurations(List<EngineEvent> events, List<ExecutionPlan> executionPlans)
    {
        foreach (var plan in executionPlans)
        {
            var queryThreadEvents =
                events.OfType<QueryThreadEvent>()
                    .Where(e => e.PlanNodeIdentifier?.PlanHandle == plan.PlanHandle)
                    .ToList();

            var nodes = plan.NodesById.Values;

            foreach (var node in nodes)
            {
                var nodeThreadEvents = queryThreadEvents.Where(e => e.PlanNodeIdentifier?.NodeId == node.NodeId)
                                                        .ToList();

                if (nodeThreadEvents.Count == 0)
                {
                    continue;
                }

                var coordinatorThread = nodeThreadEvents.FirstOrDefault(e => e.ThreadId == 0);

                node.DurationUs = coordinatorThread?.DurationUs ?? nodeThreadEvents.Max(e => e.DurationUs);
            }
        }
    }

    /// <summary>
    /// The per-thread spans for a node from its <c>query_thread_profile</c> events, ordered by thread id
    /// (coordinator 0 first). Each thread runs [close - elapsed, close].
    /// </summary>
    private static IReadOnlyList<OperatorThread> 
        BuildOperatorThreads(List<EngineEvent> events,
                             string planHandle, 
                             int nodeId,
                             IReadOnlyDictionary<int, ThreadRuntime> countersByThread)
    {
        return events
            .OfType<QueryThreadEvent>()
            .Where(e => e.PlanNodeIdentifier?.PlanHandle == planHandle
                        && e.PlanNodeIdentifier?.NodeId == nodeId)
            .OrderBy(e => e.ThreadId)
            .Select(e =>
            {
                var counters = countersByThread.GetValueOrDefault(e.ThreadId);

                // A thread's length is its measured wall-clock elapsed; fall back to the profile's total
                // (CPU/active) time when the plan has no per-thread elapsed. The absolute start is
                // anchored to the operator start by the caller, so it is left at zero here.
                var spanUs = counters.ElapsedUs > 0 ? counters.ElapsedUs : e.DurationUs;

                return new OperatorThread(e.ThreadId, 0, spanUs, counters.RowsProcessed);
            })
            .ToList();
    }

    public static void MergePlanEvents(List<EngineEvent> events, List<ExecutionPlan> executionPlans)
    {
        if (executionPlans.Count == 0)
        {
            return;
        }

        var operatorEvents = new List<ExecutionOperatorEvent>();

        foreach (var plan in executionPlans)
        {
            var timingCache = new NodeTiming(events, plan);

            timingCache.Build();

            var nodes = plan.NodesById.Values;

            var offset = 1;

            foreach (var node in nodes)
            {
                var startTime = timingCache.GetStartTime(node);
                var endTime = timingCache.GetEndTime(node);

                var operatorEvent = ToPlanEvent(plan.PlanHandle, node, node.NodeLevel);

                var nearestSequenceId = events.LastOrDefault(e => e.TimeUs < startTime)?.SequenceId ?? 0;

                // Duration is taken from the query_thread_profile (the accurate measured duration) when
                // one exists for the node; otherwise fall back to the inferred span.
                var duration = node.DurationUs > 0 ? node.DurationUs : Math.Max(endTime - startTime, 1);

                // The operator must never end before its own reads/writes: extend the duration to cover
                // the last IO tied to this node so its markers stay within the bar.
                var lastIoEnd = NodeEventHelper.GetLastIoTime(events, operatorEvent.PlanNodeIdentifier!);

                if (lastIoEnd.HasValue)
                {
                    duration = Math.Max(duration, lastIoEnd.Value - startTime);
                }

                // Per-thread spans (parallel operators): one query_thread_profile per thread, whose
                // timestamp marks its close and total_time_us its elapsed time, so start = close - elapsed.
                var threads = BuildOperatorThreads(events, plan.PlanHandle, node.NodeId, node.CountersByThread);

                // Anchor every thread to the operator's start: the first IO is performed by a thread, so
                // a thread must already be running then. Each lane then runs for its measured elapsed, so
                // the threads share the start and stagger at the end by how long each ran - rather than
                // bunching near the profile close (which leaves the start of the bar, and its early IO,
                // with no thread under it).
                threads = threads.Select(t => t with { StartUs = startTime }).ToList();

                // Cover the thread envelope so no worker lane extends past the bar.
                foreach (var t in threads)
                {
                    duration = Math.Max(duration, t.EndUs - startTime);
                }

                operatorEvent = operatorEvent with
                {
                    TimeUs = startTime,
                    DurationUs = duration,
                    Threads = threads,
                    PlanHandle = plan.PlanHandle,
                    SequenceId = nearestSequenceId - offset,
                };

                offset++;

                operatorEvents.Add(operatorEvent);
            }
        }

        events.AddRange(operatorEvents);

        ComputeHashPhases(operatorEvents, executionPlans);
    }

    private static void ComputeHashPhases(List<ExecutionOperatorEvent> operatorEvents,
                                          List<ExecutionPlan> executionPlans)
    {
        var execByNodeId = operatorEvents
            .Where(e => e.PlanNodeIdentifier != null)
            .ToDictionary(e => e.PlanNodeIdentifier!.NodeId);

        foreach (var plan in executionPlans)
        {
            foreach (var node in plan.NodesById.Values)
            {
                if (!IsHash(node))
                    continue;

                var current = execByNodeId[node.NodeId];

                var buildNode = GetHashBuildChild(node);
                var probeNode = GetHashProbeChild(node);

                if (buildNode == null || probeNode == null)
                    continue;

                var buildExec = execByNodeId[buildNode.NodeId];
                var probeExec = execByNodeId[probeNode.NodeId];

                var currentEnd = GetEnd(current);
                var buildEnd = GetEnd(buildExec);

                var buildStart = GetFirstOutputTime(buildNode, buildExec);
                var probeStart = GetFirstOutputTime(probeNode, probeExec);

                current.BuildPhaseTimeUs = buildStart;
                current.ProbePhaseTimeUs = probeStart;

                var buildEndTime = Math.Min(
                    probeStart > 0 ? probeStart : currentEnd,
                    buildEnd
                );

                buildEndTime = Math.Max(buildEndTime, buildStart);

                var probeEndTime = currentEnd;

                probeEndTime = Math.Max(probeEndTime, probeStart);

                current.BuildPhaseDurationUs =
                    buildEndTime - buildStart;

                current.ProbePhaseDurationUs =
                    probeEndTime - probeStart;
            }
        }
    }

    private static long GetEnd(ExecutionOperatorEvent e)
    {
        return e.TimeUs + e.DurationUs;
    }


    private static HashInfo ParseHashInfo(XElement hashElement)
    {
        var info = new HashInfo();

        var build = hashElement.Element("HashKeysBuild");

        if (build != null)
        {
            info.BuildKeys = ParseKeys(build);
        }

        var probe = hashElement.Element("HashKeysProbe");
        if (probe != null)
        {
            info.ProbeKeys = ParseKeys(probe);
        }

        return info;
    }

    private static List<ColumnRef> ParseKeys(XElement parent)
    {
        return parent
            .Descendants("ColumnReference")
            .Select(c => new ColumnRef
            {
                Database = GetAttribute("Database", c) ?? string.Empty,
                Schema = GetAttribute("Schema", c) ?? string.Empty,
                Table = GetAttribute("Table", c) ?? string.Empty,
                Column = GetAttribute("Column", c) ?? string.Empty
            })
            .ToList();
    }

    private static ExecutionOperatorEvent ToPlanEvent(string planHandle,
                                                      PlanNode node,
                                                      int nodeLevel)
    {
        // The operator's own cost: its subtree cost less the subtree cost of its immediate children,
        // so a parent doesn't double-count the work of the operators feeding it (matches the plan
        // view's per-node cost). Clamped at zero to guard against rounding.
        double? ownCost = null;

        if (node.EstimatedCost is { } subtree)
        {
            var childCost = node.Children.Sum(c => c.EstimatedCost ?? 0);
            ownCost = Math.Max(0, subtree - childCost);
        }

        var planEvent = new ExecutionOperatorEvent
        {
            Name = node.PhysicalOperator,
            Category = OperatorClassifier.GetCategory(node),
            PlanHandle = planHandle,
            NodeLevel = nodeLevel,
            Cost = ownCost,
            PlanNodeIdentifier = new PlanNodeIdentifier
            {
                NodeId = node.NodeId,
                PlanHandle = planHandle
            },
            RowsProcessed = node.RowsProcessed,
            ObjectName = GetNodeObjectName(node)
        };

        return planEvent;
    }

    private static string GetNodeObjectName(PlanNode node)
    {
        if (string.IsNullOrEmpty(node.Schema))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(node.Index))
        {
            return $"{node.Schema}.{node.Table}";
        }

        return $"{node.Schema}.{node.Table}.{node.Index}";
    }

    public static HashSet<string> ExtractTables(XElement nodeElement)
    {
        return nodeElement
            .Descendants("ColumnReference")
            .Select(c => $"{GetAttribute("Schema", c)}.{GetAttribute("Table", c)}")
            .Where(t => !string.IsNullOrEmpty(t))
            .Select(t => t.ToLowerInvariant())
            .ToHashSet();
    }

    public static bool IsHash(PlanNode node)
    {
        if (string.IsNullOrEmpty(node.PhysicalOperator))
            return false;

        return node.PhysicalOperator.Equals("Hash Match",
                                            StringComparison.OrdinalIgnoreCase);
    }


    private static long GetFirstOutputTime(PlanNode node, ExecutionOperatorEvent exec)
    {
        if (IsHash(node))
        {
            // Hash join produces rows during probe phase
            return exec.ProbePhaseTimeUs > 0 ? exec.ProbePhaseTimeUs : exec.TimeUs;
        }

        // Default: assume streaming
        return exec.TimeUs;
    }


    public static PlanNode? GetHashBuildChild(PlanNode hash)
    {
        if (hash.Children.Count < 2)
        {
            return null;
        }

        if (hash.HashInfo is { BuildKeys.Count: > 0 })
        {
            var buildTables = hash.HashInfo
                                  .BuildKeys
                                  .Select(k => k.TableKey)
                                  .Where(t => !string.IsNullOrEmpty(t))
                                  .ToHashSet();

            var match = FindBestMatchingChild(hash.Children, buildTables);

            if (match != null)
            {
                return match;
            }
        }

        // Fallback to table with the lowest estimated rows (if available)
        var byEstimate = hash.Children
                             .Where(c => c.EstimatedRows > 0)
                             .OrderBy(c => c.EstimatedRows)
                             .FirstOrDefault();

        if (byEstimate != null)
        {
            return byEstimate;
        }

        // Fallback to the first child if no better match is found
        return hash.Children.First();
    }

    public static PlanNode? GetHashProbeChild(PlanNode hash)
    {
        if (hash.Children.Count < 2)
        {
            return null;
        }

        var build = GetHashBuildChild(hash);

        return hash.Children.FirstOrDefault(c => c != build);
    }


    private static PlanNode? FindBestMatchingChild(List<PlanNode> children,
                                                   HashSet<string> targetTables)
    {
        PlanNode? best = null;

        var bestScore = 0;

        foreach (var child in children)
        {
            if (child.Outputs.Count == 0)
            {
                continue;
            }

            var score = child.Outputs
                .Count(targetTables.Contains);

            if (score > bestScore)
            {
                bestScore = score;
                best = child;
            }
        }

        return bestScore > 0 ? best : null;
    }
}