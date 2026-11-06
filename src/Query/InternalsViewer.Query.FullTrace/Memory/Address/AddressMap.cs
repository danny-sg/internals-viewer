using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Helpers;
using InternalsViewer.Query.FullTrace.Memory.Address.Enums;
using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Memory.Address;

public sealed partial class AddressMap
{
    private AddressMap(Dictionary<ulong, AddressMapEntry[]> entries)
    {
        var labels = new Dictionary<Label, int>();

        Entries = entries.ToDictionary(e => e.Key, e => Array.ConvertAll(e.Value, entry => Store(entry, labels)));

        Labels = [.. labels.OrderBy(l => l.Value).Select(l => l.Key)];

        Blocks = new AddressIndex([.. Entries.Where(e => e.Value.Any(entry => entry.Bytes > 0)).Select(e => e.Key).Order()],
                                  Entries.Values.SelectMany(e => e).Select(e => e.Bytes).DefaultIfEmpty(0ul).Max());
    }

    public static AddressMap Empty { get; } = new([]);

    public int Count => Entries.Values.Sum(e => e.Length);

    internal long Bytes => Count * (long)Unsafe.SizeOf<StoredEntry>() + Labels.Length * (long)Unsafe.SizeOf<Label>();

    private Dictionary<ulong, StoredEntry[]> Entries { get; }

    private Label[] Labels { get; }

    private AddressIndex Blocks { get; }

    public AddressMapEntry? GetEntry(ulong address, TracePosition position)
    {
        if (!position.IsKnown || !Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        var index = GetEntryIndex(entries, position);

        return index < 0 ? null : ToEntry(address, entries[index]);
    }

    public string? GetLabel(ulong address, TracePosition from, TracePosition to)
        => GetLabelValue(address, from, to, static l => l.ClassName) ?? GetLabelValue(address, from, to, static l => l.Site);

    public string? GetClassName(ulong address, TracePosition from, TracePosition to)
        => GetLabelValue(address, from, to, static l => l.ClassName);

    public (AddressMapEntry Entry, ulong Offset)? GetContaining(ulong address, TracePosition position)
    {
        if (!position.IsKnown)
        {
            return null;
        }

        (ulong Address, StoredEntry Entry, ulong Offset)? innermost = null;

        foreach (var index in Blocks.GetCandidates(address, isStartIncluded: false))
        {
            var block = Blocks.Starts[index];

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

    internal IEnumerable<AddressMapEntry> GetEntries() => Entries.SelectMany(e => e.Value.Select(entry => ToEntry(e.Key, entry)));

    internal int GetSiteCount()
        => Entries.Values.Sum(e => e.Count(entry => Labels[entry.LabelIndex] is { Operation: AddressMapOperation.Allocated, Site: not null }));

    internal static AddressMap Build(IEnumerable<AddressEntry> entries,
                                    Func<int, CallTreeNode?> nodeOf,
                                    Func<AddressEntry, CallTreeNode?, AddressMapAllocation>? getAllocation = null)
        => new(entries.Select(e => GetEntry(e, nodeOf, getAllocation))
                      .GroupBy(e => e.Address)
                      .ToDictionary(g => g.Key, g => Link([.. g.OrderBy(e => e.FromPosition)])));

    private static AddressMapEntry GetEntry(AddressEntry entry,
                                           Func<int, CallTreeNode?> nodeOf,
                                           Func<AddressEntry, CallTreeNode?, AddressMapAllocation>? getAllocation)
    {
        var operation = entry.Operation;

        var node = nodeOf(entry.CallNodeIndex);

        var allocates = operation == AddressMapOperation.Allocated;

        var (bytes, site, memoryObject) = allocates && entry.Bytes == 0 && getAllocation is not null
            ? getAllocation(entry, node)
            : new AddressMapAllocation(entry.Bytes, entry.Site, entry.MemoryObject);

        return new AddressMapEntry(entry.Address,
                                  operation,
                                  entry.CallIndex,
                                  entry.Owner,
                                  entry.FromPosition,
                                  entry.ToPosition,
                                  entry.Thread,
                                  entry.CallNodeIndex,
                                  entry.ClassName ?? (allocates ? null : GetName(node?.Frame?.Function?.ClassName)),
                                  allocates ? GetName(node?.Symbol) : null,
                                  bytes,
                                  site,
                                  memoryObject);
    }

    private static AddressMapEntry[] Link(AddressMapEntry[] entries)
    {
        for (var index = entries.Length - 2; index >= 0; index--)
        {
            if (entries[index] is { Operation: AddressMapOperation.Declared } declared
                && entries[index + 1] is { Operation: not AddressMapOperation.Allocated, ClassName: not null } refined
                && IsLiveAt(declared, refined.FromPosition))
            {
                entries[index] = declared with { ClassName = refined.ClassName };
            }
        }

        for (var index = 0; index + 1 < entries.Length; index++)
        {
            var previous = entries[index];

            var next = entries[index + 1];

            if (next.Operation == AddressMapOperation.Allocated)
            {
                continue;
            }

            if (previous.Operation == AddressMapOperation.Allocated)
            {
                entries[index] = previous with { ClassName = next.ClassName ?? previous.ClassName };
            }

            if (previous.Allocator is not null && IsLiveAt(previous, next.FromPosition))
            {
                entries[index + 1] = next with
                {
                    Allocator = previous.Allocator,
                    Bytes = previous.Bytes,
                    Site = previous.Site,
                    MemoryObject = previous.MemoryObject
                };
            }
        }

        return entries;
    }

    private string? GetLabelValue(ulong address, TracePosition from, TracePosition to, Func<Label, string?> getValue)
    {
        if (!Entries.TryGetValue(address, out var entries))
        {
            return null;
        }

        foreach (var entry in entries)
        {
            if (IsWithin(entry, from, to) && getValue(Labels[entry.LabelIndex]) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsLiveAt(AddressMapEntry entry, TracePosition position) => !entry.ToPosition.IsKnown || position <= entry.ToPosition;

    private static bool IsWithin(StoredEntry entry, TracePosition from, TracePosition to)
        => entry.FromPosition >= from && entry.FromPosition < to;

    private static string? GetName(string? name) => name is { Length: > 0 } ? name : null;

    private static StoredEntry Store(AddressMapEntry entry, Dictionary<Label, int> labels)
    {
        var label = new Label(entry.Operation, entry.ClassName, entry.Allocator, entry.Site, entry.MemoryObject);

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
                   && Labels[entries[0].LabelIndex].Operation is AddressMapOperation.Visited or AddressMapOperation.Declared
                ? 0
                : -1;
        }

        return entries[index].ToPosition.IsKnown && position > entries[index].ToPosition ? -1 : index;
    }

    private AddressMapEntry ToEntry(ulong address, StoredEntry entry)
    {
        var label = Labels[entry.LabelIndex];

        return new AddressMapEntry(address,
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
                                  label.Site,
                                  label.MemoryObject);
    }

    private readonly record struct StoredEntry(ulong Owner,
                                               TracePosition FromPosition,
                                               TracePosition ToPosition,
                                               ulong Bytes,
                                               int CallIndex,
                                               uint Thread,
                                               int CallNodeIndex,
                                               int LabelIndex);

    private readonly record struct Label(AddressMapOperation Operation,
                                         string? ClassName,
                                         string? Allocator,
                                         string? Site,
                                         string? MemoryObject);
}
