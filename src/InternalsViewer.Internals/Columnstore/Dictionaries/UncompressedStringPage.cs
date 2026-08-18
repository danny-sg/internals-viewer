namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// String page holding length prefixed values back to back
/// </summary>
public sealed class UncompressedStringPage : StringPage
{
    public const int HeaderSize = 24;

    public int FreeSpace { get; set; }

    public int FreeSpaceOffset { get; set; }

    public int UncompressedDataSize { get; set; }

    public ReadOnlyMemory<byte> Content { get; set; }

    protected override ReadOnlySpan<byte> GetBytes(int handleOffset)
    {
        var span = Content.Span;

        var position = handleOffset;

        var first = span[position++];

        int length = first;

        if ((first & ContinuationFlag) != 0)
        {
            var second = span[position++];

            length = DecodeLength(first, second);
        }

        return span.Slice(position, length);
    }
}
