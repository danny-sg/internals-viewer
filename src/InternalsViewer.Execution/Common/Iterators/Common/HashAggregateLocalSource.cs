using InternalsViewer.Execution.Common.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.Common.Interfaces.Iterators.Joins;

namespace InternalsViewer.Execution.Common.Iterators.Common;

public sealed class HashAggregateLocalSource(HashAggregateBuilder builder, long buildRowEstimate) : IHashTableSource
{
    public HashTable Table => builder.Table;

    public long BuildRowEstimate { get; } = buildRowEstimate;

    public void SetBucketCount(int bucketCount) => builder.Resize(HashAggregateBuilder.BucketBitsOf(bucketCount), true);
}
