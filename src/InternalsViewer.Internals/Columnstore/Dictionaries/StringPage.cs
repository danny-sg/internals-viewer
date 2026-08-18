using System.Text;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Page of dictionary string values
/// </summary>
public abstract class StringPage
{
    protected const int ContinuationFlag = 0x80;

    public SubLobType SubLobType { get; set; }

    public int PageFlags { get; set; }

    public int StringCount { get; set; }

    public int Offset { get; set; }

    public int Size { get; set; }

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
