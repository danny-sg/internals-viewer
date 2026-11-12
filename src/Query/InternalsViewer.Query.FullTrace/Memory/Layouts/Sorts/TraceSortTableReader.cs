using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.Address;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Sorts;

public static class TraceSortTableReader
{
    public const string IteratorClass = "CQScanSortNew";

    public const string ComparePageClass = "ROWCMP";

    private const string RowsetClass = "RowsetSorted";

    private const string DescriptorClass = "SORTDES";

    private const int PageSize = 8192;

    private static int EntriesPerPage => PageSize / EngineLayout.SortEntry.Size;

    public static ulong? GetIterator(MemoryHistoryStore store, AddressMap addressMap, IEnumerable<TraceOperatorLifetime> lifetimes)
    {
        foreach (var lifetime in lifetimes)
        {
            foreach (var instance in lifetime.Instances)
            {
                if (store.GetBlock(instance, lifetime.Constructed.Position) is >= 0 and var block
                    && store.Blocks[block] is { } info
                    && info.Address == instance
                    && addressMap.GetLabel(info.Address, info.FromPosition, info.ToPosition) == IteratorClass)
                {
                    return instance;
                }
            }
        }

        return null;
    }

    public static List<(int Block, TraceSortPartType Type)> GetPages(MemoryHistoryStore store, AddressMap addressMap, ulong iterator)
    {
        var pages = new List<(int Block, TraceSortPartType Type)>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var info = store.Blocks[block];

            if (info.Iterator != iterator || info.Size != PageSize)
            {
                continue;
            }

            var label = addressMap.GetLabel(info.Address, info.FromPosition, info.ToPosition);

            if (label == WorkfilePageReader.ClassName)
            {
                pages.Add((block, TraceSortPartType.RecordPage));
            }
            else if (label == ComparePageClass)
            {
                pages.Add((block, TraceSortPartType.ComparePage));
            }
        }

        return pages;
    }

    public static List<TraceMemoryUsage> GetMemory(MemoryHistoryStore store, AddressMap addressMap, ulong iterator)
    {
        var blocks = new List<(int Block, string? Category)>();

        for (var block = 0; block < store.Blocks.Count; block++)
        {
            var info = store.Blocks[block];

            if (info.Iterator == iterator)
            {
                blocks.Add((block, GetMemoryCategory(addressMap.GetLabel(info.Address, info.FromPosition, info.ToPosition))));
            }
        }

        return TraceMemoryUsage.Build(store, blocks);
    }

    public static TraceSortTable? Read(MemoryHistoryStore store,
                                       IReadOnlyList<(int Block, TraceSortPartType Type)> pages,
                                       ulong iterator,
                                       TracePosition position)
    {
        var memory = new MemoryHistoryReader(store, position);

        if (!TryRead(memory, iterator, EngineLayout.SortIterator.RowsetField, out ulong rowset) || rowset < TraceAddress.MinimumAddress)
        {
            return null;
        }

        TryRead(memory, rowset, EngineLayout.SortRowset.DescriptorField, out ulong descriptor);

        var cursor = 0UL;

        if (descriptor >= TraceAddress.MinimumAddress)
        {
            TryRead(memory, descriptor, EngineLayout.SortDescriptor.CursorField, out cursor);
        }

        var live = pages.Where(p => p.Block >= 0 && store.IsLive(p.Block, position))
                        .OrderBy(p => store.Blocks[p.Block].FromPosition)
                        .ToList();

        List<ulong> recordPages = [.. live.Where(p => p.Type == TraceSortPartType.RecordPage).Select(p => store.Blocks[p.Block].Address)];

        List<ulong> comparePages = [.. live.Where(p => p.Type == TraceSortPartType.ComparePage).Select(p => store.Blocks[p.Block].Address)];

        var entries = ReadEntries(memory, comparePages);

        var runs = ReadRuns(memory, descriptor, entries);

        return new TraceSortTable(iterator,
                                  rowset,
                                  descriptor < TraceAddress.MinimumAddress ? 0 : descriptor,
                                  cursor,
                                  recordPages,
                                  comparePages,
                                  SetRuns(entries, runs),
                                  runs);
    }

    private static string? GetMemoryCategory(string? label) => label switch
    {
        WorkfilePageReader.ClassName => "Record Pages",
        ComparePageClass => "Compare Pages",
        RowsetClass => "Rowset",
        DescriptorClass => "Sort Descriptor",
        IteratorClass => "Sort Iterator",
        _ => null
    };

    private static List<TraceSortEntry> ReadEntries(IMemoryReader memory, List<ulong> pages)
    {
        var entries = new List<TraceSortEntry>();

        foreach (var page in pages)
        {
            for (var index = 0; index < EntriesPerPage; index++)
            {
                var address = page + (ulong)(index * EngineLayout.SortEntry.Size);

                if (!TryRead(memory, address, EngineLayout.SortEntry.RecordField, out ulong record) || record == 0)
                {
                    break;
                }

                TryRead(memory, address, EngineLayout.SortEntry.KeyField, out ulong key);

                TryRead(memory, address, EngineLayout.SortEntry.RecordLengthField, out ushort recordLength);

                TryRead(memory, address, EngineLayout.SortEntry.KeyLengthField, out ushort keyLength);

                TryRead(memory, address, EngineLayout.SortEntry.NextField, out ulong next);

                entries.Add(new TraceSortEntry(entries.Count, address, record, key, recordLength, keyLength, next));
            }
        }

        return entries;
    }

    private static List<TraceSortRun> ReadRuns(IMemoryReader memory, ulong descriptor, List<TraceSortEntry> entries)
    {
        var runs = new List<TraceSortRun>();

        if (descriptor < TraceAddress.MinimumAddress)
        {
            return runs;
        }

        var addresses = entries.Select(e => e.Address).ToHashSet();

        foreach (var field in EngineLayout.SortDescriptor.RunHeadFields)
        {
            if (TryRead(memory, descriptor, field, out ulong head) && addresses.Contains(head))
            {
                runs.Add(new TraceSortRun(field, descriptor + (ulong)field.Offset, head));
            }
        }

        return runs;
    }

    private static List<TraceSortEntry> SetRuns(List<TraceSortEntry> entries, List<TraceSortRun> runs)
    {
        var byAddress = new Dictionary<ulong, int>();

        for (var index = 0; index < entries.Count; index++)
        {
            byAddress[entries[index].Address] = index;
        }

        for (var run = 0; run < runs.Count; run++)
        {
            for (var (current, order) = (byAddress[runs[run].Head], 0); current >= 0 && entries[current].Run < 0; order++)
            {
                entries[current] = entries[current] with { Run = run, SortedIndex = order };

                current = byAddress.TryGetValue(entries[current].Next, out var next) ? next : -1;
            }
        }

        return entries;
    }

    private static bool TryRead<T>(IMemoryReader memory, ulong holder, MemoryField field, out T value) where T : unmanaged
        => memory.TryRead(holder + (ulong)field.Offset, out value);
}
