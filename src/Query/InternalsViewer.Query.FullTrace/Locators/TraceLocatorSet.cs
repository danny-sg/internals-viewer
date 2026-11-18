using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed partial class TraceLocatorSet
{
    private TraceLocatorSet(Locator[] values, Dictionary<(ulong FunctionAddress, ulong InstanceAddress), LocatorTable> tables)
    {
        Values = values;
        Tables = tables;
        Count = tables.Values.Sum(t => (long)t.Count);
    }

    public static TraceLocatorSet Empty { get; } = new([], []);

    public long Count { get; }

    public int ValueCount => Values.Length;

    public IReadOnlyDictionary<(ulong FunctionAddress, ulong InstanceAddress), LocatorTable> Tables { get; }

    private Locator[] Values { get; }

    public LocatorTable? GetTable(ulong functionAddress, ulong instanceAddress)
        => Tables.GetValueOrDefault((functionAddress, instanceAddress));

    internal static TraceLocatorSet Build(ReplayCallLog log)
    {
        var largest = log.Locators.Count == 0 ? 0 : log.Locators.Values.Max(f => f.Count);

        var found = new FoundLocator[largest];

        var indexes = new int[largest];

        var packed = log.Locators.ToDictionary(f => f.Key, f => Pack(f.Value, log.LocatorPool, found, indexes));

        var used = new bool[log.LocatorPool.Count];

        foreach (var (_, palette, _) in packed.Values)
        {
            foreach (var poolIndex in palette)
            {
                used[poolIndex] = true;
            }
        }

        var (values, remap) = log.LocatorPool.Compact(used);

        var tables = packed.ToDictionary(p => p.Key,
                                         p => new LocatorTable(p.Value.CallIndexes,
                                                               Remap(p.Value.Palette, remap),
                                                               p.Value.PaletteIndexes,
                                                               values));

        return new TraceLocatorSet(values, tables);
    }

    private static (BitPackedArray CallIndexes, int[] Palette, BitPackedArray PaletteIndexes) Pack(ChunkedList<FoundLocator> list,
                                                                                                   LocatorPool pool,
                                                                                                   FoundLocator[] found,
                                                                                                   int[] indexes)
    {
        var span = found.AsSpan(0, list.Count);

        list.CopyTo(span);

        return LocatorTable.Pack(span, pool, indexes.AsSpan(0, list.Count));
    }

    private static int[] Remap(int[] palette, int[] remap)
    {
        for (var index = 0; index < palette.Length; index++)
        {
            palette[index] = remap[palette[index]];
        }

        return palette;
    }
}
