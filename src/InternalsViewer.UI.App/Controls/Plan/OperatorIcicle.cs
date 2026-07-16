using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Controls.Plan;

/// <summary>
/// Builds a mini icicle (flame graph) of the call stack an operator executed
/// </summary>
/// <remarks>
/// The same segment the Callstack view isolates, drawn: rooted at the operator's entry frames, weighted by the events
/// its plan subtree drove, and stopping where a child operator or a page fetch takes over. Each frame's width is the
/// share of those events that flowed through it.
///
/// The subtree's events, not the operator's own: only the data-access leaves emit anything, so weighting a Hash Match
/// by events carrying its own node id would leave every operator above the scans blank.
///
/// Runs that every event walks straight through collapse to one row (see <see cref="Tail"/>). In a strip this short the
/// rows are the scarce thing, and a segment is mostly one such run.
/// </remarks>
public static class OperatorIcicle
{
    public static IReadOnlyList<IcicleSegment> Build(ExecutionOperatorEvent operatorEvent,
                                                     OperatorHierarchy hierarchy,
                                                     IReadOnlyList<EngineEvent> events,
                                                     double width,
                                                     double height,
                                                     int maxLevels)
    {
        // No entry frame means the operator's frames were never located, so there is no segment to draw. Falling back to
        // whatever the leaves happen to share would root the chart at some caller above it and draw that instead.
        if (operatorEvent.EntryFrames.Count == 0)
        {
            return [];
        }

        var scope = hierarchy.ScopeOf(operatorEvent, events);

        if (scope.Count == 0)
        {
            return [];
        }

        var entries = operatorEvent.EntryFrames.ToHashSet();

        var exits = operatorEvent.ExitFrames.ToHashSet();

        var weights = new Dictionary<CallStackNode, int>();

        foreach (var scoped in scope)
        {
            if (scoped.CallStack is { } leaf)
            {
                Accumulate(leaf, entries, exits, weights);
            }
        }

        var roots = operatorEvent.EntryFrames.Where(weights.ContainsKey).OrderBy(entry => entry.Order).ToList();

        if (roots.Count == 0)
        {
            return [];
        }

        var levels = Math.Min(roots.Max(root => MaxDepth(root, weights, 0, maxLevels)) + 1, maxLevels);

        var rowHeight = height / levels;

        // Several entry frames is an operator entered on more than one branch — a hash join's build and its probe. They
        // sit side by side, split by how much of the work came through each, rather than one being picked to stand for
        // the operator.
        var total = roots.Sum(root => weights[root]);

        var segments = new List<IcicleSegment>();

        var cursor = 0d;

        foreach (var root in roots)
        {
            var rootWidth = width * weights[root] / total;

            Emit(root, cursor, rootWidth, depth: 0, maxLevels, rowHeight, weights, segments);

            cursor += rootWidth;
        }

        return segments;
    }

    /// <summary>
    /// Adds one event's weight to every frame of the operator's segment its stack passed through
    /// </summary>
    /// <remarks>
    /// Walked from the leaf, which is the only end that knows which operator this is: the frames are shared between
    /// every operator running the same code, and only the event carries the plan node.
    /// </remarks>
    private static void Accumulate(CallStackNode leaf,
                                   HashSet<CallStackNode> entries,
                                   HashSet<CallStackNode> exits,
                                   Dictionary<CallStackNode, int> weights)
    {
        var path = new List<CallStackNode>();

        // One node per function means a recursive frame can be its own ancestor; the visited set stops the walk looping.
        var seen = new HashSet<CallStackNode>();

        CallStackNode? entry = null;

        for (var node = leaf; node is { IsRoot: false } && seen.Add(node); node = node.Parent)
        {
            path.Add(node);

            if (entries.Contains(node))
            {
                entry = node;

                break;
            }
        }

        // The stack never passed through this operator, so the event is not its work however it was matched.
        if (entry is null)
        {
            return;
        }

        // Where the segment ends on this branch: the OUTERMOST child-operator entry or page fetch below the entry frame,
        // since a deep leaf crosses several and it is the highest that this operator stopped at. Its own weight counts —
        // the hand-off happened here — but nothing under it does, that being the other operator's or the read's own work.
        var bottom = 0;

        for (var i = path.Count - 2; i >= 0; i--)
        {
            if (exits.Contains(path[i]) || path[i].IsAccessBarrier)
            {
                bottom = i;

                break;
            }
        }

        for (var i = bottom; i < path.Count; i++)
        {
            weights[path[i]] = weights.GetValueOrDefault(path[i]) + 1;
        }
    }

    private static void Emit(CallStackNode node,
                             double x,
                             double width,
                             int depth,
                             int maxLevels,
                             double rowHeight,
                             Dictionary<CallStackNode, int> weights,
                             List<IcicleSegment> segments)
    {
        var tail = Tail(node, weights);

        // The run gets ONE row, named for both ends: an operator's segment is mostly a chain that every event walks
        // straight down (a scan is seven frames deep before it first branches), and a row per frame spends the whole
        // strip drawing the same full-width rectangle over and over, leaving the part that does vary a sliver at the
        // bottom. Collapsed, the frame that branches is at the top and the branch is the row under it.
        var symbol = ReferenceEquals(tail, node) ? node.Symbol : $"{node.Symbol} → {tail.Symbol}";

        segments.Add(new IcicleSegment(x, depth * rowHeight, width, rowHeight, node.CategoryColour, symbol));

        if (depth + 1 >= maxLevels)
        {
            return;
        }

        var nodeWeight = weights[tail];

        var cursor = x;

        // Children can weigh less than their parent, leaving a gap at the right: the events that stopped at this frame.
        foreach (var child in tail.ChildNodes.Where(weights.ContainsKey).OrderBy(c => c.Order))
        {
            var childWidth = width * weights[child] / nodeWeight;

            Emit(child, cursor, childWidth, depth + 1, maxLevels, rowHeight, weights, segments);

            cursor += childWidth;
        }
    }

    /// <summary>
    /// The innermost frame of the pass-through run starting here — the last frame every one of the events reached
    /// </summary>
    /// <remarks>
    /// A frame whose only weighted child carries its whole weight tells the chart nothing the child does not: same
    /// count, same full width. The run ends where the weight splits or drops, which is the first thing worth a row.
    /// </remarks>
    private static CallStackNode Tail(CallStackNode node, Dictionary<CallStackNode, int> weights)
    {
        // One node per function means a recursive frame can be its own descendant; the visited set stops the walk
        // looping through it forever.
        var seen = new HashSet<CallStackNode> { node };

        var tail = node;

        while (true)
        {
            var children = tail.ChildNodes.Where(weights.ContainsKey).ToList();

            if (children is not [var only] || weights[only] != weights[tail] || !seen.Add(only))
            {
                return tail;
            }

            tail = only;
        }
    }

    private static int MaxDepth(CallStackNode node, Dictionary<CallStackNode, int> weights, int depth, int maxLevels)
    {
        if (depth + 1 >= maxLevels)
        {
            return depth;
        }

        var deepest = depth;

        // Mirrors Emit's collapse, or the strip is cut into rows for frames that never get one and every chart ends up
        // squashed into its top half.
        foreach (var child in Tail(node, weights).ChildNodes.Where(weights.ContainsKey))
        {
            deepest = Math.Max(deepest, MaxDepth(child, weights, depth + 1, maxLevels));
        }

        return deepest;
    }
}
