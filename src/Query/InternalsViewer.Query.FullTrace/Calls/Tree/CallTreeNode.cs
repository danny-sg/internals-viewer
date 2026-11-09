using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

/// <summary>
/// A single frame in the query's shared call stack tree, linking the events whose path ends here
/// </summary>
/// <remarks>
/// Every captured call stack is merged into one tree (see <see cref="CallTree"/>) so a code location appears once
/// no matter how many events hit it. A node's <see cref="Frame"/> is the canonical frame for its <c>(Module, Rva)</c> —
/// resolved once rather than per event. <see cref="Events"/> holds, by reference, the events whose innermost frame is
/// this node (the leaf link); an event's own path is recovered by walking <see cref="Parent"/> to the root.
/// </remarks>
public sealed class CallTreeNode
{
    /// <summary>
    /// The frame at this level, or null for the synthetic root
    /// </summary>
    public CallFrame? Frame { get; init; }

    // Settable so a truncated stack's node can be re-parented onto the fuller path it belongs to.
    public CallTreeNode? Parent { get; set; }

    internal CallTree? Tree { get; init; }

    /// <summary>
    /// The key this node is stored under in its parent (RVA before resolution, function after)
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Creation order — nodes are created in first-seen order, so ordering by this shows the call sequence
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Whether this frame is infrastructure (Extended Events, Tracing, scheduling…) rather than query code
    /// </summary>
    public bool IsInfrastructure => Frame?.Function?.SymbolMetadata?.IsInfrastructure ?? false;

    public bool IsInstrumentation => Frame?.Function?.SymbolCategory is { } category && category.IsInstrumentation(HasOperator);

    /// <summary>
    /// The plan operator this frame implements if it is a query iterator, otherwise null
    /// </summary>
    public string? Operator => Frame?.Function?.Iterator;

    public bool HasOperator => Operator is not null;

    /// <summary>
    /// Whether an operator's execution begins here, so the tree can be cut into a segment per operator
    /// </summary>
    public bool IsOperatorBoundary => Frame?.Function?.PlanOperator.Count > 0 && !IsConstructor;

    /// <summary>
    /// Whether this frame is where the named showplan physical operator (e.g. Clustered Index Seek) begins executing
    /// </summary>
    public bool IsEntryFrameFor(string physicalOperator)
        => !IsConstructor && (Frame?.Function?.PlanOperator.Any(pattern => pattern.Matches(physicalOperator)) ?? false);

    /// <summary>
    /// Whether this frame is the iterator's constructor rather than the iterator running
    /// </summary>
    /// <remarks>
    /// The plan is built before it runs: CQueryScan::Setup constructs every iterator, so CQScanStreamAggregateNew::
    /// CQScanStreamAggregateNew sits on the stack of anything that happens during construction — a memory wait, say.
    /// The mapping matches it, because its rules glob the function, and it would then be taken for the operator's entry
    /// and the segment rooted in the plan's construction instead of its execution.
    ///
    /// Detected by shape rather than a rule per class: a constructor's method name is its class name.
    /// </remarks>
    public bool IsConstructor => Frame?.Function is { ClassName: { Length: > 0 } className, MethodName: { } methodName }
                                 && string.Equals(className, methodName, StringComparison.Ordinal);

    /// <summary>
    /// Per-time-bucket event counts for this node's subtree, filled by <see cref="CallTree.ComputeActivity"/>
    /// </summary>
    public int[] ActivityCounts => GetTree()?.GetActivityCounts(this) ?? [];

    /// <summary>
    /// The activity histogram bars to draw — only the selected node has any, so the histogram shows just for it
    /// </summary>
    /// <remarks>
    /// Set by the view when a node is selected: its subtree activity as grey bars with the selected event's time
    /// bucket highlighted. Empty on every other node, so the histogram column is drawn only for the selection.
    /// </remarks>
    public ActivityBand? Activity { get; set; }

    /// <summary>
    /// Child frames keyed by identity (RVA before resolution, resolved function after) so equal frames merge to one
    /// </summary>
    public Dictionary<string, CallTreeNode> Children { get; } = [];

    public long CallCount { get; set; }

    public bool HasCalls => CallCount > 0;

    public string CallsLabel => CountText.GetCalls(CallCount);

    public int[] CallActivity { get; set; } = [];

    public long AllocatedBytes { get; set; }

    public long AllocationCount { get; set; }

    public long AllocatedBytesIncludingChildren => Allocated.Bytes;

    public long AllocationsIncludingChildren => Allocated.Count;

    public bool HasAllocations => Allocated.Count > 0;

    public string AllocationsLabel
        => CountText.GetText(AllocationsIncludingChildren, "Allocation Including Children", "Allocations Including Children");

    public LocatorSet? Locators { get; set; }

    public bool HasLocators => Locators is not null;

    public string LocatorsLabel => Locators?.Label ?? string.Empty;

    public string LocatorsDetail => Locators?.Detail ?? string.Empty;

    public IReadOnlyList<ExecutionOperatorEvent> PlanOperators { get; internal set; } = [];

    public bool HasTarget => TargetLabel.Length > 0;

    public string TargetLabel => string.Join(", ", PlanOperators.Select(o => o.TargetLabel).Where(l => l.Length > 0).Distinct());

    public string TargetDetail => string.Join(Environment.NewLine,
                                              PlanOperators.Select(o => $"{Category} · {o.OperatorDescription} "
                                                                        + $"(Node {o.PlanNodeIdentifier?.NodeId}) {o.TargetLabel}"));

    /// <summary>
    /// The frames a projection cut away directly below this one — where a segment handed off to a nested operator
    /// </summary>
    /// <remarks>
    /// Only ever filled on a projected tree, and only where <see cref="CallTree.Project"/> stopped. The projection
    /// knows precisely which frame ended each segment and would otherwise discard it, leaving the call tree to just stop
    /// with nothing to say the work continues in another operator.
    /// </remarks>
    public HashSet<CallTreeNode> CutBelow { get; } = [];

    public IReadOnlyList<CallTreeNode> MergedFrom { get; private init; } = [];

    public bool IsRoot => Frame is null;

    private (long Bytes, long Count)? AllocatedIncludingChildren { get; set; }

    private (long Bytes, long Count) Allocated => AllocatedIncludingChildren ??= (
        AllocatedBytes + Children.Values.Sum(c => c.AllocatedBytesIncludingChildren),
        AllocationCount + Children.Values.Sum(c => c.AllocationsIncludingChildren));

    public IEnumerable<CallTreeNode> ChildNodes => Children.Values;

    /// <summary>
    /// The frame's symbol category name (e.g. AccessMethod, Buffer), falling back to the module
    /// </summary>
    public string Category => Frame?.Function?.SymbolMetadata?.Name ?? Frame?.Module.Name ?? string.Empty;

    /// <summary>
    /// The resolved <c>Class::Method</c> of this frame
    /// </summary>
    public string Symbol => Frame?.Function is { } resolved
        ? resolved.ClassName is { Length: > 0 } className ? $"{className}::{resolved.MethodName}" : resolved.MethodName
        : string.Empty;

    /// <summary>
    /// Hex colour of the symbol category, for the node's tag
    /// </summary>
    public string CategoryColour => Frame?.Function?.SymbolMetadata?.ForegroundColor ?? "#606060";

    /// <summary>
    /// The frames from this node up to (excluding) the root, innermost first — the call path of an event linked here
    /// </summary>
    public IEnumerable<CallFrame> Path() => Ancestors().Select(node => node.Frame!);

    /// <summary>
    /// This node and its ancestors up to (excluding) the root, innermost first
    /// </summary>
    /// <remarks>
    /// The node-level <see cref="Path()"/>, for callers that need to test the nodes themselves rather than their frames
    /// — cutting the path at an operator boundary
    /// </remarks>
    public static CallTreeNode Merge(IReadOnlyList<CallTreeNode> nodes)
    {
        var first = nodes[0];

        var merged = new CallTreeNode
        {
            Frame = first.Frame,
            Key = first.Key,
            Order = nodes.Min(n => n.Order),
            Parent = first.Parent,
            MergedFrom = nodes,
            CallCount = nodes.Sum(n => n.CallCount),
            AllocatedBytes = nodes.Sum(n => n.AllocatedBytes),
            AllocationCount = nodes.Sum(n => n.AllocationCount),
            AllocatedIncludingChildren = (nodes.Sum(n => n.AllocatedBytesIncludingChildren),
                                          nodes.Sum(n => n.AllocationsIncludingChildren)),
            PlanOperators = [.. nodes.SelectMany(n => n.PlanOperators).Distinct<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance)]
        };

        foreach (var node in nodes)
        {
            merged.CutBelow.UnionWith(node.CutBelow);

            if (node.Locators is { } locators)
            {
                (merged.Locators ??= new LocatorSet()).Add(locators);
            }

            merged.CallActivity = Sum(merged.CallActivity, node.CallActivity);
        }

        return merged;
    }

    public IEnumerable<CallTreeNode> Ancestors()
    {
        for (var node = this; node is { Frame: not null }; node = node.Parent)
        {
            yield return node;
        }
    }

    public override string ToString() => Ancestors().FirstOrDefault(node => !node.IsInfrastructure)?.Symbol ?? Symbol;

    internal void AddPlanOperators(IReadOnlyList<ExecutionOperatorEvent> operators)
    {
        if (operators.Count > 0)
        {
            PlanOperators = [.. PlanOperators.Concat(operators).Distinct<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance)];
        }
    }

    private CallTree? GetTree()
    {
        var node = this;

        while (node.Parent is { } parent)
        {
            node = parent;
        }

        return node.Tree;
    }

    private static int[] Sum(int[] total, int[] counts)
    {
        if (counts.Length == 0)
        {
            return total;
        }

        if (total.Length != counts.Length)
        {
            return [.. counts];
        }

        for (var index = 0; index < total.Length; index++)
        {
            total[index] += counts[index];
        }

        return total;
    }
}
