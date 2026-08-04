using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Results;

public abstract partial record AccessStep
{
    /// <summary>
    /// A build row was hashed and placed in a hash table bucket
    /// </summary>
    public sealed record HashBuild(int Bucket, uint Hash) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey Key { get; init; }

        public int Entry { get; init; }

        public int ChainLength { get; init; }

        public int BucketCount { get; init; }

        /// <summary>
        /// The key held a NULL, so the row occupies a bucket but can never match a probe row
        /// </summary>
        public bool IsNullKey { get; init; }
    }

    public sealed record HashBuildRun(int Bucket, uint Hash, int Count) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey Key { get; init; }

        public int ChainLength { get; init; }

        public bool IsNullKey { get; init; }

        public int BucketCount { get; init; }

        public IReadOnlyList<int> BucketFill { get; init; } = [];
    }

    /// <summary>
    /// A probe row was hashed and the bucket it selected is about to be walked
    /// </summary>
    public sealed record HashProbe(int Bucket, uint Hash) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey Key { get; init; }

        public int ChainLength { get; init; }

        public bool IsNullKey { get; init; }

        /// <summary>
        /// The bucket was empty, so the row is rejected without a single key comparison
        /// </summary>
        public bool HasNoCandidates => !IsNullKey && ChainLength == 0;
    }

    /// <summary>
    /// A run of consecutive probe rows carrying the same key, grouped for display
    /// </summary>
    /// <remarks>
    /// A probe side in key order hands the join run after run of rows with the same value, and every one of them hashes to the same bucket
    /// and walks the same chain. Only the number of rows that took that path differs, so the run says it once and counts them.
    /// </remarks>
    public sealed record HashProbeRun(int Bucket, uint Hash, int Count) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey Key { get; init; }

        public int ChainLength { get; init; }

        public bool IsNullKey { get; init; }

        /// <summary>
        /// The bucket was empty, so every row of the run was rejected without a single key comparison
        /// </summary>
        public bool HasNoCandidates => !IsNullKey && ChainLength == 0;
    }

    /// <summary>
    /// A probe row was tested against one entry of a bucket chain
    /// </summary>
    public sealed record HashCompare(int Bucket, int Entry, bool IsMatch) : AccessStep(AccessPhase.Walk)
    {
        public AccessKey ProbeKey { get; init; }

        public AccessKey BuildKey { get; init; }

        /// <summary>
        /// The hashes were equal, so a comparison that did not match cost the join a wasted key test
        /// </summary>
        public bool IsHashMatch { get; init; }

        /// <summary>
        /// The keys were equal, which is not the whole verdict when the join carries a residual
        /// </summary>
        public bool IsKeyMatch { get; init; }

        public bool HasResidual { get; init; }

        /// <summary>
        /// The hash collided but the keys differed, which is the comparison a wider hash would have avoided
        /// </summary>
        public bool IsFalsePositive => IsHashMatch && !IsKeyMatch;

        /// <summary>
        /// A residual was reached, which only happens once the keys have already matched
        /// </summary>
        public bool ShowsResidual => HasResidual && IsKeyMatch;

        public bool IsResidualFail => ShowsResidual && !IsMatch;

        /// <summary>
        /// The entry was reached by following the collision chain rather than being the first the bucket held
        /// </summary>
        public bool IsChained => Entry > 0;
    }
}
