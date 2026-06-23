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
        Contains(n.PhysicalOperator, "Exchange") || Contains(n.PhysicalOperator, "Parallelism");

    public static bool IsScan(PlanNode n) =>
        Contains(n.PhysicalOperator, "Scan");

    public static bool IsInsert(PlanNode n) =>
        Contains(n.PhysicalOperator, "Insert");

    public static bool IsUpdate(PlanNode n) =>
        Contains(n.PhysicalOperator, "Update");

    public static bool IsDelete(PlanNode n) =>
        Contains(n.PhysicalOperator, "Delete");

    public static bool IsMerge(PlanNode n) =>
        Contains(n.PhysicalOperator, "Merge") && !IsMergeJoin(n);

    public static bool IsSeek(PlanNode n) =>
        Contains(n.PhysicalOperator, "Seek");

    public static bool IsLookup(PlanNode n) =>
        Contains(n.PhysicalOperator, "Lookup");

    public static bool IsDataAccess(PlanNode n) =>
        IsScan(n) || IsSeek(n) || IsLookup(n) || IsDataModification(n);

    public static bool IsRead(PlanNode n) =>
        IsScan(n) || IsSeek(n) || IsLookup(n);

    public static bool IsDataModification(PlanNode n) =>
        IsInsert(n) || IsUpdate(n) || IsDelete(n) || IsMerge(n);

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

        if (IsLeaf(node))
        {
            var io = NodeEventHelper.GetFirstIoTime(events, identifier);
            var write = IsDataModification(node) ? NodeEventHelper.GetFirstLogTime(events, identifier) : null;
            var first = MinNullable(io, write);

            if (first.HasValue)
            {
                return first.Value;
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

    public static OperatorCategory GetCategory(PlanNode n)
    {
        // Aggregates reshape rows. Checked first because a Hash Aggregate shares the "Hash Match"
        // physical operator with a hash join and would otherwise be miscategorised as a join.
        if (Contains(n.LogicalOperator, "Aggregate"))
        {
            return OperatorCategory.Transformation;
        }

        // Data access (IO): scans, seeks and key/RID lookups read pages.
        if (IsScan(n) || IsSeek(n) || IsLookup(n))
        {
            return OperatorCategory.DataAccess;
        }

        // Joins. Checked before modification so a "Merge Join" isn't caught by the "Merge" rule below.
        if (IsHash(n) || IsNestedLoop(n) || IsMergeJoin(n))
        {
            return OperatorCategory.Join;
        }

        // Data modification: insert/update/delete/merge write to the table and transaction log.
        if (IsDataModification(n))
        {
            return OperatorCategory.Modification;
        }

        // Buffering / blocking: spools, sort (materialises rows) and exchange (parallelism buffers).
        if (IsSpool(n) || IsSort(n) || IsExchange(n))
        {
            return OperatorCategory.Buffer;
        }

        // Explicit transformations.
        if (IsFilter(n) || IsComputeScalar(n))
        {
            return OperatorCategory.Transformation;
        }

        // Fallback: Top, Concatenation, Segment, Window, and other row-shaping operators.
        return OperatorCategory.Transformation;
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
            var lastWrite = IsDataModification(node) ? NodeEventHelper.GetLastLogTime(events, identifier) : null;
            var last = MaxNullable(lastIo, lastWrite);

            // The operator's close (thread profile) is authoritative; bound the IO/log end by it so
            // work on the same object that lands after the operator finished (late read-ahead, a later
            // log flush, or the object being touched again) doesn't stretch it to the end of the query.
            var close = NodeEventHelper.GetLastActivityTime(events, identifier);

            if (last.HasValue)
            {
                return close.HasValue ? Math.Min(last.Value, close.Value) : last.Value;
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

    private static long? MinNullable(long? a, long? b) =>
        a.HasValue ? (b.HasValue ? Math.Min(a.Value, b.Value) : a) : b;

    private static long? MaxNullable(long? a, long? b) =>
        a.HasValue ? (b.HasValue ? Math.Max(a.Value, b.Value) : a) : b;

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