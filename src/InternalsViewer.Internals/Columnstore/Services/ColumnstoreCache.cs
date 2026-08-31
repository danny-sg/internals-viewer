using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Columnstore.Services;

public sealed class ColumnstoreCache
{
    public const long DataBudget = 256L * 1024 * 1024;

    private ConditionalWeakTable<DatabaseSource, Entry> Entries { get; } = [];

    public ColumnStoreIndex? GetIndex(DatabaseSource database, long allocationUnitId)
        => Entries.TryGetValue(database, out var entry) && entry.Indexes.TryGetValue(allocationUnitId, out var index)
            ? index
            : null;

    public void SetIndex(DatabaseSource database, long allocationUnitId, ColumnStoreIndex index)
        => Entries.GetOrCreateValue(database).Indexes[allocationUnitId] = index;

    public byte[]? GetData(DatabaseSource database, RowIdentifier identifier)
        => Entries.TryGetValue(database, out var entry) && entry.Data.TryGetValue(identifier, out var data)
            ? data
            : null;

    public void SetData(DatabaseSource database, RowIdentifier identifier, byte[] data)
    {
        var entry = Entries.GetOrCreateValue(database);

        if (Interlocked.Read(ref entry.Bytes) + data.LongLength > DataBudget)
        {
            return;
        }

        if (entry.Data.TryAdd(identifier, data))
        {
            Interlocked.Add(ref entry.Bytes, data.LongLength);
        }
    }

    public void SetPageRead(DatabaseSource database, ColumnstorePageRead read)
    {
        var reads = Entries.GetOrCreateValue(database)
                           .PageReads
                           .GetOrAdd(read.PageAddress,
                                     _ => new ConcurrentDictionary<ColumnstorePageRead, ColumnstorePageRead>());

        reads.AddOrUpdate(read with { Bytes = 0 }, read, (_, existing) => existing.Bytes >= read.Bytes ? existing : read);
    }

    public IReadOnlyList<ColumnstorePageRead> GetPageReads(DatabaseSource database, PageAddress pageAddress)
        => Entries.TryGetValue(database, out var entry) && entry.PageReads.TryGetValue(pageAddress, out var reads)
            ? [.. reads.Values]
            : [];

    public IReadOnlyCollection<ColumnstorePageRead> PageReads(DatabaseSource database)
        => Entries.TryGetValue(database, out var entry) ? [.. entry.PageReads.Values.SelectMany(r => r.Values)] : [];

    public void Clear(DatabaseSource database) => Entries.Remove(database);

    private sealed class Entry
    {
        public long Bytes;

        public ConcurrentDictionary<long, ColumnStoreIndex> Indexes { get; } = new();

        public ConcurrentDictionary<RowIdentifier, byte[]> Data { get; } = new();

        public ConcurrentDictionary<PageAddress, ConcurrentDictionary<ColumnstorePageRead, ColumnstorePageRead>> PageReads { get; } = new();
    }
}
