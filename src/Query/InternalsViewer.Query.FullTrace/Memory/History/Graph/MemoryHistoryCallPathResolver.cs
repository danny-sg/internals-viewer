using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Memory.History.Graph;

public sealed class MemoryHistoryCallPathResolver(MemoryHistoryStore store, TraceTimeline timeline, TraceCallStore calls)
{
    private const ulong MinimumAddress = 0x10000;

    private const ulong MaximumAddress = 0x7FFF_FFFF_FFFF;

    private Dictionary<uint, TraceTimelineThread> Threads { get; } = timeline.Threads.ToDictionary(t => t.ThreadId);

    public List<MemoryHistoryCallObject> GetPath(int block)
    {
        var path = new List<MemoryHistoryCallObject>();

        var info = store.Blocks[block];

        if (!Threads.TryGetValue(info.Thread, out var thread))
        {
            return path;
        }

        var position = info.FromPosition;

        for (var depth = 0; depth < thread.Rows.Count; depth++)
        {
            var row = thread.Rows[depth];

            var index = row.GetIndex(position);

            if (index < 0 || timeline.GetNode(row.GetSpan(index).CallNodeIndex)?.Frame is not { Function.ClassName.Length: > 0 } frame)
            {
                continue;
            }

            var instance = calls.GetCallList(frame.Address, frame.InstanceAddress) is { } list
                           && list.IndexOfEnclosing(position, info.Thread) is >= 0 and var call
                           && list[call].GetSlot(0) is >= MinimumAddress and <= MaximumAddress and var slot
                ? slot
                : 0;

            var className = frame.Function!.ClassName;

            if (path.Count > 0 && IsSameObject(path[^1], className, instance))
            {
                continue;
            }

            path.Add(new MemoryHistoryCallObject(className, instance));
        }

        return path;
    }

    private static bool IsSameObject(MemoryHistoryCallObject previous, string className, ulong instance)
        => instance != 0 ? previous.Instance == instance : previous.Instance == 0 && previous.ClassName == className;
}
