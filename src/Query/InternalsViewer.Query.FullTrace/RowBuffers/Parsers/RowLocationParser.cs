using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers.Enums;

namespace InternalsViewer.Query.FullTrace.RowBuffers.Parsers;

internal static class RowLocationParser
{
    private const ulong RowsetOffset = 0x48;

    private const ulong RowObjectOffset = 0x48;

    private const ulong BTreePageOffset = 0x20;

    private const ulong BTreeSlotOffset = 0x30;

    private const ulong HeapBufferOffset = 0x58;

    private const ulong HeapSlotOffset = 0x50;

    private const ulong BufferPageIdOffset = 0x70;

    public static RowPosition? Parse(ITraceReplay replay, ulong iterator, IReadOnlyDictionary<ulong, RowObjectClass> rowObjects)
    {
        if (!replay.TryRead(iterator + RowsetOffset, out ulong rowset)
            || rowset < ColumnValueParser.SmallestAddress
            || !replay.TryRead(rowset + RowObjectOffset, out ulong row)
            || row < ColumnValueParser.SmallestAddress
            || !replay.TryRead(row, out ulong vtable)
            || !rowObjects.TryGetValue(vtable, out var rowClass))
        {
            return null;
        }

        return rowClass switch
        {
            RowObjectClass.BTree
                when replay.TryRead(row + BTreePageOffset, out ulong page)
                     && replay.TryRead(row + BTreeSlotOffset, out int slot)
                     && IsPage(page, slot)
                => new RowPosition(page, slot, PageReferenceParser.ReadPageId(replay, page)),
            RowObjectClass.Heap
                when replay.TryRead(row + HeapBufferOffset, out ulong buffer)
                     && buffer >= ColumnValueParser.SmallestAddress
                     && replay.TryRead(buffer, out ulong page)
                     && replay.TryRead(row + HeapSlotOffset, out int slot)
                     && IsPage(page, slot)
                => new RowPosition(page, slot, ReadBufferPageId(replay, buffer)),
            _ => null
        };
    }

    private static bool IsPage(ulong page, int slot)
        => page >= ColumnValueParser.SmallestAddress && (page & (PageData.Size - 1)) == 0 && slot >= 0;

    private static PageAddress? ReadBufferPageId(ITraceReplay replay, ulong buffer)
    {
        Span<byte> pageId = stackalloc byte[PageAddress.Size];

        return replay.Read(buffer + BufferPageIdOffset, pageId) == pageId.Length ? PageReferenceParser.ParsePageId(pageId) : null;
    }

    internal readonly record struct RowPosition(ulong Frame, int Slot, PageAddress? Page);
}
