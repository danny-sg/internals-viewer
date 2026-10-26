using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory.History.Batches;

public static class DeepDataReader
{
    public const int PageHeaderSize = 0x18;

    private const uint PageMagic = 0x53445042;

    private const ulong NextPageOffset = 0x08;

    private const ulong PageMagicOffset = 0x10;

    private const ulong PageCapacityOffset = 0x14;

    private const ulong LargestInlineTag = 0xF;

    private const int EntryAlignment = 4;

    private const int MaximumLength = 8000;

    private const int MaximumPages = 4096;

    public static bool IsReference(ulong slot) => (slot & 1) == 1 && slot > LargestInlineTag;

    public static List<ulong> GetPages(IMemoryReader memory, ulong firstPage)
    {
        var pages = new List<ulong>();

        for (var page = firstPage; page != 0 && pages.Count < MaximumPages && IsPage(memory, page); page = GetNextPage(memory, page))
        {
            pages.Add(page);
        }

        return pages;
    }

    public static ulong GetNextPage(IMemoryReader memory, ulong page) => memory.TryRead(page + NextPageOffset, out ulong next) ? next : 0;

    public static List<DeepDataEntry> ReadEntries(IMemoryReader memory, ulong page, int size)
    {
        var end = memory.TryRead(page + PageCapacityOffset, out ushort capacity) ? Math.Min(size, PageHeaderSize + capacity) : size;

        var entries = new List<DeepDataEntry>();

        var offset = PageHeaderSize;

        while (offset + DeepDataEntry.HeaderSize <= end
               && ReadEntry(memory, page + (ulong)offset) is { Length: > 0 } entry
               && offset + entry.Size <= end)
        {
            entries.Add(entry);

            offset = (offset + entry.Size + EntryAlignment - 1) & ~(EntryAlignment - 1);
        }

        return entries;
    }

    public static DeepDataEntry? ReadValue(IMemoryReader memory, ulong slot)
        => IsReference(slot) ? ReadEntry(memory, (slot & ~1UL) - DeepDataEntry.HeaderSize) : null;

    private static bool IsPage(IMemoryReader memory, ulong page)
        => memory.TryRead(page + PageMagicOffset, out uint magic) && magic == PageMagic;

    private static DeepDataEntry? ReadEntry(IMemoryReader memory, ulong address)
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
