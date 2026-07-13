using System.Text;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Callstack;

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
public sealed class CallStackTree
{
    public CallStackNode Root { get; } = new();

    // Incrementing so each node records the order it was first created (first seen).
    private int _order;

    /// <summary>
    /// Adds an event's raw (unresolved) frames keyed by RVA and links the event at its leaf, returning that leaf
    /// </summary>
    public CallStackNode Add(IReadOnlyList<CallstackFrame> frames, EngineEvent engineEvent)
        => Insert(frames, RvaKey, engineEvent);

    /// <summary>
    /// Rebuilds the tree keyed on the resolved function, merging a function's call sites and repointing events
    /// </summary>
    /// <remarks>
    /// <paramref name="include"/> trims the tree to the query's scope: only kept events' frames are carried over, so a
    /// function reached only by dropped (out-of-window) events leaves no node. Null keeps every event.
    /// </remarks>
    public CallStackTree CollapseToFunctions(Func<EngineEvent, bool>? include = null)
    {
        var collapsed = new CallStackTree();

        // Insert leaves earliest-event-first so the collapsed nodes are created — and thus ordered — as first seen.
        var leaves = Nodes()
            .Where(node => node.Events.Count > 0)
            .Select(node => (Node: node, Events: include is null ? node.Events : node.Events.Where(include).ToList()))
            .Where(leaf => leaf.Events.Count > 0)
            .OrderBy(leaf => leaf.Events.Min(e => e.SequenceId));

        foreach (var (node, events) in leaves)
        {
            var leaf = collapsed.Insert([.. node.Path()], FunctionKey, engineEvent: null);

            foreach (var engineEvent in events)
            {
                leaf.Events.Add(engineEvent);

                engineEvent.CallStack = leaf;
            }
        }

        collapsed.GraftTruncatedRoots();

        return collapsed;
    }

    private CallStackNode Insert(IReadOnlyList<CallstackFrame> frames,
                                 Func<CallstackFrame, string> keyOf,
                                 EngineEvent? engineEvent)
    {
        var node = Root;

        for (var i = frames.Count - 1; i >= 0; i--)
        {
            var frame = frames[i];

            var key = keyOf(frame);

            if (!node.Children.TryGetValue(key, out var child))
            {
                child = new CallStackNode { Frame = frame, Parent = node, Key = key, Order = _order++ };

                node.Children[key] = child;
            }

            node = child;

            node.Rvas.Add(frame.Rva);
        }

        if (!node.IsRoot && engineEvent is not null)
        {
            node.Events.Add(engineEvent);
        }

        return node;
    }

    private void GraftTruncatedRoots()
    {
        var deeperByKey = Nodes().Where(node => node.Parent is { IsRoot: false })
                                 .GroupBy(node => node.Key)
                                 .ToDictionary(group => group.Key, group => group.First());

        foreach (var rootChild in Root.Children.Values.ToList())
        {
            if (deeperByKey.TryGetValue(rootChild.Key, out var deeper) && !ReferenceEquals(deeper, rootChild))
            {
                MergeInto(deeper, rootChild);

                Root.Children.Remove(rootChild.Key);
            }
        }
    }

    private static void MergeInto(CallStackNode target, CallStackNode source)
    {
        foreach (var engineEvent in source.Events)
        {
            target.Events.Add(engineEvent);

            engineEvent.CallStack = target;
        }

        foreach (var rva in source.Rvas)
        {
            target.Rvas.Add(rva);
        }

        foreach (var child in source.Children.Values)
        {
            if (target.Children.TryGetValue(child.Key, out var existing))
            {
                MergeInto(existing, child);
            }
            else
            {
                child.Parent = target;

                target.Children[child.Key] = child;
            }
        }
    }

    public long ActivityMinUs { get; private set; }

    public long ActivityMaxUs { get; private set; }

    public int ActivityBuckets { get; private set; }

    public double ActivityHeight { get; private set; }

    public int ActivityBusiest { get; private set; }

    public void ComputeActivity(long minUs, long maxUs, int buckets, double height)
    {
        ActivityMinUs = minUs;
        ActivityMaxUs = maxUs;
        ActivityBuckets = buckets;
        ActivityHeight = height;

        if (maxUs - minUs <= 0)
        {
            return;
        }

        foreach (var root in Root.Children.Values)
        {
            Accumulate(root, minUs, maxUs - minUs, buckets);
        }

        ActivityBusiest = Nodes().SelectMany(node => node.ActivityCounts).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Histogram bucket an event's timestamp falls in
    /// </summary>
    public int BucketOf(long timeUs)
    {
        var span = ActivityMaxUs - ActivityMinUs;

        if (ActivityBuckets == 0 || span <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)((timeUs - ActivityMinUs) * ActivityBuckets / span), 0, ActivityBuckets - 1);
    }

    private static int[] Accumulate(CallStackNode node, long minUs, long span, int buckets)
    {
        var bucket = new int[buckets];

        foreach (var engineEvent in node.Events)
        {
            var index = (int)((engineEvent.TimeUs - minUs) * buckets / span);

            bucket[Math.Clamp(index, 0, buckets - 1)]++;
        }

        foreach (var child in node.Children.Values)
        {
            var childBucket = Accumulate(child, minUs, span, buckets);

            for (var i = 0; i < buckets; i++)
            {
                bucket[i] += childBucket[i];
            }
        }

        node.ActivityCounts = bucket;

        return bucket;
    }

    public IEnumerable<CallStackNode> Nodes()
    {
        var stack = new Stack<CallStackNode>();

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
    public static string Render(CallStackNode node)
    {
        var builder = new StringBuilder();

        RenderNode(node, 0, builder);

        return builder.ToString();
    }

    private static void RenderNode(CallStackNode node, int depth, StringBuilder builder)
    {
        builder.Append(' ', depth * 2);

        builder.Append(node.Symbol.Length > 0 ? node.Symbol : node.Key);

        if (node.Events.Count > 0)
        {
            builder.Append($" [{node.Events.Count}]");
        }

        builder.AppendLine();

        foreach (var child in node.Children.Values.OrderBy(c => c.Order))
        {
            RenderNode(child, depth + 1, builder);
        }
    }

    private static string RvaKey(CallstackFrame frame) => $"{frame.Module}!{frame.Rva}";

    private static string FunctionKey(CallstackFrame frame) =>
        frame.Resolved is { } resolved ? $"{frame.Module}!{resolved.ClassName}::{resolved.MethodName}" : RvaKey(frame);
}
