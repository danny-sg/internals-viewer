namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryBlock(ulong Pointer, ulong Bytes, uint Thread, double Start, double FreedAt)
{
    public bool Contains(ulong address, double position)
        => address >= Pointer && address - Pointer < Bytes && Start <= position && position < FreedAt;
}
