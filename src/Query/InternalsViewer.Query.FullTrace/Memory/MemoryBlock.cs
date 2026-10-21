namespace InternalsViewer.Query.FullTrace.Memory;

internal readonly record struct MemoryBlock(ulong Pointer, ulong Bytes, uint Thread, TracePosition Start, TracePosition FreedAt)
{
    public bool Contains(ulong address, TracePosition position)
        => address >= Pointer && address - Pointer < Bytes && Start <= position && position < FreedAt;
}
