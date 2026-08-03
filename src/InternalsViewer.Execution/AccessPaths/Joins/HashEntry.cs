using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// A build row held in a hash table bucket, alongside the hash that placed it there
/// </summary>
public readonly record struct HashEntry(uint Hash, IRecord Record)
{
    public AccessKey Key { get; init; }

    /// <summary>
    /// The key holds a NULL, so this entry occupies a bucket but can never match a probe row
    /// </summary>
    public bool HasNullKey { get; init; }

    public bool IsMatched { get; init; }
}
