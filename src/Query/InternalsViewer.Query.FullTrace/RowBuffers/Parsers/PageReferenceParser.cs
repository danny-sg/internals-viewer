using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.DeferredValues;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class PageReferenceParser
{
    private const ulong LayoutDataPageSize = 0x2000;

    private const ulong LayoutPageHeaderSize = 96;

    private const ulong LayoutPageIdOffset = 0x20;

    private const int LayoutPageIdSize = 6;

    private const int LayoutPageIdFileOffset = 4;

    private const ulong LayoutLsnOffset = 0x28;

    private const int LayoutLsnSize = 10;

    private const byte PageHeaderVersion = 1;

    public static DeferredPageValue? Parse(ITraceReplay replay, ulong pointer, ulong length)
    {
        var offset = pointer & (LayoutDataPageSize - 1);

        if (pointer < ColumnValueParser.SmallestAddress || offset < LayoutPageHeaderSize || length > LayoutDataPageSize - offset)
        {
            return null;
        }

        var page = pointer - offset;

        Span<byte> pageId = stackalloc byte[LayoutPageIdSize];

        Span<byte> lsn = stackalloc byte[LayoutLsnSize];

        if (replay.Read(page + LayoutPageIdOffset, pageId) != LayoutPageIdSize
            || replay.Read(page + LayoutLsnOffset, lsn) != LayoutLsnSize
            || BinaryPrimitives.ReadUInt16LittleEndian(pageId[LayoutPageIdFileOffset..]) == 0
            || (replay.TryRead(page, out byte version) && version != PageHeaderVersion))
        {
            return null;
        }

        return new DeferredPageValue(PageAddressParser.Parse(pageId), (int)offset, LogSequenceNumberParser.Parse(lsn));
    }
}
