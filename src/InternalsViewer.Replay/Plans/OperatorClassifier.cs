using InternalsViewer.Replay.Events.EventTypes;

namespace InternalsViewer.Replay.Plans;

public static class OperatorClassifier
{
    public static bool IsHash(PlanNode n) =>
        Contains(n.PhysicalOperator, "Hash Match");

    public static bool IsNestedLoop(PlanNode n) =>
        Contains(n.PhysicalOperator, "Nested Loops");

    public static bool IsMergeJoin(PlanNode n) =>
        Contains(n.PhysicalOperator, "Merge Join");

    public static bool IsSort(PlanNode n) =>
        Contains(n.PhysicalOperator, "Sort");

    public static bool IsFilter(PlanNode n) =>
        EqualsOp(n.PhysicalOperator, "Filter");

    public static bool IsComputeScalar(PlanNode n) =>
        EqualsOp(n.PhysicalOperator, "Compute Scalar");

    public static bool IsSpool(PlanNode n) =>
        Contains(n.PhysicalOperator, "Spool");

    public static bool IsExchange(PlanNode n) =>
        Contains(n.PhysicalOperator, "Exchange");

    public static bool IsScan(PlanNode n) =>
        Contains(n.PhysicalOperator, "Scan");

    public static bool IsSeek(PlanNode n) =>
        Contains(n.PhysicalOperator, "Seek");

    public static bool IsLookup(PlanNode n) =>
        Contains(n.PhysicalOperator, "Lookup");

    public static bool IsDataAccess(PlanNode n) =>
        IsScan(n) || IsSeek(n) || IsLookup(n);

    public static bool IsLeaf(PlanNode n) =>
        IsDataAccess(n);

    public static bool IsBlocking(PlanNode n) =>
        IsHash(n) || IsSort(n) || IsSpool(n);

    public static bool IsStreaming(PlanNode n) =>
        !IsBlocking(n);

    public static bool IsJoin(PlanNode n) =>
        IsHash(n) || IsNestedLoop(n) || IsMergeJoin(n);

    public static bool IsMemoryOperator(PlanNode n) =>
        IsHash(n) || IsSort(n);

    public static bool IsLoopDriven(PlanNode n) =>
        IsNestedLoop(n);

    public static PlanNode? GetOuterChild(PlanNode n)
    {
        if (!IsNestedLoop(n) || n.Children.Count < 2)
        {
            return null;
        }

        return n.Children[0];
    }

    public static PlanNode? GetInnerChild(PlanNode n)
    {
        if (!IsNestedLoop(n) || n.Children.Count < 2)
        {
            return null;
        }

        return n.Children[1];
    }

    public static long InferStartTime(PlanNode node, 
                                      string planHandle,
                                      List<EngineEvent> events, 
                                      Func<PlanNode, long> getStart)
    {
        var identifier = new PlanNodeIdentifier
        {
            NodeId = node.NodeId,
            PlanHandle = planHandle
        };

        // 1. Hash → build child
        if (IsHash(node))
        {
            var build = GetHashBuildChild(node);

            if (build != null)
            {
                return getStart(build);
            }
        }

        // 2. Nested loop → outer child
        if (IsNestedLoop(node))
        {
            var outer = GetOuterChild(node);

            if (outer != null)
            {
                return getStart(outer);
            }
        }

        // 3. Leaf (data access) → first underlying IO; fall back to thread activity only when the
        //    operator did no IO. Taking the IO directly (rather than min'ing it with activity) keeps
        //    the start anchored to the first page read instead of an earlier operator-open event.
        if (IsLeaf(node))
        {
            var io = NodeEventHelper.GetFirstIoTime(events, identifier);

            if (io.HasValue)
            {
                return io.Value;
            }

            var activity = NodeEventHelper.GetFirstActivityTime(events, identifier);

            if (activity.HasValue)
            {
                return activity.Value;
            }
        }

        // 4. Streaming → earliest child
        if (node.Children.Count > 0)
        {
            return node.Children
                .Select(getStart)
                .Min();
        }

        // 5. Fallback → activity
        return NodeEventHelper.GetFirstActivityTime(events, identifier) ?? 0;
    }

    public static OperatorKind GetKind(PlanNode n)
    {
        if (IsHash(n))
        {
            return OperatorKind.HashJoin;
        }

        if (IsNestedLoop(n))
        {
            return OperatorKind.NestedLoop;
        }

        if (IsMergeJoin(n))
        {
            return OperatorKind.MergeJoin;
        }

        if (IsSort(n))
        {
            return OperatorKind.Sort;
        }

        if (IsFilter(n))
        {
            return OperatorKind.Filter;
        }

        if (IsComputeScalar(n))
        {
            return OperatorKind.Compute;
        }

        if (IsLookup(n))
        {
            return OperatorKind.Lookup;
        }

        if (IsScan(n) || IsSeek(n))
        {
            return OperatorKind.DataAccess;
        }

        if (IsSpool(n))
        {
            return OperatorKind.Spool;
        }

        if (IsExchange(n))
        {
            return OperatorKind.Exchange;
        }

        return OperatorKind.Unknown;
    }

    public static long InferEndTime(PlanNode node,
                                    string planHandle,
                                    List<EngineEvent> events,
                                    Func<PlanNode, long> getEnd)
    {
        var identifier = new PlanNodeIdentifier
        {
            NodeId = node.NodeId,
            PlanHandle = planHandle
        };

        if (IsLeaf(node))
        {
            var lastIo = NodeEventHelper.GetLastIoTime(events, identifier);

            // The operator's close (thread profile) is authoritative; bound the IO end by it so reads
            // on the same object that land after the operator finished (late read-ahead, or the object
            // being touched again later) don't stretch the operator to the end of the query.
            var close = NodeEventHelper.GetLastActivityTime(events, identifier);

            if (lastIo.HasValue)
            {
                return close.HasValue ? Math.Min(lastIo.Value, close.Value) : lastIo.Value;
            }

            return close ?? 0;
        }

        long end = 0;

        var lastActivityNonLeaf = NodeEventHelper.GetLastActivityTime(events, identifier);

        if (lastActivityNonLeaf.HasValue)
        {
            end = Math.Max(end, lastActivityNonLeaf.Value);
        }

        var lastIoNonLeaf = NodeEventHelper.GetLastIoTime(events, identifier);

        if (lastIoNonLeaf.HasValue)
        {
            end = Math.Max(end, lastIoNonLeaf.Value);
        }

        foreach (var child in node.Children)
        {
            var childEnd = getEnd(child);
            end = Math.Max(end, childEnd);
        }

        if (end == 0 && node.Children.Count > 0)
        {
            end = node.Children.Max(getEnd);
        }

        return end;
    }

    private static bool Contains(string s, string v) =>
        s?.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool EqualsOp(string s, string v) =>
        string.Equals(s?.Trim(), v, StringComparison.OrdinalIgnoreCase);

    public static PlanNode? GetHashBuildChild(PlanNode? hash)
    {
        if (hash == null || hash.Children.Count < 2)
        {
            return null;
        }

        if (hash.HashInfo != null && hash.HashInfo.BuildKeys.Any())
        {
            var buildTables = hash.HashInfo
                                  .BuildKeys
                                  .Select(k => k.TableKey)
                                  .ToHashSet();

            var bestMatch = FindBestMatchingChild(hash.Children, buildTables);

            if (bestMatch != null)
            {
                return bestMatch;
            }
        }

        return hash.Children
            .OrderBy(c => c.EstimatedRows)
            .First();
    }


    public static PlanNode? GetHashProbeChild(PlanNode? hash)
    {
        if (hash == null || hash.Children.Count < 2)
        {
            return null;
        }

        var build = GetHashBuildChild(hash);

        if (build == null)
        {
            return null;
        }

        return hash.Children.FirstOrDefault(c => c != build);
    }

    private static PlanNode? FindBestMatchingChild(List<PlanNode> children, HashSet<string> targetTables)
    {
        PlanNode? best = null;

        var bestScore = 0;

        foreach (var child in children)
        {
            var score = child.Outputs.Count(targetTables.Contains);

            if (score > bestScore)
            {
                bestScore = score;
                best = child;
            }
        }

        return bestScore > 0 ? best : null;
    }
}