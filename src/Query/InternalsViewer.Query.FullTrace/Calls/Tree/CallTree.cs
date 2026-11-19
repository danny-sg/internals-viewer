using System.Text;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Calls.Tree;

public sealed class CallTree
{
    public CallTree()
    {
        Root = new CallTreeNode { Tree = this };
    }

    public CallTreeNode Root { get; }

    private int NextOrder { get; set; }

    public CallTreeNode AddCall(CallTreeNode parent, CallFrame frame, long callCount)
    {
        var node = GetChild(parent, frame);

        node.CallCount += callCount;

        return node;
    }

    public CallTree Project(Func<CallTreeNode, bool>? cutAt = null,
                            Func<CallTreeNode, bool>? stopBelow = null,
                            Action<CallTreeNode, CallTreeNode>? onProjected = null)
    {
        var projected = new CallTree();

        // Insert leaves earliest-event-first so the projected nodes are created — and thus ordered — as first seen.
        var leaves = GetNodes().Where(node => node.CallCount > 0).OrderBy(node => node.Order);

        var segment = new Segmenter(projected, cutAt, stopBelow);

        foreach (var node in leaves)
        {
            if (!segment.Contains(node))
            {
                continue;
            }

            var nested = segment.GetStop(node);

            // Only a leaf reached without crossing a nested operator's frame belongs to this segment; past one, the
            // events are that operator's and just the frames above it are borrowed. Record where it was cut, so the
            // segment can show what it handed off to rather than simply stopping.
            if (nested is not null)
            {
                segment.GetProjected(nested.Parent!).AddCutBelow(nested);

                continue;
            }

            var leaf = segment.GetProjected(node);

            leaf.CallCount += node.CallCount;

            leaf.AllocatedBytes += node.AllocatedBytes;

            leaf.AllocationCount += node.AllocationCount;

            AddLocators(leaf, node.Locators);

            AddCallActivity(leaf, GetCallActivity(node));
        }

        if (onProjected is not null)
        {
            foreach (var (original, node) in segment.Projections)
            {
                onProjected(original, node);
            }
        }

        if (ActivityBuckets > 0)
        {
            projected.ComputeActivity(ActivityBuckets);
        }

        return projected;
    }

    private static void AddCallActivity(CallTreeNode target, int[] activity)
    {
        if (activity.Length == 0)
        {
            return;
        }

        if (target.OwnActivityCounts.Length != activity.Length)
        {
            target.OwnActivityCounts = new int[activity.Length];
        }

        for (var i = 0; i < activity.Length; i++)
        {
            target.OwnActivityCounts[i] += activity[i];
        }
    }

    private CallTreeNode GetChild(CallTreeNode parent, CallFrame frame)
    {
        if (!parent.ChildrenByKey.TryGetValue((frame.Address, frame.InstanceAddress), out var child))
        {
            child = new CallTreeNode { Frame = frame, Parent = parent, Order = NextOrder++ };

            parent.AddChild(child);
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
        => CallActivitySource is { } source ? source.Value.GetValueOrDefault(node) ?? [] : node.OwnActivityCounts;

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

            foreach (var child in current.Children)
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

    public IEnumerable<CallTreeNode> GetNodes()
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

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }
    }

    public static string Render(CallTreeNode node)
    {
        var builder = new StringBuilder();

        RenderNode(node, 0, builder);

        return builder.ToString();
    }

    internal HashSet<CallTreeNode> RemoveSubtrees(Func<CallTreeNode, bool> isRemoved)
    {
        var removed = new HashSet<CallTreeNode>(ReferenceEqualityComparer.Instance);

        var stack = new Stack<(CallTreeNode Node, bool IsRemoved)>();

        stack.Push((Root, false));

        while (stack.Count > 0)
        {
            var (node, isNodeRemoved) = stack.Pop();

            foreach (var child in node.Children.ToList())
            {
                var isChildRemoved = isNodeRemoved || isRemoved(child);

                if (isChildRemoved && removed.Add(child) && !isNodeRemoved)
                {
                    node.RemoveChild(child);
                }

                stack.Push((child, isChildRemoved));
            }
        }

        return removed;
    }

    private static void RenderNode(CallTreeNode node, int depth, StringBuilder builder)
    {
        builder.Append(' ', depth * 2);

        builder.Append(node.Symbol.Length > 0 ? node.Symbol : node.Frame?.QualifiedName);

        if (node.CallCount > 0)
        {
            builder.Append($" ({node.CallCount} calls)");
        }

        builder.AppendLine();

        foreach (var child in node.Children.OrderBy(c => c.Order))
        {
            RenderNode(child, depth + 1, builder);
        }
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

        public CallTreeNode? GetStop(CallTreeNode node)
        {
            if (stopBelow is null || IsTop(node))
            {
                return null;
            }

            if (!Stops.TryGetValue(node, out var stop))
            {
                stop = GetStop(node.Parent!) ?? (stopBelow(node) ? node : null);

                Stops[node] = stop;
            }

            return stop;
        }

        public IEnumerable<KeyValuePair<CallTreeNode, CallTreeNode>> Projections => Projected;

        public CallTreeNode GetProjected(CallTreeNode node)
        {
            if (!Projected.TryGetValue(node, out var result))
            {
                var parent = IsTop(node) ? projected.Root : GetProjected(node.Parent!);

                result = projected.GetChild(parent, node.Frame!);

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
