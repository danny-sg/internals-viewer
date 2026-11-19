using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Helpers;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

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
    public (ulong FunctionAddress, ulong InstanceAddress) Key => Frame is { } frame ? (frame.Address, frame.InstanceAddress) : default;

    /// <summary>
    /// Creation order — nodes are created in first-seen order, so ordering by this shows the call sequence
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Whether this frame is infrastructure (Extended Events, Tracing, scheduling…) rather than query code
    /// </summary>
    public bool IsInfrastructure => Frame?.Function?.SymbolMetadata?.IsInfrastructure ?? false;

    /// <summary>
    /// The plan operator this frame implements if it is a query iterator, otherwise null
    /// </summary>
    public string? OperatorName => Frame?.Function?.OperatorName;

    public bool HasOperatorName => OperatorName is not null;

    public bool IsInstrumentation => Frame?.Function is { } function && function.SymbolCategory.IsInstrumentation(HasOperatorName);

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
    public int[] SubtreeActivityCounts => GetTree()?.GetActivityCounts(this) ?? [];

    /// <summary>
    /// The activity histogram bars to draw — only the selected node has any, so the histogram shows just for it
    /// </summary>
    /// <remarks>
    /// Set by the view when a node is selected: its subtree activity as grey bars with the selected event's time
    /// bucket highlighted. Empty on every other node, so the histogram column is drawn only for the selection.
    /// </remarks>
    public ActivityBand? ActivityBand { get; set; }

    /// <summary>
    /// Child frames keyed by identity (RVA before resolution, resolved function after) so equal frames merge to one
    /// </summary>
    internal Dictionary<(ulong FunctionAddress, ulong InstanceAddress), CallTreeNode> ChildrenByKey => AddedChildren ?? NoChildren;

    public long CallCount { get; set; }

    public bool HasCalls => CallCount > 0;

    public string CallsLabel => CountText.GetCalls(CallCount);

    public int[] OwnActivityCounts { get; set; } = [];

    public long AllocatedBytes { get; set; }

    public long AllocationCount { get; set; }

    public long AllocatedBytesIncludingChildren => Allocated.Bytes;

    public long AllocationCountIncludingChildren => Allocated.Count;

    public bool HasAllocations => Allocated.Count > 0;

    public string AllocationsLabel
        => CountText.GetText(AllocationCountIncludingChildren, "Allocation Including Children", "Allocations Including Children");

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

    public IReadOnlyCollection<CallTreeNode> CutBelow => CutBelowNodes is { } nodes
        ? nodes
        : Array.Empty<CallTreeNode>();

    private HashSet<CallTreeNode>? CutBelowNodes { get; set; }

    private static Dictionary<(ulong FunctionAddress, ulong InstanceAddress), CallTreeNode> NoChildren { get; } = [];

    private Dictionary<(ulong FunctionAddress, ulong InstanceAddress), CallTreeNode>? AddedChildren { get; set; }

    internal void AddChild(CallTreeNode child)
    {
        (AddedChildren ??= [])[child.Key] = child;
    }

    internal void RemoveChild(CallTreeNode child) => AddedChildren?.Remove(child.Key);

    internal void AddCutBelow(CallTreeNode node)
    {
        (CutBelowNodes ??= []).Add(node);
    }

    private void AddCutBelow(IReadOnlyCollection<CallTreeNode> nodes)
    {
        if (nodes.Count > 0)
        {
            (CutBelowNodes ??= []).UnionWith(nodes);
        }
    }

    public IReadOnlyList<CallTreeNode> MergedFrom { get; private init; } = [];

    public bool IsRoot => Frame is null;

    private (long Bytes, long Count)? AllocatedIncludingChildren { get; set; }

    private (long Bytes, long Count) Allocated => AllocatedIncludingChildren ??= (
        AllocatedBytes + Children.Sum(c => c.AllocatedBytesIncludingChildren),
        AllocationCount + Children.Sum(c => c.AllocationCountIncludingChildren));

    public Dictionary<(ulong FunctionAddress, ulong InstanceAddress), CallTreeNode>.ValueCollection Children => ChildrenByKey.Values;

    /// <summary>
    /// The frame's symbol category name (e.g. AccessMethod, Buffer), falling back to the module
    /// </summary>
    public string Category => Frame?.Function?.SymbolMetadata?.Name ?? Frame?.Module.Name ?? string.Empty;

    public string Symbol => Frame?.Function?.Symbol ?? string.Empty;

    /// <summary>
    /// Hex colour of the symbol category, for the node's tag
    /// </summary>
    public string CategoryColour => Frame?.Function?.SymbolMetadata?.ForegroundColor ?? "#606060";

    /// <summary>
    /// The frames from this node up to (excluding) the root, innermost first — the call path of an event linked here
    /// </summary>
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
            Order = nodes.Min(n => n.Order),
            Parent = first.Parent,
            MergedFrom = nodes,
            CallCount = nodes.Sum(n => n.CallCount),
            AllocatedBytes = nodes.Sum(n => n.AllocatedBytes),
            AllocationCount = nodes.Sum(n => n.AllocationCount),
            AllocatedIncludingChildren = (nodes.Sum(n => n.AllocatedBytesIncludingChildren),
                                          nodes.Sum(n => n.AllocationCountIncludingChildren)),
            PlanOperators = [.. nodes.SelectMany(n => n.PlanOperators).Distinct<ExecutionOperatorEvent>(ReferenceEqualityComparer.Instance)]
        };

        foreach (var node in nodes)
        {
            merged.AddCutBelow(node.CutBelow);

            if (node.Locators is { } locators)
            {
                (merged.Locators ??= new LocatorSet()).Add(locators);
            }

            merged.OwnActivityCounts = Sum(merged.OwnActivityCounts, node.OwnActivityCounts);
        }

        return merged;
    }

    public IEnumerable<CallTreeNode> GetAncestors()
    {
        for (var node = this; node is { Frame: not null }; node = node.Parent)
        {
            yield return node;
        }
    }

    public override string ToString() => GetAncestors().FirstOrDefault(node => !node.IsInfrastructure)?.Symbol ?? Symbol;

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
