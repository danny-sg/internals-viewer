using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Query.Extensions;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;

public static class DeepDataReader
{
    public const uint PageMagic = 0x53445042;

    private const ulong LargestInlineTag = 0xF;

    private const int EntryAlignment = 4;

    private const int MaximumLength = 8000;

    private const int MaximumPages = 4096;

    public static bool IsReference(ulong slot) => (slot & 1) == 1 && slot > LargestInlineTag;

    public static List<ulong> GetPages(ITraceMemoryReader memory, ulong firstPage)
    {
        var pages = new List<ulong>();

        for (var page = firstPage; page != 0 && pages.Count < MaximumPages && IsPage(memory, page); page = GetNextPage(memory, page))
        {
            pages.Add(page);
        }

        return pages;
    }

    public static ulong GetNextPage(ITraceMemoryReader memory, ulong page)
        => memory.TryRead(page + (ulong)memory.Layout.DeepDataPage.NextPageField.Offset, out ulong next) ? next : 0;

    public static List<DeepDataEntry> ReadEntries(ITraceMemoryReader memory, ulong page, int size)
    {
        var layout = memory.Layout.DeepDataPage;

        var end = memory.TryRead(page + (ulong)layout.CapacityField.Offset, out ushort capacity)
            ? Math.Min(size, layout.HeaderSize + capacity)
            : size;

        var entries = new List<DeepDataEntry>();

        var offset = layout.HeaderSize;

        while (offset + DeepDataEntry.HeaderSize <= end
               && ReadEntry(memory, page + (ulong)offset) is { Length: > 0 } entry
               && offset + entry.Size <= end)
        {
            entries.Add(entry);

            offset = (offset + entry.Size + EntryAlignment - 1) & ~(EntryAlignment - 1);
        }

        return entries;
    }

    public static DeepDataEntry? ReadValue(ITraceMemoryReader memory, ulong slot)
        => IsReference(slot) ? ReadEntry(memory, (slot & ~1UL) - DeepDataEntry.HeaderSize) : null;

    private static bool IsPage(ITraceMemoryReader memory, ulong page)
        => memory.TryRead(page + (ulong)memory.Layout.DeepDataPage.MagicField.Offset, out uint magic) && magic == PageMagic;

    private static DeepDataEntry? ReadEntry(ITraceMemoryReader memory, ulong address)
    {
        var lengthAddress = address + DeepDataEntry.HeaderSize - (ulong)ColumnstoreValueConverter.DeepDataPrefixSize;

        if (!memory.TryRead(lengthAddress, out ushort length) || length > MaximumLength)
        {
            return null;
        }

        var data = new byte[length];

        return memory.Read(address + DeepDataEntry.HeaderSize, data) == length ? new DeepDataEntry(address, length, data) : null;
    }
}
