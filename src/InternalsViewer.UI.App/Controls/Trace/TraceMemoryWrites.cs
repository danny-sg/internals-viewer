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

            return new TraceMemoryWrite(write.Point, offset, length, write.CallNodeIndex);
        }
    }
}
