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
        => Project(include, cutAt: null, repoint: true);

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
                                 bool repoint = false)
    {
        var projected = new CallStackTree();

        // Insert leaves earliest-event-first so the projected nodes are created — and thus ordered — as first seen.
        var leaves = Nodes()
            .Where(node => node.Events.Count > 0)
            .Select(node => (Node: node, Events: include is null ? node.Events : node.Events.Where(include).ToList()))
            .Where(leaf => leaf.Events.Count > 0)
            .OrderBy(leaf => leaf.Events.Min(e => e.SequenceId));

        foreach (var (node, events) in leaves)
        {
            // Innermost-first, so index 0 is the leaf and the last is the outermost frame captured.
            var ancestors = node.Ancestors().ToList();

            var top = cutAt is null ? ancestors.Count - 1 : ancestors.FindIndex(frame => cutAt(frame));

            // A leaf the cut never reaches is not in this segment, so it is dropped rather than inserted whole —
            // otherwise a path that missed the boundary would come in rooted at the thread start, the one thing cutting
            // is meant to remove.
            if (top < 0)
            {
                continue;
            }

            var nested = HighestBelow(ancestors, top, stopBelow);

            var path = ancestors.Take(top + 1).Skip(nested + 1);

            var leaf = projected.Insert([.. path.Select(n => n.Frame!)], FunctionKey, engineEvent: null);

            // Only a leaf reached without crossing a nested operator's frame belongs to this segment; past one, the
            // events are that operator's and just the frames above it are borrowed. Record where it was cut, so the
            // segment can show what it handed off to rather than simply stopping.
            if (nested >= 0)
            {
                leaf.CutBelow.Add(ancestors[nested]);

                continue;
            }

            foreach (var engineEvent in events)
            {
                leaf.Events.Add(engineEvent);

                if (repoint)
                {
                    engineEvent.CallStack = leaf;
                }
            }
        }

        if (cutAt is null)
        {
            projected.GraftTruncatedRoots();
        }

        return projected;
    }

    /// <summary>
    /// The outermost frame below <paramref name="top"/> that ends the segment, or -1 when the path reaches it unbroken
    /// </summary>
    /// <remarks>
    /// The HIGHEST match, not the first one walking up: an operator's exits are the entry frames of every operator
    /// beneath it, so a deep leaf crosses several. Stopping at the innermost would resume the segment inside a nested
    /// operator and take everything above it — the very work being excluded.
    /// </remarks>
    private static int HighestBelow(List<CallStackNode> ancestors, int top, Func<CallStackNode, bool>? stopBelow)
    {
        if (stopBelow is null)
        {
            return -1;
        }

        for (var i = top - 1; i >= 0; i--)
        {
            if (stopBelow(ancestors[i]))
            {
                return i;
            }
        }

        return -1;
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
                MergeInto(target, rootChild);

                // Only once the merge has actually taken everything. If it has not, the root child keeps its place:
                // visibly incomplete beats invisibly gone.
                if (rootChild.Children.Count == 0 && rootChild.Events.Count == 0)
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
    private static void MergeInto(CallStackNode target, CallStackNode source)
    {
        foreach (var engineEvent in source.Events)
        {
            target.Events.Add(engineEvent);

            engineEvent.CallStack = target;
        }

        source.Events.Clear();

        foreach (var rva in source.Rvas)
        {
            target.Rvas.Add(rva);
        }

        foreach (var child in source.Children.Values.ToList())
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

        source.Children.Clear();
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
