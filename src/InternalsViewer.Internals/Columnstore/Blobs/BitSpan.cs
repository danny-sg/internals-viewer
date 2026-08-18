namespace InternalsViewer.Internals.Columnstore.Blobs;

/// <summary>
/// Bit granular address within a columnstore blob
/// </summary>
public readonly record struct BitSpan(int BitOffset, int BitLength)
{
    public int ByteOffset => BitOffset >> 3;

    /// <summary>
    /// Bytes the span touches, which is what a byte oriented viewer has to highlight
    /// </summary>
    public int ByteLength => ((BitOffset & 7) + BitLength + 7) >> 3;

    public bool IsByteAligned => ((BitOffset | BitLength) & 7) == 0;

    public static BitSpan FromBytes(int offset, int length) => new(offset << 3, length << 3);
}
