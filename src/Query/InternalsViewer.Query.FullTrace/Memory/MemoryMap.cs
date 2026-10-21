using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryMap
{
    private MemoryMap(Dictionary<ulong, MemoryMapEntry[]> entries)
    {
        Entries = entries;

        BlockAddresses = [.. entries.Where(e => e.Value.Any(entry => entry.Bytes > 0)).Select(e => e.Key).Order()];

        MaximumBytes = entries.Values.SelectMany(e => e).Select(e => e.Bytes).DefaultIfEmpty(0ul).Max();
    }

    public static MemoryMap Empty { get; } = new([]);

    public int Count => Entries.Values.Sum(e => e.Length);

    internal IEnumerable<MemoryMapEntry> GetEntries() => Entries.Values.SelectMany(e => e);

    private Dictionary<ulong, MemoryMapEntry[]> Entries { get; }

    private ulong[] BlockAddresses { get; }

    private ulong MaximumBytes { get; }

    public MemoryMapEntry? GetEntry(ulong address, TracePosition position)
    {
        if (!position.IsKnown || !Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        var index = SortedSearch.FirstAfter(entries, position, e => e.FromPosition) - 1;

        if (index < 0)
        {
            return entries[0] is { Operation: MemoryMapOperation.Visited or MemoryMapOperation.Declared, Owner: 0 } first ? first : null;
        }

        var entry = entries[index];

        return entry.ToPosition.IsKnown && position > entry.ToPosition ? null : entry;
    }

    public (MemoryMapEntry Entry, ulong Offset)? GetContaining(ulong address, TracePosition position)
    {
        (MemoryMapEntry Entry, ulong Offset)? innermost = null;

        for (var index = SortedSearch.FirstAtOrAfter(BlockAddresses, address) - 1;
             index >= 0 && address - BlockAddresses[index] < MaximumBytes;
             index--)
        {
            var offset = address - BlockAddresses[index];

            if (GetEntry(BlockAddresses[index], position) is { Bytes: > 0 } entry
                && offset < entry.Bytes
                && (innermost is null || entry.Bytes < innermost.Value.Entry.Bytes))
            {
                innermost = (entry, offset);
            }
        }

        return innermost;
    }

    internal static MemoryMap Build(IEnumerable<AddressEntry> entries,
                                    Func<int, CallStackNode?> nodeOf,
                                    Func<AddressEntry, CallStackNode?, (ulong Bytes, string? Site)>? getAllocation = null)
        => new(entries.Select(e => GetEntry(e, nodeOf, getAllocation))
                      .GroupBy(e => e.Address)
                      .ToDictionary(g => g.Key, g => Link([.. g.OrderBy(e => e.FromPosition)])));

    private static MemoryMapEntry GetEntry(AddressEntry entry,
                                           Func<int, CallStackNode?> nodeOf,
                                           Func<AddressEntry, CallStackNode?, (ulong Bytes, string? Site)>? getAllocation)
    {
        var operation = entry.Operation;

        var node = nodeOf(entry.CallNodeIndex);

        var allocates = operation == MemoryMapOperation.Allocated;

        var (bytes, site) = allocates && entry.Bytes == 0 && getAllocation is not null
            ? getAllocation(entry, node)
            : (entry.Bytes, entry.Site);

        return new MemoryMapEntry(entry.Address,
                                  operation,
                                  entry.CallIndex,
                                  entry.Owner,
                                  entry.FromPosition,
                                  entry.ToPosition,
                                  entry.Thread,
                                  entry.CallNodeIndex,
                                  entry.ClassName ?? (allocates ? null : GetName(node?.Frame?.Resolved?.ClassName)),
                                  allocates ? GetName(node?.Symbol) : null,
                                  bytes,
                                  site);
    }

    private static MemoryMapEntry[] Link(MemoryMapEntry[] entries)
    {
        for (var index = entries.Length - 2; index >= 0; index--)
        {
            if (entries[index] is { Operation: MemoryMapOperation.Declared } declared
                && entries[index + 1] is { Operation: not MemoryMapOperation.Allocated, ClassName: not null } refined
                && IsLiveAt(declared, refined.FromPosition))
            {
                entries[index] = declared with { ClassName = refined.ClassName };
            }
        }

        for (var index = 0; index + 1 < entries.Length; index++)
        {
            var previous = entries[index];

            var next = entries[index + 1];

            if (next.Operation == MemoryMapOperation.Allocated)
            {
                continue;
            }

            if (previous.Operation == MemoryMapOperation.Allocated)
            {
                entries[index] = previous with { ClassName = next.ClassName ?? previous.ClassName };
            }

            if (previous.Allocator is not null && IsLiveAt(previous, next.FromPosition))
            {
                entries[index + 1] = next with { Allocator = previous.Allocator, Bytes = previous.Bytes, Site = previous.Site };
            }
        }

        return entries;
    }

    private static bool IsLiveAt(MemoryMapEntry entry, TracePosition position) => !entry.ToPosition.IsKnown || position <= entry.ToPosition;

    private static string? GetName(string? name) => name is { Length: > 0 } ? name : null;
}
