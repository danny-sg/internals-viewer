using System.Text;
using InternalsViewer.Query.Events;

namespace InternalsViewer.Query.CallStack;

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

    public bool ActivityFromTrace { get; set; }

    // Incrementing so each node records the order it was first created (first seen).
    private int _order;

    /// <summary>
    /// Adds an event's raw (unresolved) frames keyed by RVA and links the event at its leaf, returning that leaf
    /// </summary>
    public CallStackNode Add(IReadOnlyList<CallstackFrame> frames, EngineEvent engineEvent)
        => Insert(frames, RvaKey, engineEvent);

    public CallStackNode AddCall(CallStackNode parent, CallstackFrame frame, long calls)
    {
        var node = Child(parent, frame, RvaKey);

        node.Calls += calls;

        return node;
    }

    /// <summary>
    /// Rebuilds the tree keyed on the resolved function, merging a function's call sites and repointing events
    /// </summary>
    /// <remarks>
    /// <paramref name="include"/> trims the tree to the query's scope: only kept events' frames are carried over, so a
    /// function reached only by dropped (out-of-window) events leaves no node. Null keeps every event.
    /// </remarks>
    public CallStackTree CollapseToFunctions(Func<EngineEvent, bool>? include = null,
                                             Action<CallStackNode, CallStackNode>? collapsed = null)
        => Project(include, cutAt: null, repoint: true, collapsed: collapsed);

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
    ///
    /// <paramref name="repoint"/> re-links each event to its new leaf. Only the canonical collapse may do that: the
    /// tree on <see cref="Events.EngineEvent.CallStack"/> has to stay the one shared tree, so a per-scope projection
    /// must leave those pointers alone.
    /// </remarks>
    public CallStackTree Project(Func<EngineEvent, bool>? include = null,
                                 Func<CallStackNode, bool>? cutAt = null,
                                 Func<CallStackNode, bool>? stopBelow = null,
                                 bool repoint = false,
                                 Action<CallStackNode, CallStackNode>? collapsed = null)
    {
        var projected = new CallStackTree { ActivityFromTrace = ActivityFromTrace };

        var borrowed = new List<(CallStackNode Leaf, List<EngineEvent> Events)>();

        // Insert leaves earliest-event-first so the projected nodes are created — and thus ordered — as first seen.
        var leaves = Nodes()
            .Where(node => node.Events.Count > 0 || node.Calls > 0)
            .Select(node => (Node: node, Events: include is null ? node.Events : node.Events.Where(include).ToList()))
            .Where(leaf => leaf.Events.Count > 0 || leaf.Node.Calls > 0)
            .OrderBy(leaf => leaf.Events.Count > 0 ? leaf.Events.Min(e => e.SequenceId) : int.MaxValue)
            .ThenBy(leaf => leaf.Node.Order);

        var segment = new Segmenter(projected, cutAt, stopBelow);

        foreach (var (node, events) in leaves)
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
                var cut = segment.ProjectedOf(nested.Parent!);

                cut.CutBelow.Add(nested);

                borrowed.Add((cut, events));

                continue;
            }

            var leaf = segment.ProjectedOf(node);

            leaf.Calls += node.Calls;

            AddCallActivity(leaf, node.CallActivity);

            foreach (var engineEvent in events)
            {
                leaf.Events.Add(engineEvent);

                if (repoint)
                {
                    engineEvent.CallStack = leaf;
                }
            }
        }

        var merged = new Dictionary<CallStackNode, CallStackNode>();

        if (cutAt is null)
        {
            projected.GraftTruncatedRoots(merged);
        }

        if (collapsed is not null)
        {
            foreach (var (original, node) in segment.Projections)
            {
                var target = node;

                while (merged.TryGetValue(target, out var into))
                {
                    target = into;
                }

                collapsed(original, target);
            }
        }

        if (ActivityBuckets > 0)
        {
            projected.ComputeActivity(ActivityMinUs, ActivityMaxUs, ActivityBuckets);

            foreach (var (leaf, events) in borrowed)
            {
                projected.AddActivity(leaf, events);
            }
        }

        return projected;
    }

    public void RemoveCallsUnder(Func<CallStackNode, bool> boundary)
    {
        foreach (var child in Root.Children.Values.ToList())
        {
            RemoveCallsUnder(Root, child, boundary(child), boundary);
        }
    }

    private CallStackNode Insert(IReadOnlyList<CallstackFrame> frames,
                                 Func<CallstackFrame, string> keyOf,
                                 EngineEvent? engineEvent)
    {
        var node = Root;

        for (var i = frames.Count - 1; i >= 0; i--)
        {
            node = Child(node, frames[i], keyOf);
        }

        if (!node.IsRoot && engineEvent is not null)
        {
            node.Events.Add(engineEvent);
        }

        return node;
    }

    private static void AddCallActivity(CallStackNode target, int[] activity)
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

    private static bool RemoveCallsUnder(CallStackNode parent,
                                         CallStackNode node,
                                         bool below,
                                         Func<CallStackNode, bool> boundary)
    {
        if (below)
        {
            node.Calls = 0;

            node.CallActivity = [];
        }

        var keep = node.Events.Count > 0 || node.Calls > 0;

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

    private CallStackNode Child(CallStackNode parent, CallstackFrame frame, Func<CallstackFrame, string> keyOf)
    {
        var key = keyOf(frame);

        if (!parent.Children.TryGetValue(key, out var child))
        {
            child = new CallStackNode { Frame = frame, Parent = parent, Key = key, Order = _order++ };

            parent.Children[key] = child;
        }

        child.Rvas.Add(frame.Rva);

        return child;
    }

    private void GraftTruncatedRoots(Dictionary<CallStackNode, CallStackNode> merged)
    {
        var deeperByKey = Nodes().Where(node => node.Parent is { IsRoot: false })
                                 .GroupBy(node => node.Key)
                                 .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var rootChild in Root.Children.Values.ToList())
        {
            if (!deeperByKey.TryGetValue(rootChild.Key, out var candidates))
            {
                continue;
            }

            // Grafting only makes sense when the fuller copy is a SEPARATE subtree. When the deeper node lies inside the
            // root child's own subtree (a recursive frame — the same function reappears below it), re-parenting the root
            // child under its own descendant would make that descendant its own ancestor: a cycle that hangs every
            // later parent-walk (the operator icicle, tree rendering). Skip it and leave the root child in place.
            // Still attached, because the candidate list was taken before any of this ran: each merge discards the nodes
            // whose keys collided with its target's, so a copy that was in the tree when the list was built may since
            // have been merged away. Grafting onto one of those hangs the whole truncated stack off a node nothing can
            // reach — no error, no root, just frames that quietly never render again.
            var viable = candidates.Where(candidate => !ReferenceEquals(candidate, rootChild)
                                                       && !IsDescendantOf(candidate, rootChild)
                                                       && IsAttached(candidate))
                                   .ToList();

            if (GraftTarget(viable, rootChild) is { } target)
            {
                MergeInto(target, rootChild, merged);

                // Only once the merge has actually taken everything. If it has not, the root child keeps its place:
                // visibly incomplete beats invisibly gone.
                if (rootChild.Children.Count == 0 && rootChild.Events.Count == 0 && rootChild.Calls == 0)
                {
                    Root.Children.Remove(rootChild.Key);
                }
            }
        }
    }

    /// <summary>
    /// Whether the tree can still reach this node from its root
    /// </summary>
    /// <remarks>
    /// Parent alone does not answer it. A node discarded by a merge keeps pointing at the parent it had, and that parent
    /// still lists it — but under its key now sits the node it was merged into, and the chain above has been unhooked.
    /// Only walking down-links the whole way up settles it.
    /// </remarks>
    private static bool IsAttached(CallStackNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.IsRoot)
            {
                return true;
            }

            if (current.Parent is not { } parent
                || !parent.Children.TryGetValue(current.Key, out var listed)
                || !ReferenceEquals(listed, current))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Which copy of a function a truncated stack belongs under
    /// </summary>
    /// <remarks>
    /// The key names a function, and the engine re-enters plenty of them — CSQLSource::Execute and CMsqlExecContext::
    /// FExecute recur when a batch is auto-parameterised, and the iterator wrappers recur once per plan operator — so
    /// several copies can match and the choice between them is real.
    ///
    /// The callees are the tiebreak: a truncated stack can only belong under a copy that calls what it calls. But only
    /// a tiebreak. Most truncated stacks are a single leaf frame — the event site, nothing below it — so there are no
    /// callees to compare and no signal to be had, and withholding the graft on that basis strands the overwhelming
    /// majority of them at the root, which empties the tree rather than making it honest. Better an imperfect parent
    /// than no tree: fall back to the first copy, as this did before the tiebreak existed.
    /// </remarks>
    private static CallStackNode? GraftTarget(List<CallStackNode> candidates, CallStackNode rootChild)
    {
        if (candidates.Count <= 1)
        {
            return candidates.FirstOrDefault();
        }

        var ranked = candidates.Select(candidate =>
                                   (Node: candidate, Score: rootChild.Children.Keys.Count(candidate.Children.ContainsKey)))
                               .OrderByDescending(candidate => candidate.Score)
                               .ToList();

        var decisive = ranked[0].Score > 0 && ranked[0].Score > ranked[1].Score;

        return decisive ? ranked[0].Node : candidates[0];
    }

    private static bool IsDescendantOf(CallStackNode node, CallStackNode ancestor)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    // Empties the source as it goes, so "did this merge take everything?" is a question the caller can actually ask.
    // Leaving the source populated makes a partial merge indistinguishable from a complete one, and the difference is
    // whether a subtree is still reachable.
    private static void MergeInto(CallStackNode target, CallStackNode source, Dictionary<CallStackNode, CallStackNode> merged)
    {
        merged[source] = target;

        foreach (var engineEvent in source.Events)
        {
            target.Events.Add(engineEvent);

            engineEvent.CallStack = target;
        }

        source.Events.Clear();

        target.Calls += source.Calls;

        source.Calls = 0;

        AddCallActivity(target, source.CallActivity);

        source.CallActivity = [];

        foreach (var rva in source.Rvas)
        {
            target.Rvas.Add(rva);
        }

        foreach (var child in source.Children.Values.ToList())
        {
            if (target.Children.TryGetValue(child.Key, out var existing))
            {
                MergeInto(existing, child, merged);
            }
            else
            {
                child.Parent = target;

                target.Children[child.Key] = child;
            }
        }

        source.Children.Clear();
    }

    public long ActivityMinUs { get; private set; }

    public long ActivityMaxUs { get; private set; }

    public int ActivityBuckets { get; private set; }

    public int ActivityBusiest { get; private set; }

    public void ComputeActivity(long minUs, long maxUs, int buckets)
    {
        ActivityMinUs = minUs;
        ActivityMaxUs = maxUs;
        ActivityBuckets = buckets;

        if (maxUs - minUs <= 0 && !ActivityFromTrace)
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
    /// Counts events against a frame and every frame above it without linking them to it
    /// </summary>
    public void AddActivity(CallStackNode leaf, IEnumerable<EngineEvent> events)
    {
        foreach (var engineEvent in events)
        {
            if (engineEvent.TimeUs < ActivityMinUs || engineEvent.TimeUs > ActivityMaxUs)
            {
                continue;
            }

            var bucket = BucketOf(engineEvent.TimeUs);

            foreach (var node in leaf.Ancestors())
            {
                if (node.ActivityCounts.Length == ActivityBuckets)
                {
                    node.ActivityCounts[bucket]++;
                }
            }
        }
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

        if (node.CallActivity.Length == buckets)
        {
            node.CallActivity.CopyTo(bucket, 0);
        }

        foreach (var engineEvent in node.Events)
        {
            if (span <= 0 || engineEvent.TimeUs < minUs || engineEvent.TimeUs > minUs + span)
            {
                continue;
            }

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

    private static string RvaKey(CallstackFrame frame)
        => frame.Instance == 0 ? $"{frame.Module}!{frame.Rva}" : $"{frame.Module}!{frame.Rva}@{frame.Instance:X}";

    private static string FunctionKey(CallstackFrame frame)
    {
        if (frame.Resolved is not { } resolved)
        {
            return RvaKey(frame);
        }

        var function = $"{frame.Module}!{resolved.ClassName}::{resolved.MethodName}";

        return frame.Instance == 0 ? function : $"{function}@{frame.Instance:X}";
    }

    private sealed class Segmenter(CallStackTree projected,
                                   Func<CallStackNode, bool>? cutAt,
                                   Func<CallStackNode, bool>? stopBelow)
    {
        private Dictionary<CallStackNode, bool> Tops { get; } = new();

        private Dictionary<CallStackNode, bool> Members { get; } = new();

        private Dictionary<CallStackNode, CallStackNode?> Stops { get; } = new();

        private Dictionary<CallStackNode, CallStackNode> Projected { get; } = new();

        public bool Contains(CallStackNode node)
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

        public CallStackNode? StopOf(CallStackNode node)
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

        public IEnumerable<KeyValuePair<CallStackNode, CallStackNode>> Projections => Projected;

        public CallStackNode ProjectedOf(CallStackNode node)
        {
            if (!Projected.TryGetValue(node, out var result))
            {
                var parent = IsTop(node) ? projected.Root : ProjectedOf(node.Parent!);

                result = projected.Child(parent, node.Frame!, FunctionKey);

                Projected[node] = result;
            }

            return result;
        }

        private bool IsTop(CallStackNode node)
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
