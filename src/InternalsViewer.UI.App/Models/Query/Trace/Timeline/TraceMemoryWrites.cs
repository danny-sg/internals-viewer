using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.UI.App.Models.Query.Trace.Timeline;

public sealed class TraceMemoryWrites
{
    public TraceMemoryWrites(TraceMemoryWrite[] writes)
    {
        Writes = writes;
        Count = writes.Length;
    }

    public TraceMemoryWrites(MemoryHistoryStore store, int first, int count, bool isReads = false)
    {
        Store = store;
        Writes = [];
        First = first;
        Count = count;
        IsReads = isReads;
    }

    public int Count { get; }

    public bool IsReads { get; }

    private MemoryHistoryStore? Store { get; }

    private TraceMemoryWrite[] Writes { get; }

    private int First { get; }

    public TraceMemoryWrite this[int index]
    {
        get
        {
            if (Store is not { } store)
            {
                return Writes[index];
            }

            if (IsReads)
            {
                var read = store.GetRead(First + index);

                return new TraceMemoryWrite(store.GetReadPoint(First + index), read.Offset, read.Length, read.ReplayNodeIndex);
            }

            var write = store.GetWrite(First + index);

            var (offset, length) = store.GetChangedExtent(write);

            return new TraceMemoryWrite(store.GetPoint(First + index), offset, length, write.ReplayNodeIndex);
        }
    }

    public TracePoint GetPoint(int index)
        => Store is not { } store ? Writes[index].Point
           : IsReads ? store.GetReadPoint(First + index)
           : store.GetPoint(First + index);

    public int GetReplayNodeIndex(int index)
        => Store is not { } store ? Writes[index].ReplayNodeIndex
           : IsReads ? store.GetRead(First + index).ReplayNodeIndex
           : store.GetWrite(First + index).ReplayNodeIndex;
}
