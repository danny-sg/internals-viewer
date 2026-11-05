using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal sealed class AddressTable<T>(Dictionary<ulong, T> entries) : IReadOnlyDictionary<ulong, T>
{
    public AddressTable() : this(new Dictionary<ulong, T>())
    {
    }

    public static AddressTable<T> Empty { get; } = new();

    public int Count => Entries.Count;

    public Dictionary<ulong, T>.KeyCollection Keys => Entries.Keys;

    public Dictionary<ulong, T>.ValueCollection Values => Entries.Values;

    IEnumerable<ulong> IReadOnlyDictionary<ulong, T>.Keys => Keys;

    IEnumerable<T> IReadOnlyDictionary<ulong, T>.Values => Values;

    private Dictionary<ulong, T> Entries { get; } = entries;

    public T this[ulong address] => Entries[address];

    public static AddressTable<T> Merge(IEnumerable<(AddressTable<T> Table, ulong Address)> tables)
    {
        var all = tables.ToList();

        var merged = new Dictionary<ulong, T>(all.Sum(t => t.Table.Count));

        foreach (var (table, offset) in all)
        {
            foreach (var (address, value) in table.Entries)
            {
                merged.TryAdd(address + offset, value);
            }
        }

        return new(merged);
    }

    public bool ContainsKey(ulong address) => Entries.ContainsKey(address);

    public bool TryGetValue(ulong address, [MaybeNullWhen(false)] out T value) => Entries.TryGetValue(address, out value);

    public Dictionary<ulong, T>.Enumerator GetEnumerator() => Entries.GetEnumerator();

    IEnumerator<KeyValuePair<ulong, T>> IEnumerable<KeyValuePair<ulong, T>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
