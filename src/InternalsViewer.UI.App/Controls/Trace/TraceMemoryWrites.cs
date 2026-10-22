using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed class TraceMemoryWrites
{
    public TraceMemoryWrites(TraceMemoryWrite[] writes)
    {
        Writes = writes;
        Indexes = [];
        Count = writes.Length;
    }

    public TraceMemoryWrites(MemoryHistoryStore store, int[] indexes)
    {
        Store = store;
        Writes = [];
        Indexes = indexes;
        Count = indexes.Length;
    }

    public int Count { get; }

    private MemoryHistoryStore? Store { get; }

    private TraceMemoryWrite[] Writes { get; }

    private int[] Indexes { get; }

    public TraceMemoryWrite this[int index]
    {
        get
        {
            if (Store is not { } store)
            {
                return Writes[index];
            }

            var write = store.GetWrite(Indexes[index]);

            var (offset, length) = store.GetChangedExtent(write);

            return new TraceMemoryWrite(store.GetPoint(Indexes[index]), offset, length, write.CallNodeIndex);
        }
    }

    public TracePoint GetPoint(int index) => Store is { } store ? store.GetPoint(Indexes[index]) : Writes[index].Point;
}
