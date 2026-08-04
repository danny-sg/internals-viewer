using InternalsViewer.Execution.AccessPaths.Joins.Hash;

namespace InternalsViewer.Execution.Interfaces.Iterators.Joins;

public interface IHashTableIterator : IIterator
{
    HashTable Table { get; }

    /// <summary>
    /// Rows the build side was expected to produce, which is what the table was sized for
    /// </summary>
    long BuildRowEstimate { get; }

    /// <summary>
    /// Rebuilds the table at a different bucket count without restarting the walk
    /// </summary>
    void SetBucketCount(int bucketCount);
}
