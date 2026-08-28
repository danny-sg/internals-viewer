using InternalsViewer.Execution.AccessPaths.Joins.Hash;

namespace InternalsViewer.Execution.Interfaces.Iterators.Joins;

/// <summary>
/// An operator that groups or joins through a hash table, whichever mode it runs in
/// </summary>
public interface IHashTableSource
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