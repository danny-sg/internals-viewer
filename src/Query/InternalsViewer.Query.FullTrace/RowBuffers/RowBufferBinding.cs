namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBufferBinding(int RecordOffset, uint Flags, int FixedLength)
{
    public static RowBufferBinding None { get; } = new(-1, 0, 0);
}
