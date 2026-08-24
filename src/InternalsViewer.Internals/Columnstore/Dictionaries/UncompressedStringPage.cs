using InternalsViewer.Internals.Annotations;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// String Page
/// </summary>
/// <remarks>
/// String values are held consecutively in the Content with a length prefix of one or two bytes. The first byte has its high bit set if the
/// length is greater than 127, in which case the second byte is used to hold the rest of the length.
/// </remarks>
public sealed class UncompressedStringPage : StringPage
{
    public const int HeaderSize = 24;

    [DataStructureItem(ItemType.StringPageFreeSpace)]
    public int FreeSpace { get; set; }

    [DataStructureItem(ItemType.StringPageFreeSpaceOffset)]
    public int FreeSpaceOffset { get; set; }

    [DataStructureItem(ItemType.StringPageUncompressedSize)]
    public int UncompressedDataSize { get; set; }

    public ReadOnlyMemory<byte> Content { get; set; }

    public override void Mark()
    {
        base.Mark();

        MarkProperty(nameof(FreeSpace), Offset + 0x0C, 4);
        MarkProperty(nameof(FreeSpaceOffset), Offset + 0x10, 4);
        MarkProperty(nameof(UncompressedDataSize), Offset + 0x14, 4);
    }

    /// <summary>
    /// Where an entry sits within the page, the length prefix being one byte or two
    /// </summary>
    /// <remarks>
    /// A prefix byte with its high bit set carries only bits zero to six of the length, the byte after it holding
    /// bit seven in its own high bit and bits eight upward in the rest.
    /// </remarks>
    public StringEntryExtent GetExtent(int handleOffset)
    {
        var span = Content.Span;

        var first = span[handleOffset];

        var prefixLength = (first & ContinuationFlag) == 0 ? 1 : 2;

        var length = prefixLength == 1 ? first : DecodeLength(first, span[handleOffset + 1]);

        return new StringEntryExtent(Offset + HeaderSize + handleOffset, prefixLength, length);
    }

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
