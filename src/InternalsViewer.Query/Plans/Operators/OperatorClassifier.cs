using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Operators;

/// <summary>
/// Classifies physical plan operators
/// </summary>
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

    public static bool IsTop(PlanNode n) =>
        EqualsOp(n.PhysicalOperator, "Top");

    public static bool IsConcatenation(PlanNode n) =>
        EqualsOp(n.PhysicalOperator, "Concatenation");

    public static bool IsStreamAggregate(PlanNode n) =>
        EqualsOp(n.PhysicalOperator, "Stream Aggregate");

    public static bool IsAggregate(PlanNode n) =>
        Contains(n.LogicalOperator, "Aggregate") || IsStreamAggregate(n);

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
        IsScan(n) || IsSeek(n) || IsLookup(n);

    public static bool IsPartialAggregate(PlanNode n) =>
        Contains(n.LogicalOperator, "Partial");

    public static bool IsHashAggregate(PlanNode n) =>
        IsHash(n) && Contains(n.LogicalOperator, "Aggregate") && !IsPartialAggregate(n);

    public static bool IsEagerSpool(PlanNode n) =>
        IsSpool(n) && Contains(n.LogicalOperator, "Eager");

    public static bool IsBlocking(PlanNode n) =>
        IsSort(n) || IsHashAggregate(n) || IsEagerSpool(n);

    public static bool IsStreaming(PlanNode n) =>
        !IsBlocking(n);

    public static bool IsJoin(PlanNode n) =>
        IsHash(n) || IsNestedLoop(n) || IsMergeJoin(n);

    public static bool IsMemoryOperator(PlanNode n) =>
        IsHash(n) || IsSort(n);

    public static bool IsLoopDriven(PlanNode n) =>
        IsNestedLoop(n);

    public static bool IsHashJoin(PlanNode n) =>
        IsHash(n) && n.Children.Count >= 2;

    /// <summary>
    /// How <paramref name="parent"/> consumes the given <paramref name="child"/> input
    /// </summary>
    public static InputRole RoleOf(PlanNode parent, PlanNode child)
    {
        // Hash join: the build side is blocking, the probe side streams.
        if (IsHashJoin(parent))
        {
            return child == GetHashBuildChild(parent) ? InputRole.Blocking : InputRole.Streaming;
        }

        // Single-input blocking operators (sort, hash aggregate, eager spool) consume all input first.
        if (IsBlocking(parent))
        {
            return InputRole.Blocking;
        }

        return InputRole.Streaming;
    }

    /// <summary>
    /// The child that drives the operator's start - the build side of a hash join, the outer side of a
    /// nested loop - or <c>null</c> when the operator simply opens with its earliest child.
    /// </summary>
    public static PlanNode? DrivingChild(PlanNode n)
    {
        if (IsHashJoin(n))
        {
            return GetHashBuildChild(n);
        }

        if (IsNestedLoop(n))
        {
            return GetOuterChild(n);
        }

        return null;
    }

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

        // Fallback to the table with the lowest estimated rows (the build side is usually the smaller).
        var byEstimate = hash.Children
                             .Where(c => c.EstimatedRows > 0)
                             .OrderBy(c => c.EstimatedRows)
                             .FirstOrDefault();

        return byEstimate ?? hash.Children.First();
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

    private static PlanNode? FindBestMatchingChild(List<PlanNode> children, HashSet<string> targetTables)
    {
        PlanNode? best = null;

        var bestScore = 0;

        foreach (var child in children)
        {
            if (child.Outputs.Count == 0)
            {
                continue;
            }

            var score = child.Outputs.Count(targetTables.Contains);

            if (score > bestScore)
            {
                bestScore = score;
                best = child;
            }
        }

        return bestScore > 0 ? best : null;
    }

    private static bool Contains(string s, string v) =>
        s?.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool EqualsOp(string s, string v) =>
        string.Equals(s?.Trim(), v, StringComparison.OrdinalIgnoreCase);
}
