namespace InternalsViewer.Query.FullTrace.Timeline.Tracks;

internal sealed class TraceWatchValue(int start, int length)
{
    private byte[] Bytes { get; } = new byte[length];

    private bool[] Known { get; } = new bool[length];

    private byte[]? Last { get; set; }

    public (int From, int To)? GetOverlap(int offset, int count)
    {
        var from = Math.Max(offset, start);

        var to = Math.Min(offset + count, start + length);

        return from < to ? (from, to) : null;
    }

    public Span<byte> GetTarget(int from, int to)
    {
        Known.AsSpan(from - start, to - from).Fill(true);

        return Bytes.AsSpan(from - start, to - from);
    }

    public byte[]? TakeChange()
    {
        if (Array.IndexOf(Known, false) >= 0 || (Last is not null && Bytes.AsSpan().SequenceEqual(Last)))
        {
            return null;
        }

        Last = [.. Bytes];

        return Last;
    }
}
