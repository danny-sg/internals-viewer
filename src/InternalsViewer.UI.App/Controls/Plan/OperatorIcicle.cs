using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.UI.App.Controls.Plan;

/// <summary>
/// Builds a mini icicle (flame graph) of the call stacks linked to one plan operator
/// </summary>
/// <remarks>
/// The operator's events (matched by <see cref="PlanNodeIdentifier"/>) each link to a leaf in the shared call-stack
/// tree. The icicle roots at the deepest frame those leaves share — the operator's own entry point, which trims the
/// query-wide preamble above it — and fans out downward, each frame's width proportional to the number of the
/// operator's events flowing through it.
/// </remarks>
public static class OperatorIcicle
{
    public static IReadOnlyList<IcicleSegment> Build(PlanNodeIdentifier id,
                                                     IReadOnlyList<EngineEvent> events,
                                                     double width,
                                                     double height,
                                                     int maxLevels)
    {
        // How many of the operator's events land at each call-stack leaf (a read group's pages each count once).
        var leafCounts = new Dictionary<CallStackNode, int>();

        foreach (var e in events)
        {
            if (e is ExecutionOperatorEvent || !Equals(e.PlanNodeIdentifier, id))
            {
                continue;
            }

            if (e is ReadEventGroup group)
            {
                foreach (var child in group.Events)
                {
                    Count(leafCounts, child.CallStack);
                }
            }
            else
            {
                Count(leafCounts, e.CallStack);
            }
        }

        if (leafCounts.Count == 0)
        {
            return [];
        }

        var root = CommonAncestor(leafCounts.Keys.ToList());

        if (root is null)
        {
            return [];
        }

        // Weight every frame on the operator's paths, from each leaf up to (and including) the shared root.
        var weights = new Dictionary<CallStackNode, int>();

        foreach (var (leaf, count) in leafCounts)
        {
            // One node per function means a recursive frame can make a node its own ancestor (a cycle); the visited set
            // stops the walk from looping forever.
            var seen = new HashSet<CallStackNode>();

            for (var node = leaf; node is not null && seen.Add(node); node = node.Parent)
            {
                weights[node] = weights.GetValueOrDefault(node) + count;

                if (ReferenceEquals(node, root))
                {
                    break;
                }
            }
        }

        var levels = Math.Min(MaxDepth(root, weights, 0, maxLevels) + 1, maxLevels);
        var rowHeight = height / levels;

        var segments = new List<IcicleSegment>();

        Emit(root, x: 0, width, depth: 0, maxLevels, rowHeight, weights, segments);

        return segments;
    }

    private static void Count(Dictionary<CallStackNode, int> counts, CallStackNode? leaf)
    {
        if (leaf is not null)
        {
            counts[leaf] = counts.GetValueOrDefault(leaf) + 1;
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
        segments.Add(new IcicleSegment(x, depth * rowHeight, width, rowHeight, node.CategoryColour, node.Symbol));

        if (depth + 1 >= maxLevels)
        {
            return;
        }

        var nodeWeight = weights[node];

        var cursor = x;

        foreach (var child in node.ChildNodes.Where(weights.ContainsKey).OrderBy(c => c.Order))
        {
            var childWidth = width * weights[child] / nodeWeight;

            Emit(child, cursor, childWidth, depth + 1, maxLevels, rowHeight, weights, segments);

            cursor += childWidth;
        }
    }

    private static int MaxDepth(CallStackNode node, Dictionary<CallStackNode, int> weights, int depth, int maxLevels)
    {
        if (depth + 1 >= maxLevels)
        {
            return depth;
        }

        var deepest = depth;

        foreach (var child in node.ChildNodes.Where(weights.ContainsKey))
        {
            deepest = Math.Max(deepest, MaxDepth(child, weights, depth + 1, maxLevels));
        }

        return deepest;
    }

    private static CallStackNode? CommonAncestor(List<CallStackNode> leaves)
    {
        var common = AncestorsAndSelf(leaves[0]).ToHashSet();

        for (var i = 1; i < leaves.Count; i++)
        {
            common.IntersectWith(AncestorsAndSelf(leaves[i]));
        }

        return common.OrderByDescending(DepthOf).FirstOrDefault();
    }

    private static IEnumerable<CallStackNode> AncestorsAndSelf(CallStackNode node)
    {
        // seen.Add guards against a cycle from a recursive frame (a node merged as its own ancestor).
        var seen = new HashSet<CallStackNode>();

        for (var current = node; current is { IsRoot: false } && seen.Add(current); current = current.Parent)
        {
            yield return current;
        }
    }

    private static int DepthOf(CallStackNode node)
    {
        var depth = 0;

        var seen = new HashSet<CallStackNode> { node };

        for (var current = node.Parent; current is not null && seen.Add(current); current = current.Parent)
        {
            depth++;
        }

        return depth;
    }
}
