using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records.Blob.BlobPointers;
using InternalsViewer.Internals.Services.Loaders.Records.Fields;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class BlobHandleParser
{
    public const int QueryHandleSize = 0x18;

    private const int MarkerOffset = 2;

    private const byte HandleMarker = 1;

    private const int StateOffset = 0x1A;

    private const int RootOffset = 0x1C;

    private const int SizeHeaderBytes = 16;

    private const int LinkSize = sizeof(int) + RowIdentifier.Size;

    public static DeferredLobValue? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < RootOffset + LobRootFieldLoader.ChildOffset + LinkSize)
        {
            return null;
        }

        var rootSize = BinaryPrimitives.ReadUInt16LittleEndian(data) - SizeHeaderBytes;

        if (data[MarkerOffset] != HandleMarker
            || BinaryPrimitives.ReadUInt16LittleEndian(data[StateOffset..]) is not (1 or 3)
            || data[RootOffset] != (byte)BlobFieldType.LobRoot
            || rootSize < LobRootFieldLoader.ChildOffset + LinkSize
            || (rootSize - LobRootFieldLoader.ChildOffset) % LinkSize != 0
            || data.Length < RootOffset + rootSize)
        {
            return null;
        }

        var links = new DeferredLobLink[(rootSize - LobRootFieldLoader.ChildOffset) / LinkSize];

        for (var index = 0; index < links.Length; index++)
        {
            var link = data[(RootOffset + LobRootFieldLoader.ChildOffset + index * LinkSize)..];

            var row = new RowIdentifier(link.Slice(sizeof(int), RowIdentifier.Size));

            links[index] = new DeferredLobLink(BinaryPrimitives.ReadInt32LittleEndian(link), row.PageAddress, row.SlotId);

            if (row.PageAddress.FileId <= 0 || (index > 0 && links[index].Offset <= links[index - 1].Offset))
            {
                return null;
            }
        }

        return new DeferredLobValue(links);
    }
}
