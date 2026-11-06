using System.Text;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

/// <summary>
/// The query run's whole call stack as a single tree, with events linked at the leaf of their path
/// </summary>
/// <remarks>
/// Merging every event's call stack into one structure shows the code paths the query took at a glance (execution
/// flow), keeps the per-event path (each event references its leaf node), and lets a frame be resolved once instead of
/// once per event. Returned with the query results.
///
/// Built in two stages: during parse frames are keyed by RVA (raw, unresolved) so each is resolved once; then, once
/// resolved, <see cref="CollapseToFunctions"/> rebuilds it keyed by function so a function's many call sites merge into
/// a single node (offsets kept on the node), and grafts truncated stacks onto the fuller path they belong to.
/// </remarks>
public sealed class CallTree
{
    public CallTree()
    {
        Root = new CallTreeNode { Tree = this };
    }

    public CallTreeNode Root { get; }

    // Incrementing so each node records the order it was first created (first seen).
    private int _order;

    public CallTreeNode AddCall(CallTreeNode parent, CallFrame frame, long calls)
    {
        var node = Child(parent, frame, RvaKey);

        node.Calls += calls;

        return node;
    }

    /// <summary>
    /// Rebuilds the tree keyed on the resolved function, merging a function's call sites and repointing events
    /// </summary>
    public CallTree CollapseToFunctions(Action<CallTreeNode, CallTreeNode>? collapsed = null)
        => Project(collapsed: collapsed);

    /// <summary>
    /// Rebuilds the tree keyed on the resolved function, over the events <paramref name="include"/> selects
    /// </summary>
    /// <remarks>
    /// The projected tree owns fresh nodes carrying only the included events, which is the point: a node in the shared
    /// tree holds every event that reached that function, so scoping by walking the shared nodes shows one operator's
    /// frames with the whole query's event counts on them. Projecting gives a tree that is only about the scope.
    ///
    /// <paramref name="cutAt"/> truncates each path at (and including) the first matching node, so the result is rooted
    /// at the boundary rather than the thread start — an operator's own segment rather than how it was reached.
    /// Grafting is skipped for a cut, since a cut's roots are the boundaries by design and grafting would undo them.
    ///
    /// <paramref name="stopBelow"/> is the other end of the same idea: a leaf reached THROUGH one of these frames is
    /// below the segment (it is a nested operator's work), so its path is picked up from just above that frame and its
    /// events are left out. Without it a segment cut only at the top runs all the way down to the leaves, swallowing
    /// every operator nested inside it.
    /// </remarks>
    public CallTree Project(Func<CallTreeNode, bool>? cutAt = null,
                            Func<CallTreeNode, bool>? stopBelow = null,
                            Action<CallTreeNode, CallTreeNode>? collapsed = null)
    {
        var projected = new CallTree();

        // Insert leaves earliest-event-first so the projected nodes are created — and thus ordered — as first seen.
        var leaves = Nodes().Where(node => node.Calls > 0).OrderBy(node => node.Order);

        var segment = new Segmenter(projected, cutAt, stopBelow);

        foreach (var node in leaves)
        {
            // A leaf the cut never reaches is not in this segment, so it is dropped rather than inserted whole —
            // otherwise a path that missed the boundary would come in rooted at the thread start, the one thing cutting
            // is meant to remove.
            if (!segment.Contains(node))
            {
                continue;
            }

            var nested = segment.StopOf(node);

            // Only a leaf reached without crossing a nested operator's frame belongs to this segment; past one, the
            // events are that operator's and just the frames above it are borrowed. Record where it was cut, so the
            // segment can show what it handed off to rather than simply stopping.
            if (nested is not null)
            {
                segment.ProjectedOf(nested.Parent!).CutBelow.Add(nested);

                continue;
            }

            var leaf = segment.ProjectedOf(node);

            leaf.Calls += node.Calls;

            leaf.AllocatedBytes += node.AllocatedBytes;

            leaf.Allocations += node.Allocations;

            AddLocators(leaf, node.Locators);

            AddCallActivity(leaf, GetCallActivity(node));
        }

        if (collapsed is not null)
        {
            foreach (var (original, node) in segment.Projections)
            {
                collapsed(original, node);
            }
        }

        if (ActivityBuckets > 0)
        {
            projected.ComputeActivity(ActivityBuckets);
        }

        return projected;
    }

    public void RemoveCallsUnder(Func<CallTreeNode, bool> boundary)
    {
        foreach (var child in Root.Children.Values.ToList())
        {
            RemoveCallsUnder(Root, child, boundary(child), boundary);
        }
    }

    private static void AddCallActivity(CallTreeNode target, int[] activity)
    {
        if (activity.Length == 0)
        {
            return;
        }

        if (target.CallActivity.Length != activity.Length)
        {
            target.CallActivity = new int[activity.Length];
        }

        for (var i = 0; i < activity.Length; i++)
        {
            target.CallActivity[i] += activity[i];
        }
    }

    private static bool RemoveCallsUnder(CallTreeNode parent,
                                         CallTreeNode node,
                                         bool below,
                                         Func<CallTreeNode, bool> boundary)
    {
        if (below)
        {
            node.Calls = 0;

            node.CallActivity = [];

            node.AllocatedBytes = 0;

            node.Allocations = 0;

            node.Locators = null;
        }

        var keep = node.Calls > 0;

        foreach (var child in node.Children.Values.ToList())
        {
            keep |= RemoveCallsUnder(node, child, below || boundary(child), boundary);
        }

        if (!keep)
        {
            parent.Children.Remove(node.Key);
        }

        return keep;
    }

    private CallTreeNode Child(CallTreeNode parent, CallFrame frame, Func<CallFrame, string> keyOf)
    {
        var key = keyOf(frame);

        if (!parent.Children.TryGetValue(key, out var child))
        {
            child = new CallTreeNode { Frame = frame, Parent = parent, Key = key, Order = _order++ };

            parent.Children[key] = child;
        }

        return child;
    }

    private static void AddLocators(CallTreeNode target, LocatorSet? locators)
    {
        if (locators is not null)
        {
            (target.Locators ??= new LocatorSet()).Add(locators);
        }
    }

    public int ActivityBuckets { get; private set; }

    private Lazy<IReadOnlyDictionary<CallTreeNode, int[]>>? CallActivitySource { get; set; }

    private Dictionary<CallTreeNode, int[]> ActivityCache { get; } = new(ReferenceEqualityComparer.Instance);

    public void ComputeActivity(int buckets)
    {
        ActivityBuckets = buckets;

        ClearActivity();
    }

    public void SetCallActivity(Func<IReadOnlyDictionary<CallTreeNode, int[]>> source)
    {
        CallActivitySource = new Lazy<IReadOnlyDictionary<CallTreeNode, int[]>>(source);

        ClearActivity();
    }

    public int[] GetActivityCounts(CallTreeNode node)
    {
        if (ActivityBuckets == 0)
        {
            return [];
        }

        lock (ActivityCache)
        {
            if (ActivityCache.TryGetValue(node, out var cached))
            {
                return cached;
            }
        }

        var counts = new int[ActivityBuckets];

        if (node.MergedFrom is { Count: > 0 } originals)
        {
            foreach (var original in originals)
            {
                AddCounts(counts, GetActivityCounts(original));
            }
        }
        else
        {
            AddSubtree(counts, node);
        }

        lock (ActivityCache)
        {
            ActivityCache[node] = counts;
        }

        return counts;
    }

    private int[] GetCallActivity(CallTreeNode node)
        => CallActivitySource is { } source ? source.Value.GetValueOrDefault(node) ?? [] : node.CallActivity;

    private void ClearActivity()
    {
        lock (ActivityCache)
        {
            ActivityCache.Clear();
        }
    }

    private void AddSubtree(int[] counts, CallTreeNode node)
    {
        var pending = new Stack<CallTreeNode>();

        pending.Push(node);

        while (pending.TryPop(out var current))
        {
            AddCounts(counts, GetCallActivity(current));

            foreach (var child in current.Children.Values)
            {
                pending.Push(child);
            }
        }
    }

    private static void AddCounts(int[] total, int[] counts)
    {
        if (counts.Length != total.Length)
        {
            return;
        }

        for (var index = 0; index < total.Length; index++)
        {
            total[index] += counts[index];
        }
    }

    public IEnumerable<CallTreeNode> Nodes()
    {
        var stack = new Stack<CallTreeNode>();

        stack.Push(Root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            if (!node.IsRoot)
            {
                yield return node;
            }

            foreach (var child in node.Children.Values)
            {
                stack.Push(child);
            }
        }
    }

    public string Render()
    {
        var builder = new StringBuilder();

        foreach (var child in Root.Children.Values.OrderBy(c => c.Order))
        {
            RenderNode(child, 0, builder);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders a single node and its descendants to the same indented text as <see cref="Render()"/>
    /// </summary>
    public static string Render(CallTreeNode node)
    {
        var builder = new StringBuilder();

        RenderNode(node, 0, builder);

        return builder.ToString();
    }

    private static void RenderNode(CallTreeNode node, int depth, StringBuilder builder)
    {
        builder.Append(' ', depth * 2);

        builder.Append(node.Symbol.Length > 0 ? node.Symbol : node.Key);

        if (node.Calls > 0)
        {
            builder.Append($" ({node.Calls} calls)");
        }

        builder.AppendLine();

        foreach (var child in node.Children.Values.OrderBy(c => c.Order))
        {
            RenderNode(child, depth + 1, builder);
        }
    }

    private static string RvaKey(CallFrame frame)
        => frame.InstanceAddress == 0 ? $"{frame.Module.Name}!{frame.Rva}" : $"{frame.Module.Name}!{frame.Rva}@{frame.InstanceAddress:X}";

    private static string FunctionKey(CallFrame frame)
    {
        if (frame.Function is not { } function)
        {
            return RvaKey(frame);
        }

        var key = $"{frame.Module.Name}!{function.ClassName}::{function.MethodName}";

        return frame.InstanceAddress == 0 ? key : $"{key}@{frame.InstanceAddress:X}";
    }

    private sealed class Segmenter(CallTree projected,
                                   Func<CallTreeNode, bool>? cutAt,
                                   Func<CallTreeNode, bool>? stopBelow)
    {
        private Dictionary<CallTreeNode, bool> Tops { get; } = new();

        private Dictionary<CallTreeNode, bool> Members { get; } = new();

        private Dictionary<CallTreeNode, CallTreeNode?> Stops { get; } = new();

        private Dictionary<CallTreeNode, CallTreeNode> Projected { get; } = new();

        public bool Contains(CallTreeNode node)
        {
            if (cutAt is null)
            {
                return true;
            }

            if (!Members.TryGetValue(node, out var member))
            {
                member = IsTop(node) || (node.Parent is { IsRoot: false } parent && Contains(parent));

                Members[node] = member;
            }

            return member;
        }

        public CallTreeNode? StopOf(CallTreeNode node)
        {
            if (stopBelow is null || IsTop(node))
            {
                return null;
            }

            if (!Stops.TryGetValue(node, out var stop))
            {
                stop = StopOf(node.Parent!) ?? (stopBelow(node) ? node : null);

                Stops[node] = stop;
            }

            return stop;
        }

        public IEnumerable<KeyValuePair<CallTreeNode, CallTreeNode>> Projections => Projected;

        public CallTreeNode ProjectedOf(CallTreeNode node)
        {
            if (!Projected.TryGetValue(node, out var result))
            {
                var parent = IsTop(node) ? projected.Root : ProjectedOf(node.Parent!);

                result = projected.Child(parent, node.Frame!, FunctionKey);

                result.AddPlanOperators(node.PlanOperators);

                Projected[node] = result;
            }

            return result;
        }

        private bool IsTop(CallTreeNode node)
        {
            if (!Tops.TryGetValue(node, out var top))
            {
                top = cutAt?.Invoke(node) ?? node.Parent is null or { IsRoot: true };

                Tops[node] = top;
            }

            return top;
        }
    }
}
