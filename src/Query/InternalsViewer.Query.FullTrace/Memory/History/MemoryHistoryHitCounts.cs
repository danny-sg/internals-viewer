namespace InternalsViewer.Query.FullTrace.Memory.History;

internal sealed class MemoryHistoryHitCounts
{
    public long Recorded { get; private set; }

    public long Unchanged { get; private set; }

    public long BufferPool { get; private set; }

    public long Outside { get; private set; }

    public void Add(bool isRecorded, bool isBufferPool, bool isInBlock)
    {
        if (isRecorded)
        {
            Recorded++;
        }
        else if (isBufferPool)
        {
            BufferPool++;
        }
        else if (isInBlock)
        {
            Unchanged++;
        }
        else
        {
            Outside++;
        }
    }
}
