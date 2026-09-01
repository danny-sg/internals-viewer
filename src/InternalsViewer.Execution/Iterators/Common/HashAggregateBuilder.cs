using System.Numerics;
using System.Runtime.InteropServices;
using InternalsViewer.Execution.AccessPaths.Aggregation;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Common;

public readonly record struct HashAggregateHit(int Bucket,
                                              int Entry,
                                              uint Hash,
                                              AccessKey Key,
                                              AggregateGroupRecord Group,
                                              bool IsNew);

public sealed class HashAggregateBuilder(IReadOnlyList<string> groupBy, IReadOnlyList<AggregateColumn> aggregates, int bucketBits)
{
    public HashTable Table { get; } = new(bucketBits);

    public IReadOnlyList<string> GroupBy { get; } = groupBy;

    public IReadOnlyList<AggregateColumn> Aggregates { get; } = aggregates;

    public long InputRowCount { get; private set; }

    public long GroupCount => Table.RowCount;

    private int? PendingBucketBits { get; set; }

    public void Resize(int bucketBits, bool immediate)
    {
        if (immediate)
        {
            Table.Resize(bucketBits);

            return;
        }

        PendingBucketBits = bucketBits;
    }

    public static int BucketBitsOf(int bucketCount)
    {
        if (bucketCount < 2 || (bucketCount & (bucketCount - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be a power of two.");
        }

        return BitOperations.Log2((uint)bucketCount);
    }

    public HashAggregateHit Accumulate(IRecord row, EvaluationContext context) => Accumulate(row, context, null);

    public HashAggregateHit Accumulate(IRecord row, EvaluationContext context, AccessKey? localKey, int skipKeyFields = 0)
    {
        ApplyPendingResize();

        var key = localKey ?? GetKey(row);

        var hash = JoinHash.Compute(key, key.Count);

        var (bucket, entry, group, isNew) = Find(hash, key, row, localKey, skipKeyFields);

        group.Add(new RecordRowValueSource(row), context);

        InputRowCount++;

        return new HashAggregateHit(bucket, entry, hash, key, group, isNew);
    }

    public HashAggregateHit AccumulateRun(IRecord row, EvaluationContext context, long count, AccessKey? localKey, int skipKeyFields)
    {
        ApplyPendingResize();

        var key = localKey ?? GetKey(row);

        var hash = JoinHash.Compute(key, key.Count);

        var (bucket, entry, group, isNew) = Find(hash, key, row, localKey, skipKeyFields);

        group.AddRun(new RecordRowValueSource(row), context, count);

        InputRowCount += count;

        return new HashAggregateHit(bucket, entry, hash, key, group, isNew);
    }

    public HashAggregateHit Merge(AggregateGroupRecord source)
    {
        ApplyPendingResize();

        var key = GetKey(source);

        var hash = JoinHash.Compute(key, key.Count);

        var (bucket, entry, group, isNew) = Find(hash, key, source, null, 0);

        group.Combine(source);

        InputRowCount += source.RowCount;

        return new HashAggregateHit(bucket, entry, hash, key, group, isNew);
    }

    public void Clear()
    {
        Table.Clear();

        InputRowCount = 0;
    }

    public static string RunningText(AggregateGroupRecord group)
        => string.Join(", ", group.Accumulators.Select(a => $"{a.Column.ToText()} = {AccessValueFormatter.ToText(a.Result)}"));

    private (int Bucket, int Entry, AggregateGroupRecord Group, bool IsNew) Find(uint hash,
                                                                                 AccessKey key,
                                                                                 IRecord row,
                                                                                 AccessKey? keyFields,
                                                                                 int skipKeyFields)
    {
        var bucket = Table.GetBucket(hash);

        for (var index = 0; index < bucket.Count; index++)
        {
            var candidate = bucket.Entries[index];

            if (candidate.Hash == hash
                && candidate.Record is AggregateGroupRecord existing
                && candidate.Key.ComparePrefix(key, key.Count) == 0)
            {
                return (bucket.Index, index, existing, false);
            }
        }

        var group = new AggregateGroupRecord(GroupFields(row, keyFields, skipKeyFields), Aggregates);

        var (added, entry) = Table.Add(hash, key, group, JoinHash.HasNull(key, key.Count));

        return (added, entry, group, true);
    }

    private List<RecordField> GroupFields(IRecord row, AccessKey? keyFields, int skipKeyFields)
    {
        var fields = new List<RecordField>(GroupBy.Count + (keyFields?.Count ?? 0));

        if (keyFields is { } key)
        {
            for (var index = skipKeyFields; index < key.Count; index++)
            {
                fields.Add(new ComputedField(key[index].ColumnName, key[index]));
            }
        }

        foreach (var column in GroupBy)
        {
            if (FindField(row, column) is { } field)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private AccessKey GetKey(IRecord record)
    {
        var source = new RecordRowValueSource(record);

        var values = new AccessValue[GroupBy.Count];

        for (var index = 0; index < GroupBy.Count; index++)
        {
            var column = GroupBy[index];

            if (FindField(record, column) is null)
            {
                throw new InvalidOperationException($"Row has no column '{column}' to group on");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey(ImmutableCollectionsMarshal.AsImmutableArray(values));
    }

    private void ApplyPendingResize()
    {
        if (PendingBucketBits is { } bucketBits)
        {
            Table.Resize(bucketBits);

            PendingBucketBits = null;
        }
    }

    private static RecordField? FindField(IRecord record, string column)
        => record.Fields.FirstOrDefault(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase));
}
