namespace InternalsViewer.Query.FullTrace.Memory.History.Capture;

internal sealed class MemoryHistoryCaptureCounter
{
    public long ReceivedCount { get; set; }

    public long CapturedCount { get; set; }

    public long UnchangedCount { get; set; }

    public long BufferPoolCount { get; set; }

    public long OutsideCount { get; set; }

    public long UnwrittenCount { get; set; }

    public void Add(bool isCaptured, bool isBufferPool, bool isInBlock)
    {
        if (isCaptured)
        {
            CapturedCount++;
        }
        else if (isBufferPool)
        {
            BufferPoolCount++;
        }
        else if (isInBlock)
        {
            UnchangedCount++;
        }
        else
        {
            OutsideCount++;
        }
    }
}
