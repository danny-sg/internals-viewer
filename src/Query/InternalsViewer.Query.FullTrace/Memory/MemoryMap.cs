using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory;

public sealed partial class MemoryMap
{
    private MemoryMap(Dictionary<ulong, MemoryMapEntry[]> entries)
    {
        var labels = new Dictionary<Label, int>();

        Entries = entries.ToDictionary(e => e.Key, e => Array.ConvertAll(e.Value, entry => Store(entry, labels)));

        Labels = [.. labels.OrderBy(l => l.Value).Select(l => l.Key)];

        BlockAddresses = [.. Entries.Where(e => e.Value.Any(entry => entry.Bytes > 0)).Select(e => e.Key).Order()];

        MaximumBytes = Entries.Values.SelectMany(e => e).Select(e => e.Bytes).DefaultIfEmpty(0ul).Max();
    }

    public static MemoryMap Empty { get; } = new([]);

    public int Count => Entries.Values.Sum(e => e.Length);

    internal long Bytes => Count * (long)Unsafe.SizeOf<StoredEntry>() + Labels.Length * (long)Unsafe.SizeOf<Label>();

    private Dictionary<ulong, StoredEntry[]> Entries { get; }

    private Label[] Labels { get; }

    private ulong[] BlockAddresses { get; }

    private ulong MaximumBytes { get; }

    public MemoryMapEntry? GetEntry(ulong address, TracePosition position)
    {
        if (!position.IsKnown || !Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        var index = GetEntryIndex(entries, position);

        return index < 0 ? null : ToEntry(address, entries[index]);
    }

    public string? GetLabel(ulong address, TracePosition from, TracePosition to)
    {
        if (!Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        string? site = null;

        foreach (var entry in entries)
        {
            if (!IsWithin(entry, from, to))
            {
                continue;
            }

            var label = Labels[entry.Label];

            if (label.ClassName is { } className)
            {
                return className;
            }

            site ??= label.Site;
        }

        return site;
    }

    public string? GetClassName(ulong address, TracePosition from, TracePosition to)
    {
        if (!Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (IsWithin(entry, from, to) && Labels[entry.Label].ClassName is { } className)
            {
                return className;
            }
        }

        return null;
    }

    public string? GetAllocator(ulong address, TracePosition from, TracePosition to)
    {
        if (!Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (IsWithin(entry, from, to) && Labels[entry.Label].Allocator is { } allocator)
            {
                return allocator;
            }
        }

        return null;
    }

    public (MemoryMapEntry Entry, ulong Offset)? GetContaining(ulong address, TracePosition position)
    {
        if (!position.IsKnown)
        {
            return null;
        }

        (ulong Address, StoredEntry Entry, ulong Offset)? innermost = null;

        for (var index = SortedSearch.FirstAtOrAfter(BlockAddresses, address) - 1;
             index >= 0 && address - BlockAddresses[index] < MaximumBytes;
             index--)
        {
            var block = BlockAddresses[index];

            var offset = address - block;

            var entries = Entries[block];

            if (GetEntryIndex(entries, position) is >= 0 and var found
                && entries[found] is { Bytes: > 0 } entry
                && offset < entry.Bytes
                && (innermost is null || entry.Bytes < innermost.Value.Entry.Bytes))
            {
                innermost = (block, entry, offset);
            }
        }

        return innermost is { } containing ? (ToEntry(containing.Address, containing.Entry), containing.Offset) : null;
    }

    internal IEnumerable<MemoryMapEntry> GetEntries() => Entries.SelectMany(e => e.Value.Select(entry => ToEntry(e.Key, entry)));

    internal int GetSiteCount()
        => Entries.Values.Sum(e => e.Count(entry => Labels[entry.Label] is { Operation: MemoryMapOperation.Allocated, Site: not null }));

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

    private static bool IsWithin(StoredEntry entry, TracePosition from, TracePosition to)
        => entry.FromPosition >= from && entry.FromPosition < to;

    private static string? GetName(string? name) => name is { Length: > 0 } ? name : null;

    private static StoredEntry Store(MemoryMapEntry entry, Dictionary<Label, int> labels)
    {
        var label = new Label(entry.Operation, entry.ClassName, entry.Allocator, entry.Site);

        ref var index = ref CollectionsMarshal.GetValueRefOrAddDefault(labels, label, out var exists);

        if (!exists)
        {
            index = labels.Count - 1;
        }

        return new StoredEntry(entry.Owner,
                               entry.FromPosition,
                               entry.ToPosition,
                               entry.Bytes,
                               entry.CallIndex,
                               entry.Thread,
                               entry.CallNodeIndex,
                               index);
    }

    private int GetEntryIndex(StoredEntry[] entries, TracePosition position)
    {
        var index = SortedSearch.FirstAfter(entries, position, static e => e.FromPosition) - 1;

        if (index < 0)
        {
            return entries[0].Owner == 0
                   && Labels[entries[0].Label].Operation is MemoryMapOperation.Visited or MemoryMapOperation.Declared
                ? 0
                : -1;
        }

        return entries[index].ToPosition.IsKnown && position > entries[index].ToPosition ? -1 : index;
    }

    private MemoryMapEntry ToEntry(ulong address, StoredEntry entry)
    {
        var label = Labels[entry.Label];

        return new MemoryMapEntry(address,
                                  label.Operation,
                                  entry.CallIndex,
                                  entry.Owner,
                                  entry.FromPosition,
                                  entry.ToPosition,
                                  entry.Thread,
                                  entry.CallNodeIndex,
                                  label.ClassName,
                                  label.Allocator,
                                  entry.Bytes,
                                  label.Site);
    }

    private readonly record struct StoredEntry(ulong Owner,
                                               TracePosition FromPosition,
                                               TracePosition ToPosition,
                                               ulong Bytes,
                                               int CallIndex,
                                               uint Thread,
                                               int CallNodeIndex,
                                               int Label);

    private readonly record struct Label(MemoryMapOperation Operation, string? ClassName, string? Allocator, string? Site);
}
