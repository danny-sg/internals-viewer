using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class BlobHandleParser
{
    public const int QueryHandleSize = 0x18;

    private const int LayoutMarkerOffset = 2;

    private const byte HandleMarker = 1;

    private const int LayoutStateOffset = 0x1A;

    private const int LayoutRootOffset = 0x1C;

    private const int LayoutSizeHeaderBytes = 16;

    private const int LayoutRootHeaderSize = 12;

    private const byte InRowRootType = 4;

    private const int LayoutLinkSize = 12;

    public static DeferredLobValue? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < LayoutRootOffset + LayoutRootHeaderSize + LayoutLinkSize)
        {
            return null;
        }

        var rootSize = BinaryPrimitives.ReadUInt16LittleEndian(data) - LayoutSizeHeaderBytes;

        if (data[LayoutMarkerOffset] != HandleMarker
            || BinaryPrimitives.ReadUInt16LittleEndian(data[LayoutStateOffset..]) is not (1 or 3)
            || data[LayoutRootOffset] != InRowRootType
            || rootSize < LayoutRootHeaderSize + LayoutLinkSize
            || (rootSize - LayoutRootHeaderSize) % LayoutLinkSize != 0
            || data.Length < LayoutRootOffset + rootSize)
        {
            return null;
        }

        var links = new DeferredLobLink[(rootSize - LayoutRootHeaderSize) / LayoutLinkSize];

        for (var index = 0; index < links.Length; index++)
        {
            var link = data[(LayoutRootOffset + LayoutRootHeaderSize + index * LayoutLinkSize)..];

            var page = new PageAddress(BinaryPrimitives.ReadInt16LittleEndian(link[8..]),
                                       BinaryPrimitives.ReadInt32LittleEndian(link[4..]));

            links[index] = new DeferredLobLink(BinaryPrimitives.ReadInt32LittleEndian(link),
                                               page,
                                               BinaryPrimitives.ReadUInt16LittleEndian(link[10..]));

            if (page.FileId <= 0 || (index > 0 && links[index].Offset <= links[index - 1].Offset))
            {
                return null;
            }
        }

        return new DeferredLobValue(links);
    }
}
