namespace InternalsViewer.Query.FullTrace.Memory.History;

internal sealed class MemoryHistoryCounts
{
    public long Received { get; set; }

    public long Recorded { get; set; }

    public long Unchanged { get; set; }

    public long BufferPool { get; set; }

    public long Outside { get; set; }

    public long Unwritten { get; set; }

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
