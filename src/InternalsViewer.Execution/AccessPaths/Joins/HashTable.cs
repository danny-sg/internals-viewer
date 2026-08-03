using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// The build side hash table a hash match probes against
/// </summary>
public sealed class HashTable
{
    public HashTable(int bucketBits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketBits, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bucketBits, 16);

        BucketBits = bucketBits;

        Slots = [.. Enumerable.Range(0, 1 << bucketBits).Select(i => new HashBucket(i))];
    }

    public int BucketBits { get; private set; }

    public int BucketCount => Slots.Length;

    public IReadOnlyList<HashBucket> Buckets => Slots;

    public int RowCount { get; private set; }

    public int LongestChain { get; private set; }

    private HashBucket[] Slots { get; set; }

    /// <summary>
    /// Adds a build row, returning the bucket it landed in and its position in that bucket's chain
    /// </summary>
    public (int Bucket, int Entry) Add(uint hash, AccessKey key, IRecord record, bool hasNullKey = false)
    {
        var index = JoinHash.GetBucket(hash, BucketBits);

        var bucket = Slots[index];

        var entry = bucket.Add(new HashEntry(hash, record) { Key = key, HasNullKey = hasNullKey });

        RowCount++;

        if (bucket.Count > LongestChain)
        {
            LongestChain = bucket.Count;
        }

        return (index, entry);
    }

    public HashBucket GetBucket(uint hash) => Slots[JoinHash.GetBucket(hash, BucketBits)];

    public void MarkMatched(int bucket, int entry)
    {
        Slots[bucket].MarkMatched(entry);
    }

    public void Clear()
    {
        foreach (var bucket in Slots)
        {
            bucket.Clear();
        }

        RowCount = 0;
        LongestChain = 0;
    }

    /// <summary>
    /// Rebuilds the table at a different bucket count, redistributing the rows it already holds
    /// </summary>
    /// <remarks>
    /// A row's hash does not depend on the bucket count, only the slice taken from it does, so every entry keeps its stored hash and simply
    /// moves to the slot that hash now selects. Chains keep their build order because the buckets are walked in order.
    /// </remarks>
    public void Resize(int bucketBits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketBits, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bucketBits, 16);

        if (bucketBits == BucketBits)
        {
            return;
        }

        var entries = Slots.SelectMany(b => b.Entries).ToList();

        BucketBits = bucketBits;

        Slots = [.. Enumerable.Range(0, 1 << bucketBits).Select(i => new HashBucket(i))];

        LongestChain = 0;

        foreach (var entry in entries)
        {
            var bucket = Slots[JoinHash.GetBucket(entry.Hash, BucketBits)];

            bucket.Add(entry);

            if (bucket.Count > LongestChain)
            {
                LongestChain = bucket.Count;
            }
        }
    }
}
