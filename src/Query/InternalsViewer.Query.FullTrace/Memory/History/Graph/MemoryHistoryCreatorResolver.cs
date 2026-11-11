using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Memory.History.Graph;

public sealed class MemoryHistoryCreatorResolver(MemoryHistoryStore store, TraceTimeline timeline, TraceCallStore calls)
{
    private Dictionary<uint, TraceTimelineThread> Threads { get; } = timeline.Threads.ToDictionary(t => t.ThreadId);

    public int GetCreator(int block)
    {
        var info = store.Blocks[block];

        if (!Threads.TryGetValue(info.Thread, out var thread))
        {
            return -1;
        }

        var position = info.FromPosition;

        for (var depth = thread.Rows.Count - 1; depth >= 0; depth--)
        {
            var row = thread.Rows[depth];

            var index = row.GetIndex(position);

            if (index < 0
                || timeline.GetNode(row.GetSpan(index).CallNodeIndex)?.Frame is not { Function.ClassName.Length: > 0 } frame
                || calls.GetCallList(frame.Address, frame.InstanceAddress) is not { } list
                || list.IndexOfEnclosing(position, info.Thread) is not (>= 0 and var call))
            {
                continue;
            }

            var instance = list[call].GetSlot(0);

            var creator = instance == 0 ? -1 : store.GetBlock(instance, position);

            if (creator >= 0 && creator != block)
            {
                return creator;
            }
        }

        return -1;
    }
}
