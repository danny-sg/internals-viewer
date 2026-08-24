using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Entry a string dictionary writes in place of a value too big for the string store
/// </summary>
/// <remarks>
/// The cut is 8000 bytes rather than the store's MaxStringSize, and the tag is the same for varchar, nvarchar and varbinary. The page it
/// names is a LOB page of the table's own LOB allocation unit, holding the value under the same Xpress Huffman envelope archive compression
/// uses.
/// </remarks>
public readonly record struct StringLobPointer(int Length, long BlobId, PageAddress PageAddress, short Slot)
{
    public const int Size = 22;

    private const byte Tag = 0x11;

    private const byte SubTag = 0x01;

    public static bool TryParse(ReadOnlySpan<byte> bytes, out StringLobPointer pointer)
    {
        pointer = default;

        if (bytes.Length != Size || bytes[0] != Tag || bytes[1] != SubTag)
        {
            return false;
        }

        pointer = new StringLobPointer(BinaryPrimitives.ReadInt32LittleEndian(bytes[2..]),
                                       BinaryPrimitives.ReadInt64LittleEndian(bytes[6..]),
                                       new PageAddress(BinaryPrimitives.ReadInt16LittleEndian(bytes[18..]),
                                                       BinaryPrimitives.ReadInt32LittleEndian(bytes[14..])),
                                       BinaryPrimitives.ReadInt16LittleEndian(bytes[20..]));

        return true;
    }
}
