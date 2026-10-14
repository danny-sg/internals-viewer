using System.Collections.Concurrent;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class CallExclusions(IReadOnlySet<string> hidden, IReadOnlySet<string> pruned)
{
    public static CallExclusions None { get; } = new(new HashSet<string>(), new HashSet<string>());

    public bool IsEmpty => hidden.Count == 0 && pruned.Count == 0;

    private ConcurrentDictionary<CallStackNode, bool> Pruned { get; } = new(ReferenceEqualityComparer.Instance);

    public bool Includes(CallStackNode? node) => node is null || IsEmpty || !(IsIn(hidden, node) || IsPruned(node));

    private bool IsPruned(CallStackNode node)
    {
        if (pruned.Count == 0)
        {
            return false;
        }

        if (Pruned.TryGetValue(node, out var cached))
        {
            return cached;
        }

        var result = IsIn(pruned, node) || (node.Parent is { } parent && IsPruned(parent));

        Pruned[node] = result;

        return result;
    }

    private static bool IsIn(IReadOnlySet<string> categories, CallStackNode node)
        => node.Frame is not null && categories.Count > 0 && categories.Contains(node.Category);
}
