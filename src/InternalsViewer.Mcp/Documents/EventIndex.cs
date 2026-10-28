using InternalsViewer.Query;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Mcp.Documents;

public sealed class EventIndex
{
    public EventIndex(QueryResult result)
    {
        TopLevel = new HashSet<EngineEvent>(result.EngineEvents, ReferenceEqualityComparer.Instance);

        foreach (var engineEvent in result.EngineEvents)
        {
            Add(engineEvent);
        }
    }

    public int Count => Events.Count;

    private Lock Sync { get; } = new();

    private Dictionary<EngineEvent, int> Ids { get; } = new(ReferenceEqualityComparer.Instance);

    private List<EngineEvent> Events { get; } = [];

    private HashSet<EngineEvent> TopLevel { get; }

    public int GetId(EngineEvent engineEvent)
    {
        lock (Sync)
        {
            return Register(engineEvent);
        }
    }

    public EngineEvent? GetEvent(int id)
    {
        lock (Sync)
        {
            return id >= 0 && id < Events.Count ? Events[id] : null;
        }
    }

    public bool IsTopLevel(EngineEvent engineEvent) => TopLevel.Contains(engineEvent);

    public EngineEvent[] GetEvents(bool includeChildren)
    {
        lock (Sync)
        {
            var events = includeChildren ? Events : Events.Where(TopLevel.Contains);

            return [.. events.OrderBy(e => e.TimeUs).ThenBy(Ids.GetValueOrDefault)];
        }
    }

    public static IEnumerable<EngineEvent> GetChildren(EngineEvent engineEvent)
    {
        if (engineEvent is IEventGroup group)
        {
            foreach (var child in group.Events)
            {
                yield return child;
            }
        }

        if (engineEvent.FoldedFrom is { } folded)
        {
            yield return folded;
        }

        if (engineEvent is ReadEventGroup { PoolLookup: { } lookup })
        {
            yield return lookup;
        }
    }

    private void Add(EngineEvent root)
    {
        var stack = new Stack<(EngineEvent Event, bool Expanded)>();

        var visiting = new HashSet<EngineEvent>(ReferenceEqualityComparer.Instance);

        stack.Push((root, false));

        while (stack.Count > 0)
        {
            var (current, expanded) = stack.Pop();

            if (Ids.ContainsKey(current))
            {
                continue;
            }

            if (expanded)
            {
                Register(current);

                continue;
            }

            if (!visiting.Add(current))
            {
                continue;
            }

            stack.Push((current, true));

            foreach (var child in GetChildren(current))
            {
                if (!Ids.ContainsKey(child))
                {
                    stack.Push((child, false));
                }
            }
        }
    }

    private int Register(EngineEvent engineEvent)
    {
        if (Ids.TryGetValue(engineEvent, out var id))
        {
            return id;
        }

        id = Events.Count;

        Ids[engineEvent] = id;

        Events.Add(engineEvent);

        return id;
    }
}
