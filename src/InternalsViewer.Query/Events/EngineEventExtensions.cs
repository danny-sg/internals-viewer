using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events;

public static class EngineEventExtensions
{
    /// <summary>
    /// An event together with every event it owns — the End folded into it, and a group's raw members
    /// </summary>
    /// <remarks>
    /// Consolidation leaves only the owner in the top-level list, but the call-stack frames live on the owned: a group
    /// has no stack of its own (its members carry them), and <see cref="Consolidation.IntervalCollapser"/> drops the End
    /// after moving its duration onto the Begin, though the End's frames are the release/completion path. So anything
    /// reasoning about an event's call stack — the crop's keep set, an operator's scope — has to expand through here or
    /// it silently loses those frames.
    ///
    /// Recursive because the two nest: a read group's members are themselves folded Begin/End pairs. Cycle-safe against
    /// a malformed graph via the visited set, which also de-duplicates an event owned by two paths.
    /// </remarks>
    public static IEnumerable<EngineEvent> SelfAndOwned(this EngineEvent engineEvent)
    {
        var visited = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        var pending = new Stack<EngineEvent>();

        pending.Push(engineEvent);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (!visited.Add(current))
            {
                continue;
            }

            yield return current;

            if (current.FoldedFrom is { } folded)
            {
                pending.Push(folded);
            }

            if (current is IEventGroup group)
            {
                foreach (var child in group.Events)
                {
                    pending.Push(child);
                }
            }
        }
    }

    /// <summary>
    /// Expands a set of events to everything they own, by reference identity
    /// </summary>
    public static HashSet<EngineEvent> ExpandOwned(this IEnumerable<EngineEvent> events)
    {
        var expanded = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        foreach (var engineEvent in events)
        {
            foreach (var owned in engineEvent.SelfAndOwned())
            {
                expanded.Add(owned);
            }
        }

        return expanded;
    }
}
