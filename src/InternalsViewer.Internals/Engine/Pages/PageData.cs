using System.Buffers.Binary;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// Page Data
/// </summary>
public class PageData : DataStructure
{
    /// <summary>
    /// Page size is 8192 bytes/8KB
    /// </summary>
    public const int Size = 8192;

    /// <summary>
    /// Database the page belongs to
    /// </summary>
    public DatabaseSource Database { get; init; } = null!;

    /// <summary>
    /// Page Address in the format File Id : Page Id
    /// </summary>
    public PageAddress PageAddress { get; init; }

    /// <summary>
    /// Raw page data
    /// </summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Page Header
    /// </summary>
    public PageHeader PageHeader { get => field ??= new(); init; }

    /// <summary>
    /// Table/Array containing the data offset of each row in the page
    /// </summary>
    public ushort[] OffsetTable { get => field ??= LoadOffsetTable(); set; }

    /// <summary>
    /// Load the offset table with a given slot count from the page data
    /// </summary>
    private ushort[] LoadOffsetTable()
    {
        var slotCount = PageHeader.SlotCount;

        var offsetTable = new ushort[slotCount];

        ReadOnlySpan<byte> span = Data;

        var offset = Size - 2;

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            offsetTable[slotIndex] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));

            offset -= 2;
        }

        return offsetTable;
    }
}