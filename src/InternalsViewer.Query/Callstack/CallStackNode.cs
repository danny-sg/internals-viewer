using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Callstack;

/// <summary>
/// A single frame in the query's shared call stack tree, linking the events whose path ends here
/// </summary>
/// <remarks>
/// Every captured call stack is merged into one tree (see <see cref="CallStackTree"/>) so a code location appears once
/// no matter how many events hit it. A node's <see cref="Frame"/> is the canonical frame for its <c>(Module, Rva)</c> —
/// resolved once rather than per event. <see cref="Events"/> holds, by reference, the events whose innermost frame is
/// this node (the leaf link); an event's own path is recovered by walking <see cref="Parent"/> to the root.
/// </remarks>
public sealed class CallStackNode
{
    /// <summary>
    /// The frame at this level, or null for the synthetic root
    /// </summary>
    public CallstackFrame? Frame { get; init; }

    // Settable so a truncated stack's node can be re-parented onto the fuller path it belongs to.
    public CallStackNode? Parent { get; set; }

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
    public bool IsInfrastructure => Frame?.Resolved?.SymbolMetadata?.IsInfrastructure ?? false;

    /// <summary>
    /// The plan operator this frame implements if it is a query iterator, otherwise null
    /// </summary>
    public string? Operator => Frame?.Resolved?.Iterator;

    public bool HasOperator => Operator is not null;

    /// <summary>
    /// Per-time-bucket event counts for this node's subtree, filled by <see cref="CallStackTree.ComputeActivity"/>
    /// </summary>
    public int[] ActivityCounts { get; set; } = [];

    /// <summary>
    /// The activity histogram bars to draw — only the selected node has any, so the histogram shows just for it
    /// </summary>
    /// <remarks>
    /// Set by the view when a node is selected: its subtree activity as grey bars with the selected event's time
    /// bucket highlighted. Empty on every other node, so the histogram column is drawn only for the selection.
    /// </remarks>
    public IReadOnlyList<ActivityBar> DisplayBars { get; set; } = [];

    public bool HasHistogram => DisplayBars.Count > 0;

    /// <summary>
    /// Child frames keyed by identity (RVA before resolution, resolved function after) so equal frames merge to one
    /// </summary>
    public Dictionary<string, CallStackNode> Children { get; } = new();

    /// <summary>
    /// The call-site offsets (RVAs) merged into this node — one when keyed by RVA, several once collapsed to a function
    /// </summary>
    public HashSet<uint> Rvas { get; } = [];

    /// <summary>
    /// Events whose innermost (event-site) frame is this node — held by reference, not duplicated
    /// </summary>
    public List<EngineEvent> Events { get; } = [];

    public bool IsRoot => Frame is null;

    public IEnumerable<CallStackNode> ChildNodes => Children.Values;

    /// <summary>
    /// The frame's symbol category name (e.g. AccessMethod, Buffer), falling back to the module
    /// </summary>
    public string Category => Frame?.Resolved?.SymbolMetadata?.Name ?? Frame?.Module ?? string.Empty;

    /// <summary>
    /// The resolved <c>Class::Method</c> of this frame
    /// </summary>
    public string Symbol => Frame?.Resolved is { } resolved
        ? resolved.ClassName is { Length: > 0 } className ? $"{className}::{resolved.MethodName}" : resolved.MethodName
        : string.Empty;

    /// <summary>
    /// Hex colour of the symbol category, for the node's tag
    /// </summary>
    public string CategoryColour => Frame?.Resolved?.SymbolMetadata?.ForegroundColor ?? "#606060";

    /// <summary>
    /// The frames from this node up to (excluding) the root, innermost first — the call path of an event linked here
    /// </summary>
    public IEnumerable<CallstackFrame> Path()
    {
        for (var node = this; node is { Frame: { } frame }; node = node.Parent)
        {
            yield return frame;
        }
    }

    /// <summary>
    /// Total events at this node and everything beneath it (for icicle-style weighting)
    /// </summary>
    public int SubtreeEventCount()
    {
        var count = Events.Count;

        foreach (var child in Children.Values)
        {
            count += child.SubtreeEventCount();
        }

        return count;
    }
}
