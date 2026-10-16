using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryMap
{
    private MemoryMap(Dictionary<ulong, MemoryMapEntry[]> entries)
    {
        Entries = entries;
    }

    public static MemoryMap Empty { get; } = new([]);

    public int Count => Entries.Values.Sum(e => e.Length);

    private Dictionary<ulong, MemoryMapEntry[]> Entries { get; }

    public MemoryMapEntry? GetEntry(ulong address, TracePosition position)
    {
        if (!position.IsKnown || !Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        var index = SortedSearch.FirstAfter(entries, position, e => e.FromPosition) - 1;

        if (index < 0)
        {
            return entries[0] is { Operation: MemoryMapOperation.Visited, Owner: 0 } first ? first : null;
        }

        var entry = entries[index];

        return entry.ToPosition.IsKnown && position > entry.ToPosition ? null : entry;
    }

    internal static MemoryMap Build(IEnumerable<AddressEntry> entries, Func<int, CallStackNode?> nodeOf)
        => new(entries.Select(e => GetEntry(e, nodeOf))
                      .GroupBy(e => e.Address)
                      .ToDictionary(g => g.Key, g => Link([.. g.OrderBy(e => e.FromPosition)])));

    private static MemoryMapEntry GetEntry(AddressEntry entry, Func<int, CallStackNode?> nodeOf)
    {
        var operation = entry.Operation;

        var node = nodeOf(entry.Node);

        var allocates = operation == MemoryMapOperation.Allocated;

        return new MemoryMapEntry(entry.Address,
                                  operation,
                                  entry.Call,
                                  entry.FromPosition,
                                  entry.Owner,
                                  entry.ToPosition,
                                  entry.Thread,
                                  entry.Node,
                                  allocates ? null : GetName(node?.Frame?.Resolved?.ClassName),
                                  allocates ? GetName(node?.Symbol) : null);
    }

    private static MemoryMapEntry[] Link(MemoryMapEntry[] entries)
    {
        for (var index = 0; index + 1 < entries.Length; index++)
        {
            if (entries[index] is not { Operation: MemoryMapOperation.Allocated } allocation
                || entries[index + 1] is not { Operation: not MemoryMapOperation.Allocated } built)
            {
                continue;
            }

            entries[index] = allocation with { ClassName = built.ClassName };

            entries[index + 1] = built with { Allocator = allocation.Allocator };
        }

        return entries;
    }

    private static string? GetName(string? name) => name is { Length: > 0 } ? name : null;
}
