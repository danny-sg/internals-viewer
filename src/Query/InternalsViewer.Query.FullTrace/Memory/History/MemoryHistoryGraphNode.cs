using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public sealed class MemoryHistoryGraphNode(string path,
                                           int depth,
                                           int parent,
                                           MemoryHistoryGraphTarget[] targets,
                                           List<MemoryHistoryGraphMark> marks)
{
    public string Path { get; } = path;

    public int Depth { get; } = depth;

    public int Parent { get; } = parent;

    public MemoryHistoryGraphTarget[] Targets { get; } = targets;

    public List<MemoryHistoryGraphMark> Marks { get; } = marks;

    public int DataChangeCount { get; } = marks.Count(m => !m.IsReference);

    public bool HasPointers { get; internal set; }

    public bool HasReferrers { get; internal set; }

    public MemoryHistoryGraphTarget? Object { get; internal set; }

    public bool IsData => Targets.All(t => t.Block < 0);

    public bool IsReferrer => Depth < 0;

    public MemoryHistoryGraphTarget? GetTarget(TracePosition position)
    {
        var index = SortedSearch.FirstAfter<MemoryHistoryGraphTarget, TracePosition>(Targets, position, t => t.From) - 1;

        if (index >= 0 && position < Targets[index].To)
        {
            return Targets[index];
        }

        return Object is { } found && position >= found.From && position < found.To ? found : null;
    }
}
