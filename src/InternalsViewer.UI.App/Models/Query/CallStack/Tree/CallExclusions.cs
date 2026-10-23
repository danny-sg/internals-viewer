using System.Collections.Concurrent;
using System.Collections.Generic;
using InternalsViewer.Query.CallStack;

namespace InternalsViewer.UI.App.Models.Query.CallStack.Tree;

public sealed class CallExclusions(IReadOnlySet<string> hidden, IReadOnlySet<string> pruned, IReadOnlySet<CallStackNode>? scope = null)
{
    public static CallExclusions None { get; } = new(new HashSet<string>(), new HashSet<string>());

    public bool IsEmpty => hidden.Count == 0 && pruned.Count == 0 && scope is null;

    private ConcurrentDictionary<CallStackNode, bool> Cut { get; } = new(ReferenceEqualityComparer.Instance);

    public bool Includes(CallStackNode? node) => node is null || IsEmpty || !(IsIn(hidden, node) || IsCut(node));

    private bool IsCut(CallStackNode node)
    {
        if (pruned.Count == 0 && scope is null)
        {
            return false;
        }

        if (Cut.TryGetValue(node, out var cached))
        {
            return cached;
        }

        var result = IsIn(pruned, node)
                     || (scope?.Contains(node) != true && (node.Parent is { IsRoot: false } parent ? IsCut(parent) : scope is not null));

        Cut[node] = result;

        return result;
    }

    private static bool IsIn(IReadOnlySet<string> categories, CallStackNode node)
        => node.Frame is not null && categories.Count > 0 && categories.Contains(node.Category);
}
