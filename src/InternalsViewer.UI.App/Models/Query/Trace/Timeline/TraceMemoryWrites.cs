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

    public TraceMemoryWrites(MemoryHistoryStore store, int first, int count)
    {
        Store = store;
        Writes = [];
        First = first;
        Count = count;
    }

    public int Count { get; }

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

            var write = store.GetWrite(First + index);

            var (offset, length) = store.GetChangedExtent(write);

            return new TraceMemoryWrite(store.GetPoint(First + index), offset, length, write.CallNodeIndex);
        }
    }

    public TracePoint GetPoint(int index) => Store is { } store ? store.GetPoint(First + index) : Writes[index].Point;

    public int GetCallNodeIndex(int index)
        => Store is { } store ? store.GetWrite(First + index).CallNodeIndex : Writes[index].CallNodeIndex;
}
