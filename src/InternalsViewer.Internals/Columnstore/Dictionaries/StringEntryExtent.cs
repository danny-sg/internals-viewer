namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Where an uncompressed entry sits in the blob, split into the length prefix and the bytes it counts
/// </summary>
public readonly record struct StringEntryExtent(int Offset, int PrefixLength, int Length)
{
    public int ValueOffset => Offset + PrefixLength;

    public int TotalLength => PrefixLength + Length;
}
