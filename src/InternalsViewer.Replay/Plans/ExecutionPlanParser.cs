using InternalsViewer.Replay.Events.EventTypes;
using System.Timers;
using System.Xml.Linq;

namespace InternalsViewer.Replay.Plans;

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


    private static int GetIntAttribute(XElement e, string name)
        => (int?)e.Attribute(name) ?? 0;

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

            // Include the statement (SELECT/INSERT/...) node so the timeline can show it as the
            // top-level operator spanning the whole query.
            var nodes = plan.NodesById.Values;

            foreach (var node in nodes)
            {
                var startTime = timingCache.GetStartTime(node);
                var endTime = timingCache.GetEndTime(node);

                var operatorEvent = ToPlanEvent(plan.PlanHandle, node, node.NodeLevel, timingCache);

                var nearestSequenceId = events.LastOrDefault(e => e.TimeMs < startTime)?.SequenceId ?? 0;

                operatorEvent = operatorEvent with
                {
                    TimeMs = startTime,
                    Duration = Math.Max(endTime - startTime, 1),
                    PlanHandle = plan.PlanHandle,
                    SequenceId = nearestSequenceId - 1
                };

                operatorEvents.Add(operatorEvent);
            }
        }

        events.AddRange(operatorEvents);
        ComputeHashPhases(operatorEvents, executionPlans);
    }

    private static void ComputeHashPhases(
        List<ExecutionOperatorEvent> operatorEvents,
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

                current.BuildPhaseTimeMs = buildStart;
                current.ProbePhaseTimeMs = probeStart;

                var buildEndTime = Math.Min(
                    probeStart > 0 ? probeStart : currentEnd,
                    buildEnd
                );

                buildEndTime = Math.Max(buildEndTime, buildStart);

                var probeEndTime = currentEnd;

                probeEndTime = Math.Max(probeEndTime, probeStart);

                current.BuildPhaseDuration =
                    buildEndTime - buildStart;

                current.ProbePhaseDuration =
                    probeEndTime - probeStart;
            }
        }
    }

    private static long GetEnd(ExecutionOperatorEvent e)
    {
        return e.TimeMs + e.Duration;
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
                                                      int nodeLevel,
                                                      NodeTiming timing)
    {
        var planEvent = new ExecutionOperatorEvent
        {
            Name = node.PhysicalOperator,
            Category = OperatorClassifier.GetCategory(node),
            PlanHandle = planHandle,
            NodeLevel = nodeLevel,
            PlanNodeIdentifier = new PlanNodeIdentifier
            {
                NodeId = node.NodeId,
                PlanHandle = planHandle
            },
            TimeMs = timing.GetStartTime(node),
            Duration = timing.GetEndTime(node) - timing.GetStartTime(node),
        };

        return planEvent;
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
            return exec.ProbePhaseTimeMs > 0 ? exec.ProbePhaseTimeMs : exec.TimeMs;
        }

        // Default: assume streaming
        return exec.TimeMs;
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