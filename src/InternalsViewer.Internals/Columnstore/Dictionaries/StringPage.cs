using System.Text;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Page of dictionary string values
/// </summary>
public abstract class StringPage : DataStructure
{
    protected const int ContinuationFlag = 0x80;

    [DataStructureItem(ItemType.StringPageSubLobType)]
    public SubLobType SubLobType { get; set; }

    [DataStructureItem(ItemType.StringPageFlags)]
    public int PageFlags { get; set; }

    [DataStructureItem(ItemType.StringPageStringCount)]
    public int StringCount { get; set; }

    /// <summary>
    /// Where the page starts in the dictionary blob, its fields being marked against the blob rather than itself
    /// </summary>
    public int Offset { get; set; }

    public int Size { get; set; }

    /// <summary>
    /// Records the header fields, which a page only does once its offset within the blob is known
    /// </summary>
    public virtual void Mark()
    {
        MarkProperty(nameof(SubLobType), Offset, 4);
        MarkProperty(nameof(PageFlags), Offset + 0x04, 4);
        MarkProperty(nameof(StringCount), Offset + 0x08, 4);
    }

    public string GetValue(int handleOffset, Encoding encoding) => encoding.GetString(GetBytes(handleOffset));

    /// <summary>
    /// Copy of the entry bytes, safe to hold beyond the next read
    /// </summary>
    public byte[] GetValueBytes(int handleOffset)
    {
        var span = GetBytes(handleOffset);

        var bytes = new byte[span.Length];

        span.CopyTo(bytes);

        return bytes;
    }

    /// <summary>
    /// Combines the two bytes an entry length spans when the first carries the continuation flag
    /// </summary>
    /// <remarks>
    /// The first byte holds bits 0 to 6, the second holds bit 7 in its own high bit and bits 8 upward in the rest.
    /// </remarks>
    protected static int DecodeLength(int first, int second)
        => (first & 0x7F) | (second & ContinuationFlag) | ((second & 0x7F) << 8);

    protected abstract ReadOnlySpan<byte> GetBytes(int handleOffset);
}
